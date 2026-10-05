using C2C.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace C2C.Web.Pages.Admin;

public class IndexModel(AppDb db, IConfiguration config) : PageModel
{
    public List<QuoteRequest> Quotes { get; set; } = [];
    public List<JobApplication> Applications { get; set; } = [];

    public async Task OnGetAsync()
    {
        Quotes = await db.Quotes.AsNoTracking().OrderByDescending(x => x.CreatedUtc).ToListAsync();
        Applications = await db.Applications.AsNoTracking().OrderByDescending(x => x.CreatedUtc).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleQuoteAsync(int id)
    {
        var q = await db.Quotes.FindAsync(id);
        if (q is not null) { q.Handled = !q.Handled; await db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleApplicationAsync(int id)
    {
        var a = await db.Applications.FindAsync(id);
        if (a is not null) { a.Handled = !a.Handled; await db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadAsync(int id)
    {
        var a = await db.Applications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        var dataDir = config["DataDir"]!;
        var path = Path.Combine(dataDir, "uploads", Path.GetFileName(a.StoredFileName)); // GetFileName guards against traversal
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/octet-stream", a.OriginalFileName);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
}
