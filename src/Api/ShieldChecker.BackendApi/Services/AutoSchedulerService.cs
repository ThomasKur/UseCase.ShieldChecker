using Microsoft.EntityFrameworkCore;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Services
{
    /// <summary>
    /// Background service that evaluates AutoSchedule entries every 60 seconds and
    /// creates queued TestJob records for schedules whose NextExecution has passed.
    /// Uses a SchedulerMutex row to prevent concurrent schedule evaluation across replicas.
    /// </summary>
    public sealed class AutoSchedulerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoSchedulerService> _logger;

        /// <summary>Maximum age of a mutex before it is considered stale and can be reclaimed.</summary>
        private static readonly TimeSpan MutexTimeout = TimeSpan.FromMinutes(5);

        public AutoSchedulerService(IServiceScopeFactory scopeFactory, ILogger<AutoSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoSchedulerService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EvaluateSchedulesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AutoSchedulerService encountered an error during schedule evaluation.");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }

            _logger.LogInformation("AutoSchedulerService stopped.");
        }

        internal async Task EvaluateSchedulesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ShieldCheckerContext>();

            // Try to acquire the scheduler mutex
            if (!await TryAcquireMutexAsync(context, ct))
            {
                _logger.LogTrace("AutoScheduler mutex is held by another instance. Skipping this cycle.");
                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                var dueSchedules = await context.AutoSchedule
                    .Include(s => s.TestDefinitions)
                    .Where(s => s.Enabled && s.NextExecution <= now)
                    .ToListAsync(ct);

                if (dueSchedules.Count == 0)
                {
                    _logger.LogTrace("No due schedules found.");
                    return;
                }

                _logger.LogInformation("Found {Count} due schedule(s) to process.", dueSchedules.Count);

                foreach (var schedule in dueSchedules)
                {
                    await ProcessScheduleAsync(context, schedule, now, ct);
                }
            }
            finally
            {
                await ReleaseMutexAsync(context, ct);
            }
        }

        private async Task ProcessScheduleAsync(ShieldCheckerContext context, AutoSchedule schedule, DateTime now, CancellationToken ct)
        {
            _logger.LogInformation("Processing schedule '{Name}' (ID: {Id}).", schedule.Name, schedule.ID);

            // Determine eligible test definitions
            var candidates = schedule.TestDefinitions
                .Where(t => t.Enabled == true)
                .ToList();

            // If no specific tests are selected, select all enabled tests matching filters
            if (candidates.Count == 0)
            {
                var query = context.UseCaseTests.Where(t => t.Enabled == true);

                if (schedule.FilterOperatingSystem.HasValue)
                {
                    query = query.Where(t => t.OperatingSystem == schedule.FilterOperatingSystem.Value);
                }

                candidates = await query.ToListAsync(ct);
            }
            else if (schedule.FilterOperatingSystem.HasValue)
            {
                candidates = candidates
                    .Where(t => t.OperatingSystem == schedule.FilterOperatingSystem.Value)
                    .ToList();
            }

            // Apply execution filter
            if (schedule.FilterExecution.HasValue && schedule.FilterExecution.Value != FilterExecution.None)
            {
                candidates = await ApplyExecutionFilterAsync(context, candidates, schedule.FilterExecution.Value, now, ct);
            }

            // Apply random count filter
            if (schedule.FilterRandomCount.HasValue && schedule.FilterRandomCount.Value > 0 && candidates.Count > schedule.FilterRandomCount.Value)
            {
                candidates = candidates
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(schedule.FilterRandomCount.Value)
                    .ToList();
            }

            // Create queued jobs
            int jobsCreated = 0;
            foreach (var test in candidates)
            {
                var job = new TestJob
                {
                    UseCaseID = test.ID,
                    Created = now,
                    Modified = now,
                    Status = JobStatus.Queued,
                    Result = JobResult.Undetermined,
                    SchedulerLog = $"Auto-scheduled by '{schedule.Name}' (ID: {schedule.ID})"
                };
                context.TestJobs.Add(job);
                jobsCreated++;
            }

            // Advance NextExecution
            schedule.NextExecution = CalculateNextExecution(schedule.NextExecution, schedule.Type);

            await context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Schedule '{Name}' (ID: {Id}): created {Count} job(s). Next execution: {Next}.",
                schedule.Name, schedule.ID, jobsCreated, schedule.NextExecution);
        }

        private static async Task<List<TestDefinition>> ApplyExecutionFilterAsync(
            ShieldCheckerContext context,
            List<TestDefinition> candidates,
            FilterExecution filter,
            DateTime now,
            CancellationToken ct)
        {
            var cutoff = filter switch
            {
                FilterExecution.OnlyWhenNoSuccessJobInPastWeek => now.AddDays(-7),
                FilterExecution.OnlyWhenNoSuccessJobInPastMonth => now.AddDays(-30),
                FilterExecution.OnlyWhenNoJobInPastWeek => now.AddDays(-7),
                FilterExecution.OnlyWhenNoJobInPastMonth => now.AddDays(-30),
                _ => now
            };

            bool successOnly = filter == FilterExecution.OnlyWhenNoSuccessJobInPastWeek
                            || filter == FilterExecution.OnlyWhenNoSuccessJobInPastMonth;

            var candidateIds = candidates.Select(c => c.ID).ToList();

            // Find test IDs that already have matching jobs in the time window
            IQueryable<TestJob> jobQuery = context.TestJobs
                .Where(j => candidateIds.Contains(j.UseCaseID) && j.Created >= cutoff);

            if (successOnly)
            {
                jobQuery = jobQuery.Where(j => j.Result == JobResult.Success);
            }

            var testIdsWithJobs = await jobQuery
                .Select(j => j.UseCaseID)
                .Distinct()
                .ToListAsync(ct);

            return candidates.Where(c => !testIdsWithJobs.Contains(c.ID)).ToList();
        }

        internal static DateTime CalculateNextExecution(DateTime current, AutoScheduleType type)
        {
            return type switch
            {
                AutoScheduleType.Daily => current.AddDays(1),
                AutoScheduleType.Weekly => current.AddDays(7),
                AutoScheduleType.Monthly => current.AddMonths(1),
                AutoScheduleType.Quarterly => current.AddMonths(3),
                _ => current.AddDays(1)
            };
        }

        private async Task<bool> TryAcquireMutexAsync(ShieldCheckerContext context, CancellationToken ct)
        {
            var mutex = await context.SchedulerMutex
                .FirstOrDefaultAsync(m => m.SchedulerType == SchedulerType.Worker, ct);

            var now = DateTime.UtcNow;
            string owner = Environment.MachineName;

            if (mutex == null)
            {
                context.SchedulerMutex.Add(new SchedulerMutex
                {
                    Owner = owner,
                    SchedulerType = SchedulerType.Worker,
                    Start = now
                });
                try
                {
                    await context.SaveChangesAsync(ct);
                    return true;
                }
                catch (DbUpdateException)
                {
                    // Another instance beat us to it
                    return false;
                }
            }

            // If the mutex is stale (owner crashed), reclaim it
            if (now - mutex.Start > MutexTimeout)
            {
                _logger.LogWarning("Reclaiming stale AutoScheduler mutex from '{Owner}' (started {Start}).",
                    mutex.Owner, mutex.Start);
                mutex.Owner = owner;
                mutex.Start = now;
                try
                {
                    await context.SaveChangesAsync(ct);
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false;
                }
            }

            // Mutex is held by another instance and not stale
            return false;
        }

        private async Task ReleaseMutexAsync(ShieldCheckerContext context, CancellationToken ct)
        {
            var mutex = await context.SchedulerMutex
                .FirstOrDefaultAsync(m => m.SchedulerType == SchedulerType.Worker, ct);

            if (mutex != null)
            {
                context.SchedulerMutex.Remove(mutex);
                try
                {
                    await context.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning(ex, "Failed to release AutoScheduler mutex.");
                }
            }
        }
    }
}
