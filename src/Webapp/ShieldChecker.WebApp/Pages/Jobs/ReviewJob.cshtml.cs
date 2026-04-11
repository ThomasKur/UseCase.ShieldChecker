using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShieldChecker.WebApp;
using ShieldChecker.WebApp.Models.Db;

namespace ShieldChecker.WebApp.Pages.Jobs
{
    [Authorize(Policy = "RequireOperator")]
    public class ReviewModel : PageModel
    {
        private readonly ShieldChecker.WebApp.ShieldCheckerContext _context;

        public ReviewModel(ShieldChecker.WebApp.ShieldCheckerContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ViewReviewTestJob ReviewTestJob { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var testjob =  await _context.TestJobs.Include(u => u.UseCase).Select(t => new TestJob() { ID = t.ID, UseCaseID = t.UseCaseID, UseCase = t.UseCase, Modified = t.Modified, WorkerName = t.WorkerName, Status = t.Status, Result = t.Result }).FirstOrDefaultAsync(m => m.ID == id);
            if (testjob == null)
            {
                return NotFound();
            }

            ReviewTestJob = new ViewReviewTestJob();
            ReviewTestJob.ID = testjob.ID;
            ReviewTestJob.Modified = testjob.Modified;
            ReviewTestJob.Result = testjob.Result;
            ReviewTestJob.ReviewResult = testjob.ReviewResult;
            ReviewTestJob.WorkerRemoteIP = testjob.WorkerRemoteIP;
            ReviewTestJob.OperatingSystem = testjob.UseCase.OperatingSystem;


            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            var testjob = _context.TestJobs.Find(ReviewTestJob.ID);
            if (testjob == null)
            {
                return NotFound();
            }
            testjob.ReviewResult = ReviewTestJob.ReviewResult;
            testjob.Modified = DateTime.UtcNow;
            testjob.Status = JobStatus.ReviewDone;
            testjob.Result = ReviewTestJob.Result;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TestJobExists(testjob.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool TestJobExists(int id)
        {
            return _context.TestJobs.Any(e => e.ID == id);
        }
    }
}
