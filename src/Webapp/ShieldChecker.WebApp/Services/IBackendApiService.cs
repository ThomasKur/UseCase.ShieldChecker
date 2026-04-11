using ShieldChecker.WebApp.Models.Db;

namespace ShieldChecker.WebApp.Services
{
    /// <summary>
    /// Typed HTTP client that communicates with the internal ShieldChecker Backend API.
    /// The WebApp managed identity token (WebApp.Access AppRole) is attached to every request.
    /// Signed-in user identity is forwarded via X-User-Oid, X-User-Name, and X-User-Upn headers.
    /// </summary>
    public interface IBackendApiService
    {
        // ── Tests ────────────────────────────────────────────────────────────
        Task<List<TestDefinition>> GetTestsAsync();
        Task<TestDefinition?> GetTestAsync(int id);
        Task<TestDefinition> CreateTestAsync(string name, string? mitreTechnique, string? description);
        Task UpdateTestAsync(TestDefinition test);
        Task DeleteTestAsync(int id);
        Task<List<TestDefinition>> GetTestHistoryAsync(int id);

        // ── Jobs ─────────────────────────────────────────────────────────────
        Task<List<TestJob>> GetJobsAsync();
        Task<TestJob?> GetJobAsync(int id);
        Task CancelJobAsync(int id);
        Task<int> RerunJobAsync(int id);
        Task ReviewJobAsync(int id, string? reviewResult, JobResult result);

        // ── Settings ─────────────────────────────────────────────────────────
        Task<Settings?> GetSettingsAsync();
        Task<Settings> UpdateSettingsAsync(Settings settings);
        Task<SystemStatus?> GetSystemStatusAsync();

        // ── AutoScheduler ─────────────────────────────────────────────────────
        Task<List<AutoSchedule>> GetAutoSchedulesAsync();
        Task<AutoSchedule?> GetAutoScheduleAsync(int id);
        Task<AutoSchedule> CreateAutoScheduleAsync(AutoSchedule schedule);
        Task UpdateAutoScheduleAsync(AutoSchedule schedule);
        Task DeleteAutoScheduleAsync(int id);

        // ── Shared Library ────────────────────────────────────────────────────
        Task<List<SharedTestDefinition>> GetSharedLibraryAsync();
        Task<SharedTestDefinition?> GetSharedLibraryEntryAsync(int id);
        Task<SharedTestDefinition> CreateSharedLibraryEntryAsync(SharedTestDefinition entry);
        Task UpdateSharedLibraryEntryAsync(SharedTestDefinition entry);
        Task DeleteSharedLibraryEntryAsync(int id);
        Task ApproveSharedLibraryEntryAsync(int id);
        Task RejectSharedLibraryEntryAsync(int id);
        Task<int> ConsumeSharedLibraryEntryAsync(int id);
    }
}
