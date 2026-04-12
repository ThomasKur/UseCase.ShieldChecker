using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShieldChecker.BackendApi.Controllers;
using ShieldChecker.BackendApi.Services;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;
using Microsoft.AspNetCore.Mvc;

namespace ShieldChecker.BackendApi.Tests
{
    public class JobsControllerTests : IDisposable
    {
        private readonly ShieldCheckerContext _context;
        private readonly IMemoryCache _cache;
        private readonly AuditService _audit;
        private readonly JobsController _controller;

        public JobsControllerTests()
        {
            var options = new DbContextOptionsBuilder<ShieldCheckerContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ShieldCheckerContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _audit = new AuditService(_context, NullLogger<AuditService>.Instance);
            _controller = new JobsController(_context, _cache, _audit);

            // Provide a minimal HttpContext so that Request.Headers is accessible
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };

            // Seed a test definition required by TestJob FK
            _context.UseCaseTests.Add(new TestDefinition
            {
                ID = 1,
                Name = "Test 1",
                ExpectedAlertTitle = "Alert",
                ScriptTest = "Write-Host Test",
                ScriptCleanup = "Write-Host Cleanup",
                ScriptPrerequisites = "Write-Host Pre",
                CreatedBy = new UserInfo("System", "system@test.local", Guid.Empty),
                ModifiedBy = new UserInfo("System", "system@test.local", Guid.Empty),
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            });
            _context.SaveChanges();
        }

        private TestJob AddJob(JobStatus status = JobStatus.Queued)
        {
            var job = new TestJob
            {
                UseCaseID = 1,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Status = status,
                Result = JobResult.Undetermined
            };
            _context.TestJobs.Add(job);
            _context.SaveChanges();
            return job;
        }

        [Fact]
        public async Task GetAll_ReturnsOkWithJobs()
        {
            AddJob();
            AddJob();

            var result = await _controller.GetAll(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var jobs = Assert.IsAssignableFrom<IEnumerable<TestJob>>(ok.Value);
            Assert.Equal(2, jobs.Count());
        }

        [Fact]
        public async Task GetById_ReturnsNotFound_WhenMissing()
        {
            var result = await _controller.GetById(9999, CancellationToken.None);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetById_ReturnsJob_WhenFound()
        {
            var job = AddJob();
            var result = await _controller.GetById(job.ID, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsType<TestJob>(ok.Value);
            Assert.Equal(job.ID, returned.ID);
        }

        [Fact]
        public async Task Cancel_SetsStatusCanceled()
        {
            var job = AddJob(JobStatus.Queued);
            await _controller.Cancel(job.ID, CancellationToken.None);
            var updated = await _context.TestJobs.FindAsync(job.ID);
            Assert.Equal(JobStatus.Canceled, updated!.Status);
        }

        [Fact]
        public async Task Cancel_ReturnsNotFound_ForMissingJob()
        {
            var result = await _controller.Cancel(9999, CancellationToken.None);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Rerun_CreatesNewQueuedJob()
        {
            var job = AddJob(JobStatus.Completed);
            await _controller.Rerun(job.ID, CancellationToken.None);
            var jobs = _context.TestJobs.ToList();
            Assert.Equal(2, jobs.Count);
            Assert.Contains(jobs, j => j.Status == JobStatus.Queued && j.ID != job.ID);
        }

        [Fact]
        public async Task BulkCancel_CancelsOnlyQueuedJobs()
        {
            var j1 = AddJob(JobStatus.Queued);
            var j2 = AddJob(JobStatus.Queued);
            var j3 = AddJob(JobStatus.Completed);

            var req = new BulkJobRequest { Ids = new List<int> { j1.ID, j2.ID, j3.ID } };
            var result = await _controller.BulkCancel(req, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(JobStatus.Canceled, _context.TestJobs.Find(j1.ID)!.Status);
            Assert.Equal(JobStatus.Canceled, _context.TestJobs.Find(j2.ID)!.Status);
            Assert.Equal(JobStatus.Completed, _context.TestJobs.Find(j3.ID)!.Status);
        }

        [Fact]
        public async Task BulkRerun_CreatesNewJobsForAll()
        {
            var j1 = AddJob(JobStatus.Completed);
            var j2 = AddJob(JobStatus.Error);

            var req = new BulkJobRequest { Ids = new List<int> { j1.ID, j2.ID } };
            var result = await _controller.BulkRerun(req, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(4, _context.TestJobs.Count());
        }

        public void Dispose()
        {
            _context.Dispose();
            _cache.Dispose();
        }
    }
}
