using System.Security.Claims;
using BusinessObjects.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenMinhHuyMVC.Models;
using Services.Interfaces;

namespace NguyenMinhHuyMVC.Controllers;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AccountController(IAccountService accountService, ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectForRole(User.FindFirstValue(ClaimTypes.Role));

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return InvalidLogin(model);

        Services.Models.AuthenticatedAccount? account;
        try
        {
            account = await accountService.AuthenticateAsync(model.Email, model.Password, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to authenticate because the account service failed.");
            ModelState.AddModelError(string.Empty, "Login is temporarily unavailable. Please try again later.");
            return InvalidLogin(model);
        }

        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return InvalidLogin(model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id),
            new Claim(ClaimTypes.Name, account.Name),
            new Claim(ClaimTypes.Email, account.Email),
            new Claim(ClaimTypes.Role, account.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });

        if (Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl!);

        return RedirectForRole(account.Role);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult RedirectForRole(string? role) => role switch
    {
        AccountRoles.Admin => RedirectToAction("Index", "Admin"),
        AccountRoles.Staff => RedirectToAction("Index", "Staff"),
        _ => RedirectToAction("Index", "News")
    };

    private IActionResult InvalidLogin(LoginViewModel model)
    {
        // Keep the email for correction, but never return the submitted password in the view model or ModelState.
        model.Password = string.Empty;
        if (ModelState.TryGetValue(nameof(model.Password), out var passwordState))
        {
            passwordState.RawValue = null;
            passwordState.AttemptedValue = null;
        }
        return View(nameof(Login), model);
    }
}
