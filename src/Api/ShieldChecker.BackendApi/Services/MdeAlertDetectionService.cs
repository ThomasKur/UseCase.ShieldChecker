using Microsoft.EntityFrameworkCore;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ShieldChecker.BackendApi.Services
{
    /// <summary>
    /// Background service that monitors jobs in WaitingForDetection status and queries
    /// the Microsoft Graph Security API for matching alerts from Microsoft Defender for Endpoint.
    /// Transitions jobs through WaitingForDetection → Completed/ReviewPending based on detection results.
    /// </summary>
    public sealed class MdeAlertDetectionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MdeAlertDetectionService> _logger;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Minimum time to wait after a job enters WaitingForDetection before checking for alerts.
        /// MDE typically needs time to process and generate alerts.
        /// </summary>
        private static readonly TimeSpan DetectionDelay = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Maximum time window to search for alerts after the test was executed.
        /// </summary>
        private static readonly TimeSpan AlertSearchWindow = TimeSpan.FromHours(4);

        public MdeAlertDetectionService(
            IServiceScopeFactory scopeFactory,
            ILogger<MdeAlertDetectionService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MdeAlertDetectionService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForAlertsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MdeAlertDetectionService encountered an error.");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }

            _logger.LogInformation("MdeAlertDetectionService stopped.");
        }

        internal async Task CheckForAlertsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ShieldCheckerContext>();

            var now = DateTime.UtcNow;
            var readyThreshold = now - DetectionDelay;

            // Find jobs waiting for detection that have waited long enough
            var jobs = await context.TestJobs
                .Include(j => j.UseCase)
                .Where(j => j.Status == JobStatus.WaitingForDetection && j.WorkerEnd != null && j.WorkerEnd <= readyThreshold)
                .ToListAsync(ct);

            if (jobs.Count == 0)
            {
                _logger.LogTrace("No jobs ready for alert detection check.");
                return;
            }

            _logger.LogInformation("Checking MDE alerts for {Count} job(s).", jobs.Count);

            string? accessToken = await AcquireGraphTokenAsync(ct);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Could not acquire Microsoft Graph token. Skipping alert detection cycle.");
                return;
            }

            var settings = await context.Settings.FirstOrDefaultAsync(ct);
            bool reviewMode = settings?.JobReview ?? false;

            foreach (var job in jobs)
            {
                try
                {
                    await EvaluateJobAlertsAsync(context, job, accessToken, reviewMode, now, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to evaluate alerts for job {JobId}.", job.ID);
                    AppendSchedulerLog(job, $"Error checking MDE alerts: {ex.Message}");
                }
            }

            await context.SaveChangesAsync(ct);
        }

        private async Task EvaluateJobAlertsAsync(
            ShieldCheckerContext context,
            TestJob job,
            string accessToken,
            bool reviewMode,
            DateTime now,
            CancellationToken ct)
        {
            if (job.UseCase == null)
            {
                _logger.LogWarning("Job {JobId} has no associated test definition.", job.ID);
                return;
            }

            var expectedTitles = job.UseCase.ExpectedAlertTitle
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var searchStart = job.WorkerStart ?? job.Created;
            var searchEnd = searchStart + AlertSearchWindow;

            // If we've exceeded the search window, finalize the job
            bool windowExpired = now > searchEnd;

            var alerts = await QueryMdeAlertsAsync(
                accessToken,
                job.DefenderMachineId,
                searchStart,
                searchEnd > now ? now : searchEnd,
                ct);

            // Check for matching alerts
            var matchedAlerts = new List<string>();
            var otherAlerts = new List<string>();

            foreach (var alert in alerts)
            {
                bool isExpected = expectedTitles.Any(expected =>
                    alert.Title.Contains(expected, StringComparison.OrdinalIgnoreCase));

                if (isExpected)
                    matchedAlerts.Add(alert.Title);
                else
                    otherAlerts.Add(alert.Title);
            }

            if (matchedAlerts.Count > 0)
            {
                // Expected alert was detected
                job.DetectedAlerts = JsonSerializer.Serialize(new
                {
                    matched = matchedAlerts,
                    other = otherAlerts
                });

                if (otherAlerts.Count > 0)
                    job.Result = JobResult.SuccessWithOtherDetection;
                else
                    job.Result = JobResult.Success;

                job.Status = reviewMode ? JobStatus.ReviewPending : JobStatus.Completed;
                AppendSchedulerLog(job, $"MDE alert detected: {string.Join(", ", matchedAlerts)}");
                _logger.LogInformation("Job {JobId}: Expected alert(s) detected. Result: {Result}.", job.ID, job.Result);
            }
            else if (windowExpired)
            {
                // Search window expired without finding the expected alert
                job.Result = otherAlerts.Count > 0 ? JobResult.SuccessWithOtherDetection : JobResult.Failed;
                job.DetectedAlerts = otherAlerts.Count > 0
                    ? JsonSerializer.Serialize(new { matched = Array.Empty<string>(), other = otherAlerts })
                    : null;
                job.Status = reviewMode ? JobStatus.ReviewPending : JobStatus.Completed;
                AppendSchedulerLog(job, "MDE detection window expired. Expected alert not found.");
                _logger.LogInformation("Job {JobId}: Detection window expired. Result: {Result}.", job.ID, job.Result);
            }
            else
            {
                // Still within the search window, keep waiting
                _logger.LogTrace("Job {JobId}: Still within detection window. Will check again.", job.ID);
            }

            job.Modified = now;
        }

        internal async Task<List<AlertInfo>> QueryMdeAlertsAsync(
            string accessToken,
            string? machineId,
            DateTime from,
            DateTime to,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(machineId))
                return new List<AlertInfo>();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Use Microsoft Graph Security API v2 alerts endpoint with OData filter
            string fromStr = from.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string toStr = to.ToString("yyyy-MM-ddTHH:mm:ssZ");

            string filter = $"evidence/any(e: e/microsoft.graph.security.deviceEvidence/mdeDeviceId eq '{machineId}') " +
                            $"and createdDateTime ge {fromStr} and createdDateTime le {toStr}";

            string url = $"https://graph.microsoft.com/v1.0/security/alerts_v2?$filter={Uri.EscapeDataString(filter)}&$select=title,createdDateTime,severity,status";

            try
            {
                var response = await httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Graph API returned {Status} when querying alerts: {Body}",
                        response.StatusCode, body);
                    return new List<AlertInfo>();
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var results = new List<AlertInfo>();

                if (doc.RootElement.TryGetProperty("value", out var alertsArray))
                {
                    foreach (var alertElement in alertsArray.EnumerateArray())
                    {
                        results.Add(new AlertInfo
                        {
                            Title = alertElement.GetProperty("title").GetString() ?? string.Empty,
                            CreatedDateTime = alertElement.GetProperty("createdDateTime").GetString() ?? string.Empty,
                            Severity = alertElement.TryGetProperty("severity", out var sev) ? sev.GetString() ?? string.Empty : string.Empty,
                        });
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query MDE alerts from Graph API.");
                return new List<AlertInfo>();
            }
        }

        private async Task<string?> AcquireGraphTokenAsync(CancellationToken ct)
        {
            string? tenantId = _configuration["AzureAd:TenantId"];
            string? clientId = _configuration["MdeDetection:ClientId"] ?? _configuration["AzureAd:ClientId"];
            string? clientSecret = _configuration["MdeDetection:ClientSecret"] ?? _configuration["AzureAd:ClientSecret"];

            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogWarning("MDE detection credentials not configured. Set AzureAd:TenantId and MdeDetection:ClientId/ClientSecret.");
                return null;
            }

            try
            {
                using var httpClient = new HttpClient();
                var tokenRequest = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "https://graph.microsoft.com/.default"
                };

                var response = await httpClient.PostAsync(
                    $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
                    new FormUrlEncodedContent(tokenRequest),
                    ct);

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("access_token").GetString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire Microsoft Graph access token.");
                return null;
            }
        }

        private static void AppendSchedulerLog(TestJob job, string message)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            string entry = $"[{timestamp}] {message}{Environment.NewLine}";
            job.SchedulerLog = string.IsNullOrEmpty(job.SchedulerLog) ? entry : job.SchedulerLog + entry;
        }
    }

    /// <summary>Lightweight DTO for MDE alert data from Graph API.</summary>
    internal sealed class AlertInfo
    {
        public string Title { get; set; } = string.Empty;
        public string CreatedDateTime { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }
}
