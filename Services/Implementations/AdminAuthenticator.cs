using BusinessObjects.Security;
using Services.Interfaces;
using Services.Models;

namespace Services.Implementations;

// One immutable instance is registered with DI. It never depends on a scoped DbContext.
public sealed class AdminAuthenticator(string email, string password) : IAdminAuthenticator
{
    public bool IsAdminEmail(string candidate)
        => string.Equals(email, candidate, StringComparison.OrdinalIgnoreCase);

    public AuthenticatedAccount? Authenticate(string candidateEmail, string candidatePassword)
        => IsAdminEmail(candidateEmail) && string.Equals(password, candidatePassword, StringComparison.Ordinal)
            ? new AuthenticatedAccount("admin", "Administrator", email, AccountRoles.Admin)
            : null;
}
