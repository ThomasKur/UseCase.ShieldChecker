using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShieldChecker.WebApp.Models.Db;


namespace ShieldChecker.WebApp.Pages.Settings
{
    [Authorize(Policy = "RequireAdmin")]
    public class SettingsModel : PageModel
    {
        private readonly ShieldCheckerContext _context;

        public SettingsModel(ShieldCheckerContext context, IConfiguration Configuration)
        {
            _context = context;
        }

        [BindProperty]
        public Models.Db.Settings Settings { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {

            var settings = await _context.Settings.FirstOrDefaultAsync(m => m.ID == 1);
            if (settings == null)
            {
                return NotFound();
            }
            Settings = settings;
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

            _context.Attach(Settings).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SettingsExists(Settings.ID))
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

        private bool SettingsExists(int id)
        {
            return _context.Settings.Any(e => e.ID == id);
        }
    }
}
