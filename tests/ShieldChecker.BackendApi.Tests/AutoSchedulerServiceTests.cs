using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShieldChecker.BackendApi.Services;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Tests
{
    public class AutoSchedulerServiceTests : IDisposable
    {
        private readonly ShieldCheckerContext _context;
        private readonly ServiceProvider _serviceProvider;
        private readonly AutoSchedulerService _service;

        public AutoSchedulerServiceTests()
        {
            var options = new DbContextOptionsBuilder<ShieldCheckerContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var services = new ServiceCollection();
            services.AddDbContext<ShieldCheckerContext>(o =>
                o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            _serviceProvider = services.BuildServiceProvider();

            _context = new ShieldCheckerContext(options);
            SeedData();

            // Create a scope factory that returns our known context
            var scopeFactory = new TestScopeFactory(_context);
            _service = new AutoSchedulerService(scopeFactory, NullLogger<AutoSchedulerService>.Instance);
        }

        private void SeedData()
        {
            var user = new UserInfo("System", "system@test.local", Guid.Empty);
            _context.UserInfo.Add(user);
            _context.SaveChanges();

            _context.UseCaseTests.AddRange(
                new TestDefinition
                {
                    ID = 1, Name = "Test Win 1", Enabled = true,
                    OperatingSystem = DataAccess.Models.OperatingSystem.Windows,
                    ExpectedAlertTitle = "Alert 1", ScriptTest = "echo test",
                    CreatedBy = user, ModifiedBy = user,
                    Created = DateTime.UtcNow, Modified = DateTime.UtcNow
                },
                new TestDefinition
                {
                    ID = 2, Name = "Test Linux 1", Enabled = true,
                    OperatingSystem = DataAccess.Models.OperatingSystem.Linux,
                    ExpectedAlertTitle = "Alert 2", ScriptTest = "echo test",
                    CreatedBy = user, ModifiedBy = user,
                    Created = DateTime.UtcNow, Modified = DateTime.UtcNow
                },
                new TestDefinition
                {
                    ID = 3, Name = "Disabled Test", Enabled = false,
                    OperatingSystem = DataAccess.Models.OperatingSystem.Windows,
                    ExpectedAlertTitle = "Alert 3", ScriptTest = "echo test",
                    CreatedBy = user, ModifiedBy = user,
                    Created = DateTime.UtcNow, Modified = DateTime.UtcNow
                }
            );
            _context.SaveChanges();
        }

        [Fact]
        public async Task EvaluateSchedules_CreatesJobs_ForDueSchedule()
        {
            var schedule = new AutoSchedule
            {
                Name = "Daily Schedule",
                Enabled = true,
                NextExecution = DateTime.UtcNow.AddMinutes(-5),
                Type = AutoScheduleType.Daily,
                TestDefinitions = new List<TestDefinition>
                {
                    _context.UseCaseTests.Find(1)!,
                    _context.UseCaseTests.Find(2)!
                }
            };
            _context.AutoSchedule.Add(schedule);
            _context.SaveChanges();

            await _service.EvaluateSchedulesAsync(CancellationToken.None);

            var jobs = _context.TestJobs.ToList();
            Assert.Equal(2, jobs.Count);
            Assert.All(jobs, j => Assert.Equal(JobStatus.Queued, j.Status));
            Assert.All(jobs, j => Assert.Equal(JobResult.Undetermined, j.Result));
        }

        [Fact]
        public async Task EvaluateSchedules_SkipsDisabledSchedule()
        {
            _context.AutoSchedule.Add(new AutoSchedule
            {
                Name = "Disabled Schedule",
                Enabled = false,
                NextExecution = DateTime.UtcNow.AddMinutes(-5),
                Type = AutoScheduleType.Daily,
                TestDefinitions = new List<TestDefinition> { _context.UseCaseTests.Find(1)! }
            });
            _context.SaveChanges();

            await _service.EvaluateSchedulesAsync(CancellationToken.None);

            Assert.Empty(_context.TestJobs.ToList());
        }

        [Fact]
        public async Task EvaluateSchedules_SkipsFutureSchedule()
        {
            _context.AutoSchedule.Add(new AutoSchedule
            {
                Name = "Future Schedule",
                Enabled = true,
                NextExecution = DateTime.UtcNow.AddHours(1),
                Type = AutoScheduleType.Daily,
                TestDefinitions = new List<TestDefinition> { _context.UseCaseTests.Find(1)! }
            });
            _context.SaveChanges();

            await _service.EvaluateSchedulesAsync(CancellationToken.None);

            Assert.Empty(_context.TestJobs.ToList());
        }

        [Fact]
        public async Task EvaluateSchedules_AdvancesNextExecution()
        {
            var originalNext = DateTime.UtcNow.AddMinutes(-5);
            var schedule = new AutoSchedule
            {
                Name = "Weekly Schedule",
                Enabled = true,
                NextExecution = originalNext,
                Type = AutoScheduleType.Weekly,
                TestDefinitions = new List<TestDefinition> { _context.UseCaseTests.Find(1)! }
            };
            _context.AutoSchedule.Add(schedule);
            _context.SaveChanges();

            await _service.EvaluateSchedulesAsync(CancellationToken.None);

            var updated = _context.AutoSchedule.Find(schedule.ID)!;
            Assert.Equal(originalNext.AddDays(7), updated.NextExecution, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task EvaluateSchedules_FiltersOperatingSystem()
        {
            _context.AutoSchedule.Add(new AutoSchedule
            {
                Name = "Linux Only",
                Enabled = true,
                NextExecution = DateTime.UtcNow.AddMinutes(-5),
                Type = AutoScheduleType.Daily,
                FilterOperatingSystem = DataAccess.Models.OperatingSystem.Linux,
                TestDefinitions = new List<TestDefinition>
                {
                    _context.UseCaseTests.Find(1)!, // Windows
                    _context.UseCaseTests.Find(2)!  // Linux
                }
            });
            _context.SaveChanges();

            await _service.EvaluateSchedulesAsync(CancellationToken.None);

            var jobs = _context.TestJobs.ToList();
            Assert.Single(jobs);
            Assert.Equal(2, jobs[0].UseCaseID); // Only Linux test
        }

        [Fact]
        public async Task EvaluateSchedules_AppliesRandomCountFilter()
        {
            _context.AutoSchedule.Add(new AutoSchedule
            {
                Name = "Random 1",
                Enabled = true,
                NextExecution = DateTime.UtcNow.AddMinutes(-5),
                Type = AutoScheduleType.Daily,
                FilterRandomCount = 1,
                TestDefinitions = new List<TestDefinition>
                {
                    _context.UseCaseTests.Find(1)!,
                    _context.UseCaseTests.Find(2)!
                }
            });
            _context.SaveChanges();

            await _service.EvaluateSchedulesAsync(CancellationToken.None);

            var jobs = _context.TestJobs.ToList();
            Assert.Single(jobs);
        }

        [Fact]
        public async Task EvaluateSchedules_SkipsDisabledTests()
        {
            _context.AutoSchedule.Add(new AutoSchedule
            {
                Name = "With Disabled",
                Enabled = true,
                NextExecution = DateTime.UtcNow.AddMinutes(-5),
                Type = AutoScheduleType.Daily,
                TestDefinitions = new List<TestDefinition>
                {
                    _context.UseCaseTests.Find(1)!,
                    _context.UseCaseTests.Find(3)! // Disabled test
                }
            });
            _context.SaveChanges();

            await _service.EvaluateSchedulesAsync(CancellationToken.None);

            var jobs = _context.TestJobs.ToList();
            Assert.Single(jobs);
            Assert.Equal(1, jobs[0].UseCaseID);
        }

        [Theory]
        [InlineData(AutoScheduleType.Daily, 1)]
        [InlineData(AutoScheduleType.Weekly, 7)]
        public void CalculateNextExecution_ReturnsCorrectDate(AutoScheduleType type, int expectedDays)
        {
            var current = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var next = AutoSchedulerService.CalculateNextExecution(current, type);
            Assert.Equal(current.AddDays(expectedDays), next);
        }

        [Fact]
        public void CalculateNextExecution_Monthly_AddsOneMonth()
        {
            var current = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
            var next = AutoSchedulerService.CalculateNextExecution(current, AutoScheduleType.Monthly);
            Assert.Equal(new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), next);
        }

        [Fact]
        public void CalculateNextExecution_Quarterly_AddsThreeMonths()
        {
            var current = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
            var next = AutoSchedulerService.CalculateNextExecution(current, AutoScheduleType.Quarterly);
            Assert.Equal(new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc), next);
        }

        public void Dispose()
        {
            _context.Dispose();
            _serviceProvider.Dispose();
        }

        /// <summary>
        /// Test helper that creates scopes returning the same DbContext instance.
        /// </summary>
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
