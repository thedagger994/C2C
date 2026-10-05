using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using C2C.Web.Data;
using C2C.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// All writable state (SQLite db + uploaded files) lives under DataDir so it can be a mounted volume.
var dataDir = builder.Configuration["DataDir"];
if (string.IsNullOrWhiteSpace(dataDir)) dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
builder.Configuration["DataDir"] = dataDir;
Directory.CreateDirectory(Path.Combine(dataDir, "uploads"));

builder.Services.AddDbContext<AppDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "c2c.db")}"));
builder.Services.AddSingleton<ImageCatalog>();
builder.Services.AddSingleton<Mailer>();

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin");
    o.Conventions.AllowAnonymousToPage("/Admin/Login");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/admin/login";
        o.Cookie.Name = "c2c.admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();

// Throttle form posts per client IP (GETs are unlimited).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        HttpMethods.IsPost(ctx.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(10) })
            : RateLimitPartition.GetNoLimiter("get"));
});

// The app normally runs behind a TLS-terminating proxy (Caddy, Azure, Fly, etc.).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    h["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self' https://fonts.googleapis.com; " +
        "font-src https://fonts.gstatic.com; script-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'";
    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".webp") || ctx.File.Name.EndsWith(".png"))
            ctx.Context.Response.Headers.CacheControl = "public,max-age=2592000";
    }
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/robots.txt", () => Results.Text("User-agent: *\nDisallow: /admin\nSitemap: https://www.c2cleadengineering.ca/sitemap.xml\n", "text/plain"));
app.MapGet("/sitemap.xml", () =>
{
    var paths = new[] { "", "services", "projects", "about-us", "get-a-quote" };
    var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
    foreach (var p in paths) xml.Append($"<url><loc>https://www.c2cleadengineering.ca/{p}</loc></url>");
    return Results.Text(xml.Append("</urlset>").ToString(), "application/xml");
});

app.MapRazorPages();
app.Run();

public static class AdminAuth
{
    /// <summary>Constant-time check against Admin:Username / Admin:Password. Login is disabled if no password is configured.</summary>
    public static bool Check(IConfiguration cfg, string? user, string? pass)
    {
        var expectedPass = cfg["Admin:Password"];
        if (string.IsNullOrEmpty(expectedPass)) return false;
        var expectedUser = cfg["Admin:Username"];
        if (string.IsNullOrEmpty(expectedUser)) expectedUser = "admin";
        static byte[] H(string? s) => SHA256.HashData(Encoding.UTF8.GetBytes(s ?? ""));
        var okUser = CryptographicOperations.FixedTimeEquals(H(user), H(expectedUser));
        var okPass = CryptographicOperations.FixedTimeEquals(H(pass), H(expectedPass));
        return okUser & okPass;
    }
}
