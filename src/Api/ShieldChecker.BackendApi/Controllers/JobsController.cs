using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Controllers
{
    /// <summary>
    /// Read and update test jobs. Called by the WebApp under its managed identity.
    /// Requires the WebApp.Access AppRole issued by ShieldChecker-BackendApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;

        public JobsController(ShieldCheckerContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var jobs = await _context.TestJobs
                .Include(j => j.UseCase)
                .AsNoTracking()
                .OrderByDescending(j => j.Created)
                .ToListAsync(ct);
            return Ok(jobs);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var job = await _context.TestJobs
                .Include(j => j.UseCase)
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.ID == id, ct);
            return job == null ? NotFound() : Ok(job);
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id, CancellationToken ct)
        {
            var job = await _context.TestJobs.FindAsync(new object[] { id }, ct);
            if (job == null) return NotFound();
            job.Status = JobStatus.Canceled;
            job.Modified = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return Ok();
        }

        [HttpPost("{id:int}/rerun")]
        public async Task<IActionResult> Rerun(int id, CancellationToken ct)
        {
            var originalJob = await _context.TestJobs
                .Include(j => j.UseCase)
                .FirstOrDefaultAsync(j => j.ID == id, ct);
            if (originalJob == null) return NotFound();

            var newJob = new TestJob
            {
                UseCaseID = originalJob.UseCaseID,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Status = JobStatus.Queued,
                Result = JobResult.Undetermined
            };
            _context.TestJobs.Add(newJob);
            await _context.SaveChangesAsync(ct);
            return Ok(new { newJob.ID });
        }

        [HttpPut("{id:int}/review")]
        public async Task<IActionResult> Review(int id, [FromBody] JobReviewRequest req, CancellationToken ct)
        {
            var job = await _context.TestJobs.FindAsync(new object[] { id }, ct);
            if (job == null) return NotFound();
            job.ReviewResult = req.ReviewResult;
            job.Result = req.Result;
            job.Status = JobStatus.ReviewDone;
            job.Modified = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return Ok();
        }
    }

    public sealed class JobReviewRequest
    {
        public string? ReviewResult { get; set; }
        public JobResult Result { get; set; }
    }
}
