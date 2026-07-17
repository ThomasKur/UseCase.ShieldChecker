using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShieldChecker.BackendApi.Services;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Tests
{
    public class JobTimeoutServiceTests : IDisposable
    {
        private readonly ShieldCheckerContext _context;
        private readonly JobTimeoutService _service;

        public JobTimeoutServiceTests()
        {
            var options = new DbContextOptionsBuilder<ShieldCheckerContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ShieldCheckerContext(options);
            SeedData();

            var scopeFactory = new TestScopeFactory(_context);
            _service = new JobTimeoutService(scopeFactory, NullLogger<JobTimeoutService>.Instance);
        }

        private void SeedData()
        {
            var user = new UserInfo("System", "system@test.local", Guid.Empty);
            _context.UserInfo.Add(user);
            _context.SaveChanges();

            _context.Settings.Add(new Settings
            {
                ID = 1,
                MaxWorkerCount = 2,
                JobTimeout = 60, // 60 minutes
                WorkerVMCpuCount = 2,
                WorkerVMMemoryMB = 4096,
                WorkerVMWindowsImage = "C:\\img\\win.vhdx",
                WorkerVMLinuxImage = "C:\\img\\linux.vhdx",
                DcVMCpuCount = 2,
                DcVMMemoryMB = 4096,
                DcVMImage = "C:\\img\\dc.vhdx",
                VMStoragePath = "C:\\vms",
                DomainFQDN = "test.local",
                DomainControllerName = "dc01",
                MDEWindowsOnboardingScript = "",
                MDELinuxOnboardingScript = ""
            });

            _context.UseCaseTests.Add(new TestDefinition
            {
                ID = 1, Name = "Test 1", ExpectedAlertTitle = "Alert",
                ScriptTest = "echo test",
                CreatedBy = new UserInfo("System", "system@test.local", Guid.Empty),
                ModifiedBy = new UserInfo("System", "system@test.local", Guid.Empty),
                Created = DateTime.UtcNow, Modified = DateTime.UtcNow
            });
            _context.SaveChanges();
        }

        [Fact]
        public async Task EnforceTimeouts_MarksOldJobsAsError()
        {
            _context.TestJobs.Add(new TestJob
            {
                UseCaseID = 1,
                Created = DateTime.UtcNow.AddHours(-2), // Way past the 60-min timeout
                Modified = DateTime.UtcNow.AddHours(-2),
                Status = JobStatus.WaitingForMDE,
                Result = JobResult.Undetermined
            });
            _context.SaveChanges();

            await _service.EnforceTimeoutsAsync(CancellationToken.None);

            var job = _context.TestJobs.First();
            Assert.Equal(JobStatus.Error, job.Status);
            Assert.Equal(JobResult.Undetermined, job.Result);
            Assert.Contains("timed out", job.SchedulerLog);
        }

        [Fact]
        public async Task EnforceTimeouts_DoesNotAffectRecentJobs()
        {
            _context.TestJobs.Add(new TestJob
            {
                UseCaseID = 1,
                Created = DateTime.UtcNow.AddMinutes(-5), // Within the 60-min timeout
                Modified = DateTime.UtcNow.AddMinutes(-5),
                Status = JobStatus.WaitingForMDE,
                Result = JobResult.Undetermined
            });
            _context.SaveChanges();

            await _service.EnforceTimeoutsAsync(CancellationToken.None);

            var job = _context.TestJobs.First();
            Assert.Equal(JobStatus.WaitingForMDE, job.Status);
        }

        [Fact]
        public async Task EnforceTimeouts_DoesNotAffectCompletedJobs()
        {
            _context.TestJobs.Add(new TestJob
            {
                UseCaseID = 1,
                Created = DateTime.UtcNow.AddHours(-2),
                Modified = DateTime.UtcNow.AddHours(-2),
                Status = JobStatus.Completed, // Already completed
                Result = JobResult.Success
            });
            _context.SaveChanges();

            await _service.EnforceTimeoutsAsync(CancellationToken.None);

            var job = _context.TestJobs.First();
            Assert.Equal(JobStatus.Completed, job.Status);
        }

        [Fact]
        public async Task EnforceTimeouts_HandlesMultipleStatuses()
        {
            var now = DateTime.UtcNow;
            _context.TestJobs.AddRange(
                new TestJob
                {
                    UseCaseID = 1, Created = now.AddHours(-2), Modified = now.AddHours(-2),
                    Status = JobStatus.Queued, Result = JobResult.Undetermined
                },
                new TestJob
                {
                    UseCaseID = 1, Created = now.AddHours(-2), Modified = now.AddHours(-2),
                    Status = JobStatus.WaitingForDetection, Result = JobResult.Undetermined
                }
            );
            _context.SaveChanges();

            await _service.EnforceTimeoutsAsync(CancellationToken.None);

            var jobs = _context.TestJobs.ToList();
            Assert.All(jobs, j => Assert.Equal(JobStatus.Error, j.Status));
        }

        [Fact]
        public async Task EnforceTimeouts_SkipsWhenTimeoutNotConfigured()
        {
            var settings = _context.Settings.First();
            settings.JobTimeout = 0;
            _context.SaveChanges();

            _context.TestJobs.Add(new TestJob
            {
                UseCaseID = 1,
                Created = DateTime.UtcNow.AddHours(-2),
                Modified = DateTime.UtcNow.AddHours(-2),
                Status = JobStatus.WaitingForMDE,
                Result = JobResult.Undetermined
            });
            _context.SaveChanges();

            await _service.EnforceTimeoutsAsync(CancellationToken.None);

            var job = _context.TestJobs.First();
            Assert.Equal(JobStatus.WaitingForMDE, job.Status);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        private class TestScopeFactory : IServiceScopeFactory
        {
            private readonly ShieldCheckerContext _context;
            public TestScopeFactory(ShieldCheckerContext context) => _context = context;
            public IServiceScope CreateScope() => new TestScope(_context);

            private class TestScope : IServiceScope
            {
                private readonly IServiceProvider _provider;
                public TestScope(ShieldCheckerContext context) =>
                    _provider = new TestServiceProvider(context);
                public IServiceProvider ServiceProvider => _provider;
                public void Dispose() { }
            }

            private class TestServiceProvider : IServiceProvider
            {
                private readonly ShieldCheckerContext _context;
                public TestServiceProvider(ShieldCheckerContext context) => _context = context;
                public object? GetService(Type serviceType)
                {
                    if (serviceType == typeof(ShieldCheckerContext))
                        return _context;
                    return null;
                }
            }
        }
    }
}
