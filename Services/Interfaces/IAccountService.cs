using Services.Models;

namespace Services.Interfaces;

public interface IAccountService
{
    Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountSummary>> SearchAsync(string? search, int? role, CancellationToken cancellationToken = default);
    Task<AccountSummary?> GetByIdAsync(short id, CancellationToken cancellationToken = default);
    Task<AccountOperationResult> CreateAsync(AccountInput input, CancellationToken cancellationToken = default);
    Task<AccountOperationResult> UpdateAsync(short id, AccountInput input, CancellationToken cancellationToken = default);
    Task<AccountOperationResult> DeleteAsync(short id, CancellationToken cancellationToken = default);
    Task<AccountOperationResult> UpdateProfileAsync(short id, StaffProfileInput input, CancellationToken cancellationToken = default);
}
