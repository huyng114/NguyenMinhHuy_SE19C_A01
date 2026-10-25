using BusinessObjects.Models;
using BusinessObjects.Results;
using DataAccessObjects.Context;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DataAccessObjects;

public sealed class AccountDAO(FUNewsManagementContext context)
{
    public Task<SystemAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => context.SystemAccounts.AsNoTracking()
            .FirstOrDefaultAsync(account => account.AccountEmail == email, cancellationToken);

    public Task<SystemAccount?> GetByIdAsync(short id, CancellationToken cancellationToken = default)
        => context.SystemAccounts.AsNoTracking().FirstOrDefaultAsync(account => account.AccountId == id, cancellationToken);

    public async Task<IReadOnlyList<SystemAccount>> SearchAsync(string? search, int? role, CancellationToken cancellationToken = default)
    {
        var query = context.SystemAccounts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(account =>
                (account.AccountName != null && account.AccountName.Contains(search)) ||
                (account.AccountEmail != null && account.AccountEmail.Contains(search)));
        }
        if (role.HasValue)
            query = query.Where(account => account.AccountRole == role);

        return await query.OrderBy(account => account.AccountId)
            .Select(account => new SystemAccount
            {
                AccountId = account.AccountId,
                AccountName = account.AccountName,
                AccountEmail = account.AccountEmail,
                AccountRole = account.AccountRole
            }).ToListAsync(cancellationToken);
    }

    public async Task<AccountWriteStatus> CreateAsync(SystemAccount account, CancellationToken cancellationToken = default)
    {
        // The supplied schema has neither an identity ID nor a unique email index.
        // Keep duplicate detection, ID allocation and insert in one serializable transaction.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await EmailExistsAsync(account.AccountEmail!, null, cancellationToken))
            return AccountWriteStatus.DuplicateEmail;

        var maximumId = await context.SystemAccounts.MaxAsync(item => (int?)item.AccountId, cancellationToken) ?? 0;
        if (maximumId >= short.MaxValue)
            return AccountWriteStatus.IdLimitReached;

        account.AccountId = (short)(Math.Max(0, maximumId) + 1);
        context.SystemAccounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountWriteStatus.Success;
    }

    public async Task<AccountWriteStatus> UpdateAsync(SystemAccount changes, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var account = await context.SystemAccounts.FindAsync([changes.AccountId], cancellationToken);
        if (account is null)
            return AccountWriteStatus.NotFound;
        if (await EmailExistsAsync(changes.AccountEmail!, changes.AccountId, cancellationToken))
            return AccountWriteStatus.DuplicateEmail;

        account.AccountName = changes.AccountName;
        account.AccountEmail = changes.AccountEmail;
        account.AccountRole = changes.AccountRole;
        if (changes.AccountPassword is not null)
            account.AccountPassword = changes.AccountPassword;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountWriteStatus.Success;
    }

    public async Task<AccountWriteStatus> UpdateProfileAsync(short id, string name, string email,
        string currentPassword, string? newPassword, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var account = await context.SystemAccounts.FindAsync([id], cancellationToken);
        if (account is null || account.AccountRole != 1)
            return AccountWriteStatus.NotFound;
        if (!string.Equals(account.AccountPassword, currentPassword, StringComparison.Ordinal))
            return AccountWriteStatus.WrongPassword;
        if (await EmailExistsAsync(email, id, cancellationToken))
            return AccountWriteStatus.DuplicateEmail;

        account.AccountName = name;
        account.AccountEmail = email;
        if (!string.IsNullOrEmpty(newPassword))
            account.AccountPassword = newPassword;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountWriteStatus.Success;
    }

    public async Task<AccountWriteStatus> DeleteAsync(short id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var account = await context.SystemAccounts.FindAsync([id], cancellationToken);
        if (account is null)
            return AccountWriteStatus.NotFound;

        // Prevent the supplied cascade relationship from deleting an author's news.
        // UpdatedByID is also checked even though the schema has no FK for it.
        if (await context.NewsArticles.AnyAsync(article => article.CreatedById == id || article.UpdatedById == id, cancellationToken))
            return AccountWriteStatus.InUse;

        context.SystemAccounts.Remove(account);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountWriteStatus.Success;
    }

    private Task<bool> EmailExistsAsync(string email, short? excludingId, CancellationToken cancellationToken)
    {
        var normalized = email.ToUpperInvariant();
        return context.SystemAccounts.AnyAsync(account =>
            (!excludingId.HasValue || account.AccountId != excludingId.Value) &&
            account.AccountEmail != null && account.AccountEmail.ToUpper() == normalized, cancellationToken);
    }
}
