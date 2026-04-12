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
        private readonly IMemoryCache _cache;
        private readonly AuditService _audit;
        private static readonly string AllJobsCacheKey = "jobs:all";

        public JobsController(ShieldCheckerContext context, IMemoryCache cache, AuditService audit)
        {
            _context = context;
            _cache = cache;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            if (!_cache.TryGetValue(AllJobsCacheKey, out List<TestJob>? jobs))
            {
                jobs = await _context.TestJobs
                    .Include(j => j.UseCase)
                    .AsNoTracking()
                    .OrderByDescending(j => j.Created)
                    .ToListAsync(ct);
                _cache.Set(AllJobsCacheKey, jobs, TimeSpan.FromSeconds(30));
            }
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
            _cache.Remove(AllJobsCacheKey);
            await _audit.LogAsync("TestJob", id, "Cancel", Request.Headers["X-User-Oid"].FirstOrDefault(), Request.Headers["X-User-Name"].FirstOrDefault(), Request.Headers["X-User-Upn"].FirstOrDefault(), null, ct);
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
            _cache.Remove(AllJobsCacheKey);
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
            _cache.Remove(AllJobsCacheKey);
            return Ok();
        }

        /// <summary>
        /// Bulk-cancel jobs. Accepts a list of job IDs and cancels all that are still queued.
        /// </summary>
        [HttpPost("bulk-cancel")]
        public async Task<IActionResult> BulkCancel([FromBody] BulkJobRequest req, CancellationToken ct)
        {
            if (req.Ids == null || req.Ids.Count == 0) return BadRequest("No job IDs provided.");
            var jobs = await _context.TestJobs
                .Where(j => req.Ids.Contains(j.ID) && j.Status == JobStatus.Queued)
                .ToListAsync(ct);
            foreach (var job in jobs)
            {
                job.Status = JobStatus.Canceled;
                job.Modified = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(ct);
            _cache.Remove(AllJobsCacheKey);
            return Ok(new { canceled = jobs.Count });
        }

        /// <summary>
        /// Bulk-rerun jobs. Creates new queued jobs for each supplied job ID.
        /// </summary>
        [HttpPost("bulk-rerun")]
        public async Task<IActionResult> BulkRerun([FromBody] BulkJobRequest req, CancellationToken ct)
        {
            if (req.Ids == null || req.Ids.Count == 0) return BadRequest("No job IDs provided.");
            var originals = await _context.TestJobs
                .Where(j => req.Ids.Contains(j.ID))
                .ToListAsync(ct);
            var newJobs = originals.Select(o => new TestJob
            {
                UseCaseID = o.UseCaseID,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Status = JobStatus.Queued,
                Result = JobResult.Undetermined
            }).ToList();
            _context.TestJobs.AddRange(newJobs);
            await _context.SaveChangesAsync(ct);
            _cache.Remove(AllJobsCacheKey);

            var oid = Request.Headers["X-User-Oid"].FirstOrDefault();
            var name = Request.Headers["X-User-Name"].FirstOrDefault();
            var upn = Request.Headers["X-User-Upn"].FirstOrDefault();
            foreach (var (original, newJob) in originals.Zip(newJobs))
            {
                await _audit.LogAsync("TestJob", newJob.ID, "BulkRerun", oid, name, upn, $"Rerun of job {original.ID}", ct);
            }

            return Ok(new { queued = newJobs.Count, ids = newJobs.Select(j => j.ID) });
        }
    }

    public sealed class JobReviewRequest
    {
        public string? ReviewResult { get; set; }
        public JobResult Result { get; set; }
    }

    public sealed class BulkJobRequest
    {
        public List<int> Ids { get; set; } = new();
    }
}
