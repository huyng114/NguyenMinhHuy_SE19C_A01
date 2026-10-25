using Services.Models;

namespace Services.Interfaces;

public interface IAdminAuthenticator
{
    bool IsAdminEmail(string email);
    AuthenticatedAccount? Authenticate(string email, string password);
}
