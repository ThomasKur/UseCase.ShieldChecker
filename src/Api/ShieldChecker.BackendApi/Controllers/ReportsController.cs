using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;
using System.Text;

namespace ShieldChecker.BackendApi.Controllers
{
    /// <summary>
    /// Provides report export endpoints for test job results.
    /// Requires the WebApp.Access AppRole issued by ShieldChecker-BackendApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;

        public ReportsController(ShieldCheckerContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Exports all completed job results as a UTF-8 CSV file.
        /// Optional query parameters: <c>from</c> and <c>to</c> (ISO-8601 dates).
        /// </summary>
        [HttpGet("jobs.csv")]
        public async Task<IActionResult> ExportJobsCsv(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            CancellationToken ct)
        {
            var query = _context.TestJobs
                .Include(j => j.UseCase)
                .AsNoTracking()
                .Where(j => j.Status == JobStatus.Completed || j.Status == JobStatus.ReviewDone);

            if (from.HasValue) query = query.Where(j => j.Created >= from.Value);
            if (to.HasValue) query = query.Where(j => j.Created <= to.Value);

            var jobs = await query.OrderByDescending(j => j.Created).ToListAsync(ct);

            var sb = new StringBuilder();
            sb.AppendLine("ID,TestName,Status,Result,ReviewResult,WorkerName,Created,WorkerStart,WorkerEnd");

            foreach (var job in jobs)
            {
                sb.AppendLine(string.Join(",",
                    job.ID,
                    CsvEscape(job.UseCase?.Name),
                    job.Status,
                    job.Result,
                    CsvEscape(job.ReviewResult),
                    CsvEscape(job.WorkerName),
                    job.Created.ToString("o"),
                    job.WorkerStart?.ToString("o"),
                    job.WorkerEnd?.ToString("o")));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"shieldchecker-jobs-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private static string CsvEscape(string? value)
        {
            if (value == null) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
