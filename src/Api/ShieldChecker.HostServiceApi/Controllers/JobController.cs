using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.HostServiceApi.Controllers
{
    /// <summary>
    /// Called by customer HostService agents to fetch the next pending job.
    /// Requires the HostService.Access AppRole issued by ShieldChecker-HostServiceApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class JobController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<JobController> _logger;

        public JobController(ShieldCheckerContext context, IConfiguration configuration, ILogger<JobController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Returns the next queued job for the calling worker (identified by workername query param).
        /// Returns 404 when no job is available, 400 when DC is not ready.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetJob([FromQuery] string workername, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(workername))
                return BadRequest("workername is required.");

            // Check that the domain controller is ready before processing jobs
            var systemStatus = await _context.SystemStatus.FirstOrDefaultAsync(ct);
            if (systemStatus == null || systemStatus.DomainControllerStatus != DomainControllerStatus.Initialized)
            {
                _logger.LogInformation("Domain Controller is not yet ready, skip processing jobs for worker '{Worker}'.", workername);
                return BadRequest("Domain Controller is not yet ready, skip processing jobs");
            }

            // Find the oldest queued job for this worker's OS type
            var settings = await _context.Settings.FirstOrDefaultAsync(ct);
            if (settings == null)
                return BadRequest("Settings not initialised.");

            var job = await _context.TestJobs
                .Include(j => j.UseCase)
                .Where(j => j.Status == JobStatus.Queued && j.UseCase.ExecutorSystemType == ExecutorSystemType.Worker)
                .OrderBy(j => j.Created)
                .FirstOrDefaultAsync(ct);

            if (job == null)
                return NotFound();

            // Assign the job to this worker
            job.Status = JobStatus.WaitingForMDE;
            job.WorkerName = workername;
            job.WorkerStart = DateTime.UtcNow;
            job.Modified = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            var response = new
            {
                job.UseCase.ID,
                job.UseCase.Name,
                job.UseCase.ScriptTest,
                job.UseCase.ScriptPrerequisites,
                job.UseCase.ScriptCleanup,
                job.UseCase.ElevationRequired,
                OperatingSystem = (int)job.UseCase.OperatingSystem,
                ExecutorSystemType = (int)job.UseCase.ExecutorSystemType,
                ExecutorUserType = (int)job.UseCase.ExecutorUserType,
                Username = (string?)null,
                Password = (string?)null,
                Domain = (string?)null
            };

            _logger.LogInformation("Assigned job {JobId} to worker '{Worker}'.", job.ID, workername);
            return Ok(response);
        }
    }
}
