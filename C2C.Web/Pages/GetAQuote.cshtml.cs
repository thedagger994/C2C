using System.ComponentModel.DataAnnotations;
using C2C.Web.Data;
using C2C.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace C2C.Web.Pages;

public class QuoteInput
{
    [Required(ErrorMessage = "Please enter your first name."), StringLength(80)] public string? FirstName { get; set; }
    [Required(ErrorMessage = "Please enter your last name."), StringLength(80)] public string? LastName { get; set; }
    [Required(ErrorMessage = "Please enter your email."), EmailAddress(ErrorMessage = "Please enter a valid email."), StringLength(200)] public string? Email { get; set; }
    [Required(ErrorMessage = "Please enter your phone number."), StringLength(40)] public string? Phone { get; set; }
    [Required(ErrorMessage = "Please tell us which services you need."), StringLength(2000)] public string? ServicesRequired { get; set; }
    [StringLength(200)] public string? StartTiming { get; set; }
    public string? Website { get; set; } // honeypot
}

public class ApplyInput
{
    [Required(ErrorMessage = "Please enter your name."), StringLength(160)] public string? Company { get; set; }
    [Required(ErrorMessage = "Please enter your email."), EmailAddress(ErrorMessage = "Please enter a valid email."), StringLength(200)] public string? Email { get; set; }
    [Required(ErrorMessage = "Please enter your phone number."), StringLength(40)] public string? Phone { get; set; }
    [Required(ErrorMessage = "Please choose a field."), StringLength(120)] public string? Field { get; set; }
    [Required(ErrorMessage = "Please add your profile or LinkedIn link."), StringLength(4000)] public string? Profile { get; set; }
    [Required(ErrorMessage = "Please attach a file.")] public IFormFile? Upload { get; set; }
    public string? Website { get; set; } // honeypot
}

[RequestSizeLimit(12 * 1024 * 1024)]
public class GetAQuoteModel(AppDb db, Mailer mailer, IConfiguration config, ILogger<GetAQuoteModel> log) : PageModel
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedExt = [".pdf", ".doc", ".docx"];

    public static readonly string[] Fields =
    [
        "Structural Design and Analysis", "Quality Verification", "Technical Field Testing",
        "Development of Engineering Procedures", "Quality Audit", "Material Inspections",
        "Structural Rehabilitation", "Geotechnical Science", "Construction / General Contracting", "Other"
    ];

    public QuoteInput Quote { get; set; } = new();
    public ApplyInput Apply { get; set; } = new();
    public string? Sent { get; set; }
    public string? Failed { get; set; }

    public void OnGet() => Sent = TempData["Sent"] as string;

    public async Task<IActionResult> OnPostQuoteAsync([Bind(Prefix = "Quote")] QuoteInput quote)
    {
        Quote = quote;
        if (!string.IsNullOrEmpty(quote.Website)) return Done("quote"); // bot: pretend success
        if (!ModelState.IsValid) { Failed = "quote"; return Page(); }

        var q = new QuoteRequest
        {
            FirstName = quote.FirstName!.Trim(), LastName = quote.LastName!.Trim(), Email = quote.Email!.Trim(),
            Phone = quote.Phone!.Trim(), ServicesRequired = quote.ServicesRequired!.Trim(), StartTiming = quote.StartTiming?.Trim()
        };
        db.Quotes.Add(q);
        await db.SaveChangesAsync();
        await mailer.NotifyAsync($"New quote request from {q.FirstName} {q.LastName}",
            $"Name: {q.FirstName} {q.LastName}\nEmail: {q.Email}\nPhone: {q.Phone}\nStart: {q.StartTiming}\n\nServices required:\n{q.ServicesRequired}", q.Email);
        return Done("quote");
    }

    public async Task<IActionResult> OnPostApplyAsync([Bind(Prefix = "Apply")] ApplyInput apply)
    {
        Apply = apply;
        if (!string.IsNullOrEmpty(apply.Website)) return Done("apply");
        if (apply.Upload is not null)
        {
            var ext = Path.GetExtension(apply.Upload.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext)) ModelState.AddModelError("Apply.Upload", "Please upload a PDF or Word document.");
            else if (apply.Upload.Length == 0 || apply.Upload.Length > MaxUploadBytes) ModelState.AddModelError("Apply.Upload", "File must be 10 MB or smaller.");
            else if (!await LooksLikeDocument(apply.Upload, ext)) ModelState.AddModelError("Apply.Upload", "That file doesn't look like a valid PDF or Word document.");
        }
        if (!ModelState.IsValid) { Failed = "apply"; return Page(); }

        var dir = Path.Combine(config["DataDir"]!, "uploads");
        Directory.CreateDirectory(dir);
        var stored = Guid.NewGuid().ToString("N") + Path.GetExtension(apply.Upload!.FileName).ToLowerInvariant();
        await using (var fs = System.IO.File.Create(Path.Combine(dir, stored)))
            await apply.Upload.CopyToAsync(fs);

        var a = new JobApplication
        {
            Company = apply.Company!.Trim(), Email = apply.Email!.Trim(), Phone = apply.Phone!.Trim(), Field = apply.Field!.Trim(),
            Profile = apply.Profile!.Trim(), OriginalFileName = Path.GetFileName(apply.Upload.FileName), StoredFileName = stored
        };
        db.Applications.Add(a);
        await db.SaveChangesAsync();
        await mailer.NotifyAsync($"New 'Work With Us' submission from {a.Company}",
            $"Name: {a.Company}\nEmail: {a.Email}\nPhone: {a.Phone}\nField: {a.Field}\nFile: {a.OriginalFileName} (download from the admin page)\n\nProfile/LinkedIn:\n{a.Profile}", a.Email);
        log.LogInformation("Stored application {Id} file {File}", a.Id, stored);
        return Done("apply");
    }

    private IActionResult Done(string which)
    {
        TempData["Sent"] = which;
        return RedirectToPage(null, null, null, which == "quote" ? "quote" : "join");
    }

    private static async Task<bool> LooksLikeDocument(IFormFile f, string ext)
    {
        var head = new byte[8];
        await using var s = f.OpenReadStream();
        var n = await s.ReadAsync(head);
        if (n < 4) return false;
        return ext switch
        {
            ".pdf" => head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46,           // %PDF
            ".docx" => head[0] == 0x50 && head[1] == 0x4B,                                                  // PK (zip)
            ".doc" => head[0] == 0xD0 && head[1] == 0xCF && head[2] == 0x11 && head[3] == 0xE0,            // OLE2
            _ => false
        };
    }
}
