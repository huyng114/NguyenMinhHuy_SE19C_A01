using BusinessObjects.Models;
using BusinessObjects.Results;

namespace Repositories.Interfaces;

public interface IAccountRepository
{
    Task<SystemAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<SystemAccount?> GetByIdAsync(short id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemAccount>> SearchAsync(string? search, int? role, CancellationToken cancellationToken = default);
    Task<AccountWriteStatus> CreateAsync(SystemAccount account, CancellationToken cancellationToken = default);
    Task<AccountWriteStatus> UpdateAsync(SystemAccount account, CancellationToken cancellationToken = default);
    Task<AccountWriteStatus> UpdateProfileAsync(short id, string name, string email,
        string currentPassword, string? newPassword, CancellationToken cancellationToken = default);
    Task<AccountWriteStatus> DeleteAsync(short id, CancellationToken cancellationToken = default);
}
