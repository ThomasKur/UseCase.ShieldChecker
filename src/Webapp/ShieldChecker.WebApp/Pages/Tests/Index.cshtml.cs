using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShieldChecker.WebApp;
using ShieldChecker.WebApp.Models.Db;
using ShieldChecker.WebApp.Helper;
using System.Configuration;

namespace ShieldChecker.WebApp.Pages.Tests
{
    public class IndexModel : PageModel
    {
        private readonly ShieldChecker.WebApp.ShieldCheckerContext _context;
        private readonly IConfiguration _configuration;

        public IndexModel(ShieldChecker.WebApp.ShieldCheckerContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        public PaginatedList<TestDefinition> Test { get; set; }

        public string CurrentFilter { get; set; } = string.Empty;

        /// <summary>Maps TestDefinition.ID → whether the test's last completed job succeeded.</summary>
        public Dictionary<int, bool> TestCanBeShared { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetSchedule(int id)
        {
            var test = await _context.UseCaseTests.FindAsync(id);
            if (test == null)
            {
                return Page();
            }
            TestJob job = new TestJob
            {
                Status = JobStatus.Queued,
                UseCase = test,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Result = JobResult.Undetermined,
                SchedulerLog = ""

            };
            _context.TestJobs.Add(job);
            await _context.SaveChangesAsync();
            return RedirectToPage("./../Jobs/Index");
            
        }
        public async Task OnGetAsync(string currentFilter, string searchString, int? pageIndex)
        {
            if (searchString != null)
            {
                pageIndex = 1;
            }
            else
            {
                searchString = currentFilter;
            }
            CurrentFilter = searchString;
            IQueryable<TestDefinition> testsIQ = from s in _context.UseCaseTests
                                             select s;
            
            if (!String.IsNullOrWhiteSpace(searchString))
            {
                testsIQ = testsIQ.Where(t => t.Name.Contains(searchString) || t.Description.Contains(searchString));
            }
            var pageSize = _configuration.GetValue("PageSize", 25);
            Test = await PaginatedList<TestDefinition>.CreateAsync(testsIQ.AsNoTracking().Include(u => u.CreatedBy)
                    .Include(u => u.ModifiedBy), pageIndex ?? 1, pageSize);

            // Build TestCanBeShared: for each test on this page, find the latest completed job
            var testIds = Test.Select(t => t.ID).ToList();
            var latestJobResults = await _context.TestJobs
                .Where(j => testIds.Contains(j.UseCaseID) && j.Status == JobStatus.Completed)
                .GroupBy(j => j.UseCaseID)
                .Select(g => new
                {
                    UseCaseID = g.Key,
                    LatestResult = g.OrderByDescending(j => j.Modified).First().Result
                })
                .ToListAsync();

            TestCanBeShared = latestJobResults.ToDictionary(
                x => x.UseCaseID,
                x => x.LatestResult == JobResult.Success || x.LatestResult == JobResult.SuccessWithOtherDetection);
        }

        public async Task<IActionResult> OnPostShareAsync(int id)
        {
            var test = await _context.UseCaseTests.FindAsync(id);
            if (test == null)
                return NotFound();

            // Server-side re-validation: last completed job must be successful
            var latestCompletedJob = await _context.TestJobs
                .Where(j => j.UseCaseID == id && j.Status == JobStatus.Completed)
                .OrderByDescending(j => j.Modified)
                .FirstOrDefaultAsync();

            if (latestCompletedJob == null ||
                (latestCompletedJob.Result != JobResult.Success &&
                 latestCompletedJob.Result != JobResult.SuccessWithOtherDetection))
            {
                StatusMessage = "Error: Only tests with a successful last job run can be shared to the community.";
                return RedirectToPage();
            }

            var currentUser = UserInfo.EnsureUserInDb(User, _context);

            // Check if a draft or approved submission for this test already exists via SharedLibrarySourceId
            var test2 = await _context.UseCaseTests
                .Where(t => t.ID == id && t.SharedLibrarySourceId != null)
                .Select(t => new { t.SharedLibrarySourceId })
                .FirstOrDefaultAsync();

            SharedTestDefinition? existing = null;
            if (test2?.SharedLibrarySourceId != null)
                existing = await _context.SharedTestLibrary.FindAsync(test2.SharedLibrarySourceId.Value);

            if (existing != null && existing.Status == SharedTestStatus.Approved)
            {
                StatusMessage = "Error: An approved shared library entry already exists for this test.";
                return RedirectToPage();
            }

            var shared = new SharedTestDefinition
            {
                Name = test.Name,
                MitreTechnique = test.MitreTechnique,
                Description = test.Description,
                ExpectedAlertTitle = test.ExpectedAlertTitle,
                ScriptTest = test.ScriptTest,
                ScriptPrerequisites = test.ScriptPrerequisites,
                ScriptCleanup = test.ScriptCleanup,
                ElevationRequired = test.ElevationRequired,
                OperatingSystem = test.OperatingSystem,
                ExecutorSystemType = test.ExecutorSystemType,
                ExecutorUserType = test.ExecutorUserType,
                Status = SharedTestStatus.Draft,
                SubmittedBy = currentUser,
                SubmittedAt = DateTime.UtcNow
            };

            _context.SharedTestLibrary.Add(shared);
            await _context.SaveChangesAsync();

            // Link the source test to the shared entry
            test.SharedLibrarySourceId = shared.ID;
            await _context.SaveChangesAsync();

            StatusMessage = "Your test has been submitted to the Shared Library and is pending review.";
            return RedirectToPage();
        }
    }
}
