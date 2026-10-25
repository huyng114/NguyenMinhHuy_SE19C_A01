using System.Security.Claims;
using BusinessObjects.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenMinhHuyMVC.Models;
using Services.Interfaces;
using Services.Models;

namespace NguyenMinhHuyMVC.Controllers;

[Authorize(Roles = AccountRoles.Staff)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class StaffProfileController(IAccountService accounts, ILogger<StaffProfileController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!short.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)) return Forbid();
        var profile = await accounts.GetByIdAsync(staffId, cancellationToken);
        if (profile is null || profile.Role != 1) return Forbid();
        return View(new StaffProfileViewModel { Name = profile.Name, Email = profile.Email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(StaffProfileViewModel model, CancellationToken cancellationToken)
    {
        if (!short.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var result = await accounts.UpdateProfileAsync(staffId,
                    new StaffProfileInput(model.Name, model.Email, model.CurrentPassword, model.NewPassword), cancellationToken);
                if (result.Succeeded)
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    TempData["Success"] = "Profile updated. Sign in again to continue.";
                    return RedirectToAction("Login", "Account");
                }
                ModelState.AddModelError(result.Field, result.Error!);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Unable to update profile for account {AccountId}.", staffId);
                ModelState.AddModelError(string.Empty, "Unable to update your profile. Please try again.");
            }
        }

        // A validation response must not echo any submitted password values.
        model.CurrentPassword = string.Empty;
        model.NewPassword = null;
        model.ConfirmNewPassword = null;
        foreach (var name in new[] { nameof(model.CurrentPassword), nameof(model.NewPassword), nameof(model.ConfirmNewPassword) })
        {
            if (ModelState.TryGetValue(name, out var state))
            {
                state.RawValue = null;
                state.AttemptedValue = null;
            }
        }
        return View(model);
    }
}
