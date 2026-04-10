using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShieldChecker.WebApp.Models.Db;
using ShieldChecker.WebApp.Helper;

namespace ShieldChecker.WebApp.Pages.SharedLibrary
{
    public class IndexModel : PageModel
    {
        private readonly ShieldCheckerContext _context;
        private readonly IConfiguration _configuration;

        public IndexModel(ShieldCheckerContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public List<SharedTestDefinition> SharedTests { get; set; } = new();

        /// <summary>Counts of consumptions per SharedTestDefinition.ID.</summary>
        public Dictionary<int, int> ConsumptionCounts { get; set; } = new();

        public bool IsAdmin { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? MitreTechniqueFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? OsFilter { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            IsAdmin = AuthorizationHelper.IsManagingTenantUser(User, _configuration);

            var query = _context.SharedTestLibrary
                .Include(s => s.SubmittedBy)
                .Include(s => s.ApprovedBy)
                .AsQueryable();

            if (!IsAdmin)
                query = query.Where(s => s.Status == SharedTestStatus.Approved);

            if (!string.IsNullOrWhiteSpace(SearchString))
                query = query.Where(s => s.Name.Contains(SearchString) || s.Description.Contains(SearchString));

            if (!string.IsNullOrWhiteSpace(MitreTechniqueFilter))
                query = query.Where(s => s.MitreTechnique.Contains(MitreTechniqueFilter));

            if (!string.IsNullOrWhiteSpace(OsFilter) && Enum.TryParse<ShieldChecker.WebApp.Models.Db.OperatingSystem>(OsFilter, out var os))
                query = query.Where(s => s.OperatingSystem == os);

            SharedTests = await query.OrderBy(s => s.Name).ToListAsync();

            var ids = SharedTests.Select(s => s.ID).ToList();
            ConsumptionCounts = await _context.SharedTestConsumptions
                .Where(c => ids.Contains(c.SharedTestDefinitionId))
                .GroupBy(c => c.SharedTestDefinitionId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count);
        }

        public async Task<IActionResult> OnPostAddToMyTestsAsync(int id)
        {
            var shared = await _context.SharedTestLibrary.FindAsync(id);
            if (shared == null || shared.Status != SharedTestStatus.Approved)
                return NotFound();

            var currentUser = UserInfo.EnsureUserInDb(User, _context);

            // Check if this user already has a test linked to this shared entry
            var existing = await _context.UseCaseTests
                .FirstOrDefaultAsync(t => t.SharedLibrarySourceId == id
                    && _context.UserInfo.Any(u => u.Id == currentUser.Id));

            if (existing == null)
            {
                var newTest = new TestDefinition
                {
                    Name = shared.Name,
                    MitreTechnique = shared.MitreTechnique,
                    Description = shared.Description,
                    ExpectedAlertTitle = shared.ExpectedAlertTitle,
                    ScriptTest = shared.ScriptTest,
                    ScriptPrerequisites = shared.ScriptPrerequisites,
                    ScriptCleanup = shared.ScriptCleanup,
                    ElevationRequired = shared.ElevationRequired,
                    OperatingSystem = shared.OperatingSystem,
                    ExecutorSystemType = shared.ExecutorSystemType,
                    ExecutorUserType = shared.ExecutorUserType,
                    Enabled = false,
                    ReadOnly = false,
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow,
                    CreatedBy = currentUser,
                    ModifiedBy = currentUser,
                    SharedLibrarySourceId = shared.ID
                };
                _context.UseCaseTests.Add(newTest);
                await _context.SaveChangesAsync();
            }

            // Record or upsert consumption
            var alreadyConsumed = await _context.SharedTestConsumptions
                .AnyAsync(c => c.SharedTestDefinitionId == id && c.ConsumedByUserId == currentUser.Id);

            if (!alreadyConsumed)
            {
                _context.SharedTestConsumptions.Add(new SharedTestConsumption
                {
                    SharedTestDefinitionId = id,
                    ConsumedByUserId = currentUser.Id,
                    ConsumedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            StatusMessage = existing != null
                ? "A test linked to this library entry already exists in your tests."
                : "The test has been added to your tests.";

            return RedirectToPage();
        }
    }
}
