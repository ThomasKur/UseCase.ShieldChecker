using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShieldChecker.WebApp.Models.Db;
using ShieldChecker.WebApp.Helper;

namespace ShieldChecker.WebApp.Pages.SharedLibrary
{
    public class ApproveModel : PageModel
    {
        private readonly ShieldCheckerContext _context;
        private readonly IConfiguration _configuration;

        public ApproveModel(ShieldCheckerContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public List<SharedTestDefinition> DraftSubmissions { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!AuthorizationHelper.IsManagingTenantUser(User, _configuration))
                return Forbid();

            DraftSubmissions = await _context.SharedTestLibrary
                .Where(s => s.Status == SharedTestStatus.Draft)
                .Include(s => s.SubmittedBy)
                .OrderBy(s => s.SubmittedAt)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            if (!AuthorizationHelper.IsManagingTenantUser(User, _configuration))
                return Forbid();

            var entry = await _context.SharedTestLibrary.FindAsync(id);
            if (entry == null)
                return NotFound();

            var currentUser = UserInfo.EnsureUserInDb(User, _context);
            entry.Status = SharedTestStatus.Approved;
            entry.ApprovedBy = currentUser;
            entry.ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            StatusMessage = $"The submission '{entry.Name}' has been approved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            if (!AuthorizationHelper.IsManagingTenantUser(User, _configuration))
                return Forbid();

            var entry = await _context.SharedTestLibrary.FindAsync(id);
            if (entry == null)
                return NotFound();

            string name = entry.Name;

            // Unlink any TestDefinition that referenced this draft so the link is clean
            var linkedTests = await _context.UseCaseTests
                .Where(t => t.SharedLibrarySourceId == id)
                .ToListAsync();
            foreach (var t in linkedTests)
                t.SharedLibrarySourceId = null;

            _context.SharedTestLibrary.Remove(entry);
            await _context.SaveChangesAsync();

            StatusMessage = $"The submission '{name}' has been rejected and removed.";
            return RedirectToPage();
        }
    }
}
