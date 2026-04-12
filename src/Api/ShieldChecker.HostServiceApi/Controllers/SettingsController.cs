using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;

namespace ShieldChecker.HostServiceApi.Controllers
{
    /// <summary>
    /// Exposes the Hyper-V VM configuration settings to authenticated HostService agents.
    /// Requires the HostService.Access AppRole issued by ShieldChecker-HostServiceApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;

        public SettingsController(ShieldCheckerContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns the Hyper-V VM configuration that HostService agents use when
        /// creating worker and domain-controller VMs.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var settings = await _context.Settings.FirstOrDefaultAsync(ct);
            if (settings == null)
                return NotFound("Settings not initialised.");

            var result = new
            {
                settings.WorkerVMCpuCount,
                settings.WorkerVMMemoryMB,
                settings.WorkerVMWindowsImage,
                settings.WorkerVMLinuxImage,
                settings.DcVMCpuCount,
                settings.DcVMMemoryMB,
                settings.DcVMImage,
                settings.VMStoragePath
            };

            return Ok(result);
        }
    }
}
