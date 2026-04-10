using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using ShieldChecker.WebApp.Models.Db;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace ShieldChecker.WebApp.Services
{
    /// <summary>
    /// Typed HTTP client implementation for the ShieldChecker Backend API.
    ///
    /// Authentication: uses DefaultAzureCredential to obtain an access token scoped to
    /// api://{BACKEND_API_CLIENT_ID}/.default (the WebApp.Access AppRole on the BackendApi
    /// app registration).
    ///
    /// User identity forwarding: the signed-in OIDC user's OID, display name and UPN are
    /// attached as X-User-Oid, X-User-Name, X-User-Upn headers so the BackendApi can
    /// resolve the UserInfo record.  These headers are only trusted by the BackendApi when
    /// the caller holds the WebApp.Access AppRole.
    /// </summary>
    public class BackendApiService : IBackendApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<BackendApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public BackendApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<BackendApiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task AttachAuthHeadersAsync(HttpRequestMessage request, CancellationToken ct = default)
        {
            // Managed-identity token for the BackendApi app registration
            var clientId = _configuration["BACKEND_API_CLIENT_ID"];
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                var credential = new DefaultAzureCredential();
                var tokenContext = new TokenRequestContext(new[] { $"api://{clientId}/.default" });
                var token = await credential.GetTokenAsync(tokenContext, ct);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            }

            // Forward the signed-in user's identity so the BackendApi can resolve UserInfo
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var oid = user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
                var name = user.FindFirst("name")?.Value;
                var upn = user.Identity.Name;
                if (!string.IsNullOrWhiteSpace(oid))
                {
                    request.Headers.TryAddWithoutValidation("X-User-Oid", oid);
                    if (!string.IsNullOrWhiteSpace(name)) request.Headers.TryAddWithoutValidation("X-User-Name", name);
                    if (!string.IsNullOrWhiteSpace(upn))  request.Headers.TryAddWithoutValidation("X-User-Upn", upn);
                }
            }
        }

        private string BaseUrl => _configuration["BACKEND_API_BASE_URL"]?.TrimEnd('/') ?? string.Empty;

        private async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{path}");
            await AttachAuthHeadersAsync(request, ct);
            var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return default;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }

        private async Task<T?> PostAsync<T>(string path, object? body = null, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{path}");
            await AttachAuthHeadersAsync(request, ct);
            if (body != null) request.Content = JsonContent.Create(body);
            var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return default;
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }

        private async Task PutAsync(string path, object body, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}{path}");
            await AttachAuthHeadersAsync(request, ct);
            request.Content = JsonContent.Create(body);
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }

        private async Task DeleteAsync(string path, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}{path}");
            await AttachAuthHeadersAsync(request, ct);
            var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                response.EnsureSuccessStatusCode();
        }

        // ── Tests ────────────────────────────────────────────────────────────

        public Task<List<TestDefinition>> GetTestsAsync()
            => GetAsync<List<TestDefinition>>("/api/tests")!;

        public Task<TestDefinition?> GetTestAsync(int id)
            => GetAsync<TestDefinition>($"/api/tests/{id}");

        public async Task<TestDefinition> CreateTestAsync(string name, string? mitreTechnique, string? description)
        {
            var result = await PostAsync<TestDefinition>("/api/tests", new { name, mitreTechnique, description });
            return result!;
        }

        public Task UpdateTestAsync(TestDefinition test)
            => PutAsync($"/api/tests/{test.ID}", test);

        public Task DeleteTestAsync(int id)
            => DeleteAsync($"/api/tests/{id}");

        public Task<List<TestDefinition>> GetTestHistoryAsync(int id)
            => GetAsync<List<TestDefinition>>($"/api/tests/{id}/history")!;

        // ── Jobs ─────────────────────────────────────────────────────────────

        public Task<List<TestJob>> GetJobsAsync()
            => GetAsync<List<TestJob>>("/api/jobs")!;

        public Task<TestJob?> GetJobAsync(int id)
            => GetAsync<TestJob>($"/api/jobs/{id}");

        public Task CancelJobAsync(int id)
            => PostAsync<object>($"/api/jobs/{id}/cancel");

        public async Task<int> RerunJobAsync(int id)
        {
            var result = await PostAsync<RerunResult>($"/api/jobs/{id}/rerun");
            return result?.Id ?? 0;
        }

        public Task ReviewJobAsync(int id, string? reviewResult, JobResult result)
            => PutAsync($"/api/jobs/{id}/review", new { reviewResult, result });

        // ── Settings ─────────────────────────────────────────────────────────

        public Task<Settings?> GetSettingsAsync()
            => GetAsync<Settings>("/api/settings");

        public async Task<Settings> UpdateSettingsAsync(Settings settings)
        {
            await PutAsync("/api/settings", settings);
            return settings;
        }

        public Task<SystemStatus?> GetSystemStatusAsync()
            => GetAsync<SystemStatus>("/api/settings/systemstatus");

        // ── AutoScheduler ─────────────────────────────────────────────────────

        public Task<List<AutoSchedule>> GetAutoSchedulesAsync()
            => GetAsync<List<AutoSchedule>>("/api/autoschedule")!;

        public Task<AutoSchedule?> GetAutoScheduleAsync(int id)
            => GetAsync<AutoSchedule>($"/api/autoschedule/{id}");

        public async Task<AutoSchedule> CreateAutoScheduleAsync(AutoSchedule schedule)
        {
            var result = await PostAsync<AutoSchedule>("/api/autoschedule", schedule);
            return result!;
        }

        public Task UpdateAutoScheduleAsync(AutoSchedule schedule)
            => PutAsync($"/api/autoschedule/{schedule.ID}", schedule);

        public Task DeleteAutoScheduleAsync(int id)
            => DeleteAsync($"/api/autoschedule/{id}");

        // ── Shared Library ────────────────────────────────────────────────────

        public Task<List<SharedTestDefinition>> GetSharedLibraryAsync()
            => GetAsync<List<SharedTestDefinition>>("/api/sharedlibrary")!;

        public Task<SharedTestDefinition?> GetSharedLibraryEntryAsync(int id)
            => GetAsync<SharedTestDefinition>($"/api/sharedlibrary/{id}");

        public async Task<SharedTestDefinition> CreateSharedLibraryEntryAsync(SharedTestDefinition entry)
        {
            var result = await PostAsync<SharedTestDefinition>("/api/sharedlibrary", entry);
            return result!;
        }

        public Task UpdateSharedLibraryEntryAsync(SharedTestDefinition entry)
            => PutAsync($"/api/sharedlibrary/{entry.ID}", entry);

        public Task DeleteSharedLibraryEntryAsync(int id)
            => DeleteAsync($"/api/sharedlibrary/{id}");

        public Task ApproveSharedLibraryEntryAsync(int id)
            => PostAsync<object>($"/api/sharedlibrary/{id}/approve");

        public Task RejectSharedLibraryEntryAsync(int id)
            => PostAsync<object>($"/api/sharedlibrary/{id}/reject");

        public async Task<int> ConsumeSharedLibraryEntryAsync(int id)
        {
            var result = await PostAsync<ConsumeResult>($"/api/sharedlibrary/{id}/consume");
            return result?.Id ?? 0;
        }

        // ── DTOs ──────────────────────────────────────────────────────────────
        private sealed record RerunResult(int Id);
        private sealed record ConsumeResult(int Id);
    }
}
