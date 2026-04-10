using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
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

        public TestsController(ShieldCheckerContext context, ILogger<TestsController> logger)
        {
            _context = context;
            _logger = logger;
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

        // ── Endpoints ────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var tests = await _context.UseCaseTests
                .Include(t => t.CreatedBy)
                .Include(t => t.ModifiedBy)
                .AsNoTracking()
                .OrderByDescending(t => t.Created)
                .ToListAsync(ct);
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
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var test = await _context.UseCaseTests.FindAsync(new object[] { id }, ct);
            if (test == null) return NotFound();
            _context.UseCaseTests.Remove(test);
            await _context.SaveChangesAsync(ct);
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
    }

    public sealed class TestDefinitionCreateRequest
    {
        public required string Name { get; set; }
        public string? MitreTechnique { get; set; }
        public string? Description { get; set; }
    }
}
