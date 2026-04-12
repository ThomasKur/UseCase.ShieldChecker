using ShieldChecker.WebApp.Models.Db;
using ShieldChecker.WebApp.Models.View;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ShieldChecker.WebApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ShieldChecker.WebApp.ShieldCheckerContext _context;
        private readonly ILogger<IndexModel> _logger;

        public ViewHomepageModel HomepageModel { get; set; } = default!;

        public IndexModel(ILogger<IndexModel> logger, ShieldCheckerContext context)
        {
            _logger = logger;
            _context = context;
            HomepageModel = new ViewHomepageModel();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var Status = _context.SystemStatus.Where(s => s.ID == 1).First();
            if (Status.IsFirstRunCompleted == false)
            {
                return RedirectToPage("./FirstRun/Welcome");
            }

            HomepageModel.Status = Status;

            // ── Trend analysis (last 30 days) ─────────────────────────────────
            var since = DateTime.UtcNow.AddDays(-30).Date;
            var recentJobs = await _context.TestJobs
                .Where(j => j.Created >= since && j.Status == JobStatus.Completed)
                .Select(j => new { j.Created, j.Result })
                .AsNoTracking()
                .ToListAsync();

            HomepageModel.TotalJobsLast30Days = recentJobs.Count;
            HomepageModel.SuccessfulJobsLast30Days = recentJobs.Count(j =>
                j.Result == JobResult.Success || j.Result == JobResult.SuccessWithOtherDetection);
            HomepageModel.FailedJobsLast30Days = recentJobs.Count(j =>
                j.Result == JobResult.Failed);
            HomepageModel.UndeterminedJobsLast30Days = recentJobs.Count(j =>
                j.Result == JobResult.Failed);

            // Daily trend for sparkline chart
            for (var d = since; d < DateTime.UtcNow.Date; d = d.AddDays(1))
            {
                var dayJobs = recentJobs.Where(j => j.Created.Date == d).ToList();
                HomepageModel.DailyTrend.Add(new DailyJobCount(
                    d.ToString("yyyy-MM-dd"),
                    dayJobs.Count(j => j.Result == JobResult.Success || j.Result == JobResult.SuccessWithOtherDetection),
                    dayJobs.Count(j => j.Result == JobResult.Failed),
                    dayJobs.Count));
            }

            return Page();
        }
    }
}
