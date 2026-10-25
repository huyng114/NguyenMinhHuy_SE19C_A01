using BusinessObjects.Models;
using BusinessObjects.Results;
using DataAccessObjects;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class AccountRepository(AccountDAO accountDAO) : IAccountRepository
{
    public Task<SystemAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => accountDAO.GetByEmailAsync(email, cancellationToken);

    public Task<SystemAccount?> GetByIdAsync(short id, CancellationToken cancellationToken = default)
        => accountDAO.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<SystemAccount>> SearchAsync(string? search, int? role, CancellationToken cancellationToken = default)
        => accountDAO.SearchAsync(search, role, cancellationToken);

    public Task<AccountWriteStatus> CreateAsync(SystemAccount account, CancellationToken cancellationToken = default)
        => accountDAO.CreateAsync(account, cancellationToken);

    public Task<AccountWriteStatus> UpdateAsync(SystemAccount account, CancellationToken cancellationToken = default)
        => accountDAO.UpdateAsync(account, cancellationToken);

    public Task<AccountWriteStatus> UpdateProfileAsync(short id, string name, string email,
        string currentPassword, string? newPassword, CancellationToken cancellationToken = default)
        => accountDAO.UpdateProfileAsync(id, name, email, currentPassword, newPassword, cancellationToken);

    public Task<AccountWriteStatus> DeleteAsync(short id, CancellationToken cancellationToken = default)
        => accountDAO.DeleteAsync(id, cancellationToken);
}
