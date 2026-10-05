using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace C2C.Web.Pages.Admin;

public class LoginModel(IConfiguration config) : PageModel
{
    public string? Error { get; set; }
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? username, string? password, string? returnUrl)
    {
        if (!AdminAuth.Check(config, username, password))
        {
            await Task.Delay(750); // slow down guessing
            Error = "Incorrect username or password.";
            ReturnUrl = returnUrl;
            return Page();
        }
        var id = new ClaimsIdentity([new Claim(ClaimTypes.Name, username!)], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(id));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin");
    }
}
