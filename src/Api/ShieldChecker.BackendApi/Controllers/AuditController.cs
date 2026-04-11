using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;

namespace ShieldChecker.BackendApi.Controllers
{
    /// <summary>
    /// Read-only access to the audit log. Requires WebApp.Access AppRole.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;

        public AuditController(ShieldCheckerContext context)
        {
            _context = context;
        }

        /// <summary>Returns the most recent audit log entries (up to 500).</summary>
        [HttpGet]
        public async Task<IActionResult> GetRecent([FromQuery] int count = 100, CancellationToken ct = default)
        {
            count = Math.Clamp(count, 1, 500);
            var entries = await _context.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToListAsync(ct);
            return Ok(entries);
        }

        /// <summary>Returns audit log entries for a specific entity.</summary>
        [HttpGet("{entityType}/{entityId:int}")]
        public async Task<IActionResult> GetByEntity(string entityType, int entityId, CancellationToken ct)
        {
            var entries = await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync(ct);
            return Ok(entries);
        }
    }
}
