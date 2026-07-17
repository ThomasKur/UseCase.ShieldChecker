using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.HostServiceApi.Controllers
{
    /// <summary>
    /// Called by customer HostService agents to update a job's status, output and result.
    /// Requires the HostService.Access AppRole issued by ShieldChecker-HostServiceApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class JobUpdaterController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;
        private readonly ILogger<JobUpdaterController> _logger;

        public JobUpdaterController(ShieldCheckerContext context, ILogger<JobUpdaterController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Updates the status and output of the job currently assigned to the given worker.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateJob([FromQuery] string workername, [FromBody] JobUpdateRequest update, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(workername))
                return BadRequest("workername is required.");

            if (update == null)
                return BadRequest("Request body is required.");

            // Sanitise the workername before logging to prevent log injection
            var safeWorkerName = System.Text.RegularExpressions.Regex.Replace(workername, @"[\r\n\t]", "_");

            var job = await _context.TestJobs
                .Where(j => j.WorkerName == workername && j.Status == JobStatus.WaitingForMDE)
                .OrderByDescending(j => j.WorkerStart)
                .FirstOrDefaultAsync(ct);

            if (job == null)
            {
                _logger.LogWarning("No active job found for worker '{Worker}'.", safeWorkerName);
                return NotFound();
            }

            job.Status = update.Status.HasValue ? (JobStatus)update.Status.Value : JobStatus.WaitingForDetection;
            job.TestOutput = update.TestOutput;
            job.SchedulerLog = update.ExecutorOutput;
            job.WorkerEnd = DateTime.UtcNow;
            job.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            // Auto-retry jobs that failed due to Azure Spot VM eviction
            if (job.Status == JobStatus.AzureSpotEvicted)
            {
                var retryJob = new TestJob
                {
                    UseCaseID = job.UseCaseID,
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow,
                    Status = JobStatus.Queued,
                    Result = JobResult.Undetermined,
                    SchedulerLog = $"Auto-retried after AzureSpotEviction of job {job.ID}"
                };
                _context.TestJobs.Add(retryJob);
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("Auto-retried job {JobId} (was AzureSpotEvicted) as new job {NewJobId}.", job.ID, retryJob.ID);
            }

            _logger.LogInformation("Updated job {JobId} for worker '{Worker}' with status {Status}.", job.ID, safeWorkerName, job.Status);
            return Ok();
        }
    }

    public sealed class JobUpdateRequest
    {
        public int? Status { get; set; }
        public required string TestOutput { get; set; }
        public required string ExecutorOutput { get; set; }
    }
}
