using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ShieldChecker.BackendApi.Controllers;
using ShieldChecker.BackendApi.Services;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace ShieldChecker.BackendApi.Tests
{
    public class TestsControllerTests : IDisposable
    {
        private readonly ShieldCheckerContext _context;
        private readonly IMemoryCache _cache;
        private readonly AuditService _audit;
        private readonly TestsController _controller;

        public TestsControllerTests()
        {
            var options = new DbContextOptionsBuilder<ShieldCheckerContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ShieldCheckerContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _audit = new AuditService(_context, NullLogger<AuditService>.Instance);
            _controller = new TestsController(_context, NullLogger<TestsController>.Instance, _cache, _audit);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-User-Oid"] = Guid.Empty.ToString();
            httpContext.Request.Headers["X-User-Name"] = "Test User";
            httpContext.Request.Headers["X-User-Upn"] = "test@test.local";

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var user = new UserInfo("System", "system@test.local", Guid.Empty);
            _context.UserInfo.Add(user);
            _context.SaveChanges();

            _context.UseCaseTests.Add(new TestDefinition
            {
                ID = 1,
                Name = "Test 1",
                ExpectedAlertTitle = "Alert",
                ScriptTest = "Write-Host Test",
                ScriptCleanup = "Write-Host Cleanup",
                ScriptPrerequisites = "Write-Host Pre",
                CreatedBy = user,
                ModifiedBy = user,
                Enabled = true,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            });
            _context.UseCaseTests.Add(new TestDefinition
            {
                ID = 2,
                Name = "Test 2",
                ExpectedAlertTitle = "Alert",
                ScriptTest = "Write-Host Test",
                ScriptCleanup = "Write-Host Cleanup",
                ScriptPrerequisites = "Write-Host Pre",
                CreatedBy = user,
                ModifiedBy = user,
                Enabled = true,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            });
            _context.SaveChanges();
        }

        [Fact]
        public async Task BulkQueue_InvalidatesJobsAllCache()
        {
            // Pre-populate the jobs:all cache
            _cache.Set("jobs:all", new List<TestJob>(), TimeSpan.FromMinutes(5));

            var req = new BulkTestQueueRequest { Ids = new List<int> { 1, 2 } };
            await _controller.BulkQueue(req, CancellationToken.None);

            // Cache should have been invalidated
            Assert.False(_cache.TryGetValue("jobs:all", out _));
        }

        [Fact]
        public async Task BulkQueue_CreatesJobsForEnabledTests()
        {
            var req = new BulkTestQueueRequest { Ids = new List<int> { 1, 2 } };
            var result = await _controller.BulkQueue(req, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(2, _context.TestJobs.Count());
        }

        [Fact]
        public async Task BulkQueue_WritesAuditLogEntry()
        {
            var req = new BulkTestQueueRequest { Ids = new List<int> { 1, 2 } };

            var result = await _controller.BulkQueue(req, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);

            var auditEntry = _context.AuditLogs
                .FirstOrDefault(a => a.Action == "BulkQueue" && a.EntityType == "TestJob");
            Assert.NotNull(auditEntry);
            Assert.Null(auditEntry.EntityId);
            Assert.Equal(Guid.Empty.ToString(), auditEntry.ActorOid);
            Assert.Equal("Test User", auditEntry.ActorName);
            Assert.Equal("test@test.local", auditEntry.ActorUpn);
            Assert.Contains("Queued 2 job(s)", auditEntry.Details);
        }

        [Fact]
        public async Task BulkQueue_ReturnsBadRequest_WhenNoIds()
        {
            var req = new BulkTestQueueRequest { Ids = new List<int>() };
            var result = await _controller.BulkQueue(req, CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(result);

            Assert.Empty(_context.AuditLogs.Where(a => a.Action == "BulkQueue"));
        }

        public void Dispose()
        {
            _context.Dispose();
            _cache.Dispose();
        }
    }
}
