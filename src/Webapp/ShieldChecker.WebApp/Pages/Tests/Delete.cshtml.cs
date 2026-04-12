using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShieldChecker.WebApp;
using ShieldChecker.WebApp.Models.Db;

using Microsoft.AspNetCore.Authorization;
namespace ShieldChecker.WebApp.Pages.Tests
{
    [Authorize(Policy = "RequireAdmin")]
    public class DeleteModel : PageModel
    {
        private readonly ShieldChecker.WebApp.ShieldCheckerContext _context;

        public DeleteModel(ShieldChecker.WebApp.ShieldCheckerContext context)
        {
            _context = context;
        }

        [BindProperty]
        public TestDefinition Test { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var test = await _context.UseCaseTests.FirstOrDefaultAsync(m => m.ID == id);

            if (test == null)
            {
                return NotFound();
            }
            else
            {
                Test = test;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var test = await _context.UseCaseTests.FindAsync(id);
            if (test != null)
            {
                Test = test;
                _context.UseCaseTests.Remove(Test);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
