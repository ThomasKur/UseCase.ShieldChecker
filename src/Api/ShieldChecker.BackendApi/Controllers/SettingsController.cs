using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.BackendApi.Services;
using ShieldChecker.DataAccess;

namespace ShieldChecker.BackendApi.Controllers
{
    /// <summary>
    /// Read and update application settings. Called by the WebApp under its managed identity.
    /// Requires the WebApp.Access AppRole issued by ShieldChecker-BackendApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;
        private readonly AuditService _audit;

        public SettingsController(ShieldCheckerContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var settings = await _context.Settings.FirstOrDefaultAsync(s => s.ID == 1, ct);
            return settings == null ? NotFound() : Ok(settings);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] DataAccess.Models.Settings settings, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _context.Attach(settings).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Settings.Any(s => s.ID == settings.ID)) return NotFound();
                throw;
            }
            await _audit.LogAsync("Settings", settings.ID, "Update", Request.Headers["X-User-Oid"].FirstOrDefault(), Request.Headers["X-User-Name"].FirstOrDefault(), Request.Headers["X-User-Upn"].FirstOrDefault(), null, ct);
            return Ok(settings);
        }

        [HttpGet("systemstatus")]
        public async Task<IActionResult> GetSystemStatus(CancellationToken ct)
        {
            var status = await _context.SystemStatus.FirstOrDefaultAsync(ct);
            return status == null ? NotFound() : Ok(status);
        }
    }
}
