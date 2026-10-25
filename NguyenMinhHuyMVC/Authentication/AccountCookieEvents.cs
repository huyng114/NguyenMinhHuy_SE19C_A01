using System.Security.Claims;
using BusinessObjects.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Services.Interfaces;

namespace NguyenMinhHuyMVC.Authentication;

public sealed class AccountCookieEvents(IAccountService accounts, IAdminAuthenticator adminAuthenticator) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal!;
        var role = principal.FindFirstValue(ClaimTypes.Role);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (role == AccountRoles.Admin && id == "admin" && email is not null && adminAuthenticator.IsAdminEmail(email))
            return;

        var account = short.TryParse(id, out var accountId)
            ? await accounts.GetByIdAsync(accountId, context.HttpContext.RequestAborted)
            : null;
        // Removing an account or changing its role/email must invalidate old privileges.
        if (account is null || AccountRoles.FromDatabase(account.Role) != role ||
            !string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
