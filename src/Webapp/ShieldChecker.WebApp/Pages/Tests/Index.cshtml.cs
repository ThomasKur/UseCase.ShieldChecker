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

        public async Task<IActionResult> OnGetSchedule(int id)
        {
            var test = await _context.UseCaseTests.FindAsync(id);
            if (test == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return new JsonResult(new { success = false, error = "Test not found" });
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

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return new JsonResult(new { success = true, jobId = job.ID });
            return RedirectToPage("./Index");
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

        }
    }
}
