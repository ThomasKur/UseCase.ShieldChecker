using Microsoft.EntityFrameworkCore;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Services
{
    /// <summary>
    /// Background service that periodically scans for jobs exceeding the configured
    /// <see cref="Settings.JobTimeout"/> and marks them as Error with an Undetermined result.
    /// </summary>
    public sealed class JobTimeoutService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JobTimeoutService> _logger;

        public JobTimeoutService(IServiceScopeFactory scopeFactory, ILogger<JobTimeoutService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("JobTimeoutService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnforceTimeoutsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "JobTimeoutService encountered an error.");
                }

                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }

            _logger.LogInformation("JobTimeoutService stopped.");
        }

        internal async Task EnforceTimeoutsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ShieldCheckerContext>();

            var settings = await context.Settings.FirstOrDefaultAsync(ct);
            if (settings == null || settings.JobTimeout <= 0)
            {
                _logger.LogTrace("Job timeout not configured or is zero. Skipping.");
                return;
            }

            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(-settings.JobTimeout);

            // Find jobs that are in active processing states and have exceeded the timeout
            var timedOutJobs = await context.TestJobs
                .Where(j =>
                    (j.Status == JobStatus.WaitingForMDE
                     || j.Status == JobStatus.WaitingForDetection
                     || j.Status == JobStatus.Queued)
                    && j.Created < cutoff)
                .ToListAsync(ct);

            if (timedOutJobs.Count == 0)
            {
                _logger.LogTrace("No timed-out jobs found.");
                return;
            }

            _logger.LogInformation("Found {Count} timed-out job(s) (timeout: {Timeout} min).",
                timedOutJobs.Count, settings.JobTimeout);

            foreach (var job in timedOutJobs)
            {
                job.Status = JobStatus.Error;
                job.Result = JobResult.Undetermined;
                job.Modified = now;

                string timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = $"[{timestamp}] Job timed out after {settings.JobTimeout} minutes.{Environment.NewLine}";
                job.SchedulerLog = string.IsNullOrEmpty(job.SchedulerLog) ? logEntry : job.SchedulerLog + logEntry;

                _logger.LogWarning("Job {JobId} timed out (created: {Created}, timeout: {Timeout} min).",
                    job.ID, job.Created, settings.JobTimeout);
            }

            await context.SaveChangesAsync(ct);
        }
    }
}
