using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Controllers
{
    /// <summary>
    /// CRUD and scheduling for AutoSchedule entities. Called by the WebApp.
    /// Requires the WebApp.Access AppRole issued by ShieldChecker-BackendApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class AutoSchedulerController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;

        public AutoSchedulerController(ShieldCheckerContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var schedules = await _context.AutoSchedule
                .Include(s => s.TestDefinitions)
                .AsNoTracking()
                .ToListAsync(ct);
            return Ok(schedules);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var schedule = await _context.AutoSchedule
                .Include(s => s.TestDefinitions)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ID == id, ct);
            return schedule == null ? NotFound() : Ok(schedule);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AutoSchedule schedule, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _context.AutoSchedule.Add(schedule);
            await _context.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id = schedule.ID }, schedule);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AutoSchedule schedule, CancellationToken ct)
        {
            if (id != schedule.ID) return BadRequest();
            _context.Attach(schedule).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.AutoSchedule.Any(s => s.ID == id)) return NotFound();
                throw;
            }
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var schedule = await _context.AutoSchedule.FindAsync(new object[] { id }, ct);
            if (schedule == null) return NotFound();
            _context.AutoSchedule.Remove(schedule);
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
