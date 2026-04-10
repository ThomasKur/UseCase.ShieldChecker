using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Controllers
{
    /// <summary>
    /// Manage the shared test library: CRUD, share/approve/reject.
    /// Called by the WebApp under its managed identity.
    /// Requires the WebApp.Access AppRole issued by ShieldChecker-BackendApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class SharedLibraryController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;

        public SharedLibraryController(ShieldCheckerContext context)
        {
            _context = context;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private UserInfo? GetCallerUser()
        {
            var oid = Request.Headers["X-User-Oid"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(oid)) return null;
            var id = new Guid(oid);
            return _context.UserInfo.FirstOrDefault(u => u.Id == id);
        }

        // ── Endpoints ────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var entries = await _context.SharedTestLibrary
                .Include(e => e.SubmittedBy)
                .Include(e => e.ApprovedBy)
                .AsNoTracking()
                .ToListAsync(ct);
            return Ok(entries);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var entry = await _context.SharedTestLibrary
                .Include(e => e.SubmittedBy)
                .Include(e => e.ApprovedBy)
                .Include(e => e.Consumptions).ThenInclude(c => c.ConsumedBy)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ID == id, ct);
            return entry == null ? NotFound() : Ok(entry);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SharedTestDefinition entry, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var user = GetCallerUser();
            if (user == null) return BadRequest("X-User-Oid header required.");
            entry.SubmittedBy = user;
            entry.SubmittedAt = DateTime.UtcNow;
            entry.Status = SharedTestStatus.Draft;
            _context.SharedTestLibrary.Add(entry);
            await _context.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id = entry.ID }, entry);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SharedTestDefinition entry, CancellationToken ct)
        {
            if (id != entry.ID) return BadRequest();
            _context.Attach(entry).State = EntityState.Modified;
            _context.Entry(entry).Property(e => e.SubmittedAt).IsModified = false;
            try { await _context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.SharedTestLibrary.Any(e => e.ID == id)) return NotFound();
                throw;
            }
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entry = await _context.SharedTestLibrary.FindAsync(new object[] { id }, ct);
            if (entry == null) return NotFound();
            _context.SharedTestLibrary.Remove(entry);
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

        [HttpPost("{id:int}/approve")]
        public async Task<IActionResult> Approve(int id, CancellationToken ct)
        {
            var entry = await _context.SharedTestLibrary.FindAsync(new object[] { id }, ct);
            if (entry == null) return NotFound();
            var user = GetCallerUser();
            if (user == null) return BadRequest("X-User-Oid header required.");
            entry.Status = SharedTestStatus.Approved;
            entry.ApprovedBy = user;
            entry.ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return Ok();
        }

        [HttpPost("{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, CancellationToken ct)
        {
            var entry = await _context.SharedTestLibrary.FindAsync(new object[] { id }, ct);
            if (entry == null) return NotFound();
            entry.Status = SharedTestStatus.Draft;
            entry.ApprovedBy = null;
            entry.ApprovedAt = null;
            await _context.SaveChangesAsync(ct);
            return Ok();
        }

        [HttpPost("{id:int}/consume")]
        public async Task<IActionResult> Consume(int id, CancellationToken ct)
        {
            var entry = await _context.SharedTestLibrary.FindAsync(new object[] { id }, ct);
            if (entry == null) return NotFound();
            var user = GetCallerUser();
            if (user == null) return BadRequest("X-User-Oid header required.");

            var alreadyConsumed = _context.SharedTestConsumptions
                .Any(c => c.SharedTestDefinitionId == id && c.ConsumedByUserId == user.Id);
            if (alreadyConsumed)
                return Conflict("Test already added to your library.");

            // Clone the shared test into a private TestDefinition
            var test = new TestDefinition
            {
                Name = entry.Name,
                MitreTechnique = entry.MitreTechnique,
                Description = entry.Description,
                ExpectedAlertTitle = entry.ExpectedAlertTitle,
                ScriptTest = entry.ScriptTest,
                ScriptPrerequisites = entry.ScriptPrerequisites,
                ScriptCleanup = entry.ScriptCleanup,
                ElevationRequired = entry.ElevationRequired,
                OperatingSystem = entry.OperatingSystem,
                ExecutorSystemType = entry.ExecutorSystemType,
                ExecutorUserType = entry.ExecutorUserType,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                CreatedBy = user,
                ModifiedBy = user,
                Enabled = false,
                ReadOnly = true,
                SharedLibrarySourceId = entry.ID
            };
            _context.UseCaseTests.Add(test);

            var consumption = new SharedTestConsumption
            {
                SharedTestDefinitionId = id,
                ConsumedByUserId = user.Id,
                ConsumedAt = DateTime.UtcNow
            };
            _context.SharedTestConsumptions.Add(consumption);
            await _context.SaveChangesAsync(ct);
            return Ok(new { test.ID });
        }
    }
}
