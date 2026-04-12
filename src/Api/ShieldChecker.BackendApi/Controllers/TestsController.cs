using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.BackendApi.Services;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Controllers
{
    /// <summary>
    /// CRUD operations for test definitions. Called by the WebApp under its managed identity.
    /// Requires the WebApp.Access AppRole issued by ShieldChecker-BackendApi.
    /// User identity is forwarded via X-User-Oid and X-User-Name headers.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class TestsController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;
        private readonly ILogger<TestsController> _logger;
        private readonly IMemoryCache _cache;
        private readonly AuditService _audit;
        private static readonly string AllTestsCacheKey = "tests:all";

        public TestsController(ShieldCheckerContext context, ILogger<TestsController> logger, IMemoryCache cache, AuditService audit)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _audit = audit;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private UserInfo GetOrCreateCallerUser()
        {
            var oid = Request.Headers["X-User-Oid"].FirstOrDefault();
            var name = Request.Headers["X-User-Name"].FirstOrDefault();
            var upn = Request.Headers["X-User-Upn"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(oid))
                throw new InvalidOperationException("X-User-Oid header is required.");

            var id = new Guid(oid);
            var user = _context.UserInfo.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                user = new UserInfo(name, upn, id);
                _context.UserInfo.Add(user);
                _context.SaveChanges();
            }
            else
            {
                if (user.DisplayName != name) { user.DisplayName = name; _context.SaveChanges(); }
                if (user.UserPrincipalName != upn) { user.UserPrincipalName = upn; _context.SaveChanges(); }
            }
            return user;
        }

        private (string? oid, string? name, string? upn) GetCallerHeaders() =>
            (Request.Headers["X-User-Oid"].FirstOrDefault(),
             Request.Headers["X-User-Name"].FirstOrDefault(),
             Request.Headers["X-User-Upn"].FirstOrDefault());

        // ── Endpoints ────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            if (!_cache.TryGetValue(AllTestsCacheKey, out List<TestDefinition>? tests))
            {
                tests = await _context.UseCaseTests
                    .Include(t => t.CreatedBy)
                    .Include(t => t.ModifiedBy)
                    .AsNoTracking()
                    .OrderByDescending(t => t.Created)
                    .ToListAsync(ct);
                _cache.Set(AllTestsCacheKey, tests, TimeSpan.FromMinutes(2));
            }
            return Ok(tests);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var test = await _context.UseCaseTests
                .Include(t => t.CreatedBy)
                .Include(t => t.ModifiedBy)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ID == id, ct);
            return test == null ? NotFound() : Ok(test);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TestDefinitionCreateRequest req, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = GetOrCreateCallerUser();
            var entry = new TestDefinition
            {
                Name = req.Name,
                MitreTechnique = req.MitreTechnique ?? string.Empty,
                Description = req.Description ?? string.Empty,
                ExpectedAlertTitle = "Unknown",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                ScriptTest = "Write-Host \"Start Main Script\"",
                ScriptCleanup = "Write-Host \"Start Cleanup\"",
                ScriptPrerequisites = "Write-Host \"Start Prerquisites\"",
                OperatingSystem = DataAccess.Models.OperatingSystem.Windows,
                ExecutorSystemType = ExecutorSystemType.Worker,
                ExecutorUserType = ExecutorUserType.System,
                ElevationRequired = false,
                Enabled = false,
                ReadOnly = false,
                CreatedBy = user,
                ModifiedBy = user
            };
            _context.UseCaseTests.Add(entry);
            await _context.SaveChangesAsync(ct);
            _cache.Remove(AllTestsCacheKey);
            var (oid, name, upn) = GetCallerHeaders();
            await _audit.LogAsync("TestDefinition", entry.ID, "Create", oid, name, upn, entry.Name, ct);
            return CreatedAtAction(nameof(GetById), new { id = entry.ID }, entry);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TestDefinition test, CancellationToken ct)
        {
            if (id != test.ID) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = GetOrCreateCallerUser();
            test.Modified = DateTime.UtcNow;
            test.ModifiedBy = user;
            _context.Entry(test).State = EntityState.Modified;
            // Don't overwrite Created / CreatedBy
            _context.Entry(test).Property(t => t.Created).IsModified = false;

            try { await _context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.UseCaseTests.Any(t => t.ID == id)) return NotFound();
                throw;
            }
            _cache.Remove(AllTestsCacheKey);
            var (oid, name, upn) = GetCallerHeaders();
            await _audit.LogAsync("TestDefinition", id, "Update", oid, name, upn, test.Name, ct);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var test = await _context.UseCaseTests.FindAsync(new object[] { id }, ct);
            if (test == null) return NotFound();
            _context.UseCaseTests.Remove(test);
            await _context.SaveChangesAsync(ct);
            _cache.Remove(AllTestsCacheKey);
            await _audit.LogAsync("TestDefinition", id, "Delete", Request.Headers["X-User-Oid"].FirstOrDefault(), Request.Headers["X-User-Name"].FirstOrDefault(), Request.Headers["X-User-Upn"].FirstOrDefault(), test.Name, ct);
            return NoContent();
        }

        [HttpGet("{id:int}/history")]
        public async Task<IActionResult> GetHistory(int id, CancellationToken ct)
        {
            var history = await _context.UseCaseTests
                .TemporalAll()
                .Where(t => t.ID == id)
                .AsNoTracking()
                .OrderByDescending(t => EF.Property<DateTime>(t, "PeriodStart"))
                .ToListAsync(ct);
            return Ok(history);
        }

        /// <summary>
        /// Bulk-queue jobs for a list of test IDs. Creates one queued job per test.
        /// </summary>
        [HttpPost("bulk-queue")]
        public async Task<IActionResult> BulkQueue([FromBody] BulkTestQueueRequest req, CancellationToken ct)
        {
            if (req.Ids == null || req.Ids.Count == 0) return BadRequest("No test IDs provided.");
            var tests = await _context.UseCaseTests
                .Where(t => req.Ids.Contains(t.ID) && t.Enabled == true)
                .ToListAsync(ct);
            var newJobs = tests.Select(t => new TestJob
            {
                UseCaseID = t.ID,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Status = JobStatus.Queued,
                Result = JobResult.Undetermined
            }).ToList();
            _context.TestJobs.AddRange(newJobs);
            await _context.SaveChangesAsync(ct);
            _cache.Remove("jobs:all");
            return Ok(new { queued = newJobs.Count, ids = newJobs.Select(j => j.ID) });
        }
    }

    public sealed class TestDefinitionCreateRequest
    {
        public required string Name { get; set; }
        public string? MitreTechnique { get; set; }
        public string? Description { get; set; }
    }

    public sealed class BulkTestQueueRequest
    {
        public List<int> Ids { get; set; } = new();
    }
}
