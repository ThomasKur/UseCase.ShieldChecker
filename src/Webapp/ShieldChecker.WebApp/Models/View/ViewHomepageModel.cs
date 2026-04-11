using ShieldChecker.WebApp.Models.Db;

namespace ShieldChecker.WebApp.Models.View
{
    public class ViewHomepageModel
    {
        public SystemStatus Status { get; set; }

        // ── Trend data (last 30 days) ─────────────────────────────────────────
        public int TotalJobsLast30Days { get; set; }
        public int SuccessfulJobsLast30Days { get; set; }
        public int FailedJobsLast30Days { get; set; }
        public int UndeterminedJobsLast30Days { get; set; }

        /// <summary>Detection rate = successful / total jobs (0-100 %).</summary>
        public double DetectionRatePercent =>
            TotalJobsLast30Days == 0 ? 0
            : Math.Round(SuccessfulJobsLast30Days * 100.0 / TotalJobsLast30Days, 1);

        /// <summary>Trend points for the 30-day sparkline (date → count of completed jobs).</summary>
        public List<DailyJobCount> DailyTrend { get; set; } = new();
    }

    public record DailyJobCount(string Date, int Success, int Failed, int Total);
}
