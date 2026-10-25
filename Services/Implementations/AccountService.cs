using BusinessObjects.Security;
using BusinessObjects.Models;
using BusinessObjects.Results;
using System.ComponentModel.DataAnnotations;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Models;

namespace Services.Implementations;

public sealed class AccountService(IAccountRepository repository, IAdminAuthenticator adminAuthenticator) : IAccountService
{
    public async Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return null;

        email = email.Trim();
        if (adminAuthenticator.IsAdminEmail(email))
            return adminAuthenticator.Authenticate(email, password);

        var account = await repository.GetByEmailAsync(email, cancellationToken);
        // Compare in C# so a case-insensitive SQL collation cannot accept a wrong password.
        if (account is null || !string.Equals(account.AccountPassword, password, StringComparison.Ordinal))
            return null;

        var role = AccountRoles.FromDatabase(account.AccountRole);
        return role is null ? null : new AuthenticatedAccount(
            account.AccountId.ToString(), account.AccountName ?? email, account.AccountEmail ?? email, role);
    }

    public async Task<IReadOnlyList<AccountSummary>> SearchAsync(string? search, int? role, CancellationToken cancellationToken = default)
        => (await repository.SearchAsync(search?.Trim(), role, cancellationToken)).Select(ToSummary).ToList();

    public async Task<AccountSummary?> GetByIdAsync(short id, CancellationToken cancellationToken = default)
    {
        var account = await repository.GetByIdAsync(id, cancellationToken);
        return account is null ? null : ToSummary(account);
    }

    public async Task<AccountOperationResult> CreateAsync(AccountInput input, CancellationToken cancellationToken = default)
    {
        var error = Validate(input, isCreate: true);
        if (error is not null)
            return error;
        return ToResult(await repository.CreateAsync(ToEntity(input), cancellationToken));
    }

    public async Task<AccountOperationResult> UpdateAsync(short id, AccountInput input, CancellationToken cancellationToken = default)
    {
        var error = Validate(input, isCreate: false);
        if (error is not null)
            return error;
        var account = ToEntity(input);
        account.AccountId = id;
        return ToResult(await repository.UpdateAsync(account, cancellationToken));
    }

    public async Task<AccountOperationResult> DeleteAsync(short id, CancellationToken cancellationToken = default)
        => ToResult(await repository.DeleteAsync(id, cancellationToken));

    public async Task<AccountOperationResult> UpdateProfileAsync(short id, StaffProfileInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
            return AccountOperationResult.Failure("Name is required and must be at most 100 characters.", "Name");
        if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Trim().Length > 70 ||
            !new EmailAddressAttribute().IsValid(input.Email.Trim()))
            return AccountOperationResult.Failure("Enter a valid email address of at most 70 characters.", "Email");
        if (adminAuthenticator.IsAdminEmail(input.Email.Trim()))
            return AccountOperationResult.Failure("This email is reserved for the administrator.", "Email");
        if (string.IsNullOrEmpty(input.CurrentPassword))
            return AccountOperationResult.Failure("Enter your current password.", "CurrentPassword");
        if (input.NewPassword?.Length > 70 || input.CurrentPassword.Length > 70)
            return AccountOperationResult.Failure("Passwords must be at most 70 characters.", "NewPassword");
        if (!string.IsNullOrEmpty(input.NewPassword) && string.IsNullOrWhiteSpace(input.NewPassword))
            return AccountOperationResult.Failure("New password cannot contain only spaces.", "NewPassword");
        return ToResult(await repository.UpdateProfileAsync(id, input.Name.Trim(), input.Email.Trim(),
            input.CurrentPassword, input.NewPassword, cancellationToken));
    }

    private AccountOperationResult? Validate(AccountInput input, bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
            return AccountOperationResult.Failure("Name is required and must be at most 100 characters.", "Name");
        if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Trim().Length > 70 ||
            !new EmailAddressAttribute().IsValid(input.Email.Trim()))
            return AccountOperationResult.Failure("Enter a valid email address of at most 70 characters.", "Email");
        if (adminAuthenticator.IsAdminEmail(input.Email.Trim()))
            return AccountOperationResult.Failure("This email is reserved for the administrator.", "Email");
        if (AccountRoles.FromDatabase(input.Role) is null)
            return AccountOperationResult.Failure("Choose Staff or Lecturer.", "Role");
        if (isCreate && string.IsNullOrWhiteSpace(input.Password))
            return AccountOperationResult.Failure("Password is required.", "Password");
        if (input.Password?.Length > 70)
            return AccountOperationResult.Failure("Password must be at most 70 characters.", "Password");
        if (!string.IsNullOrEmpty(input.Password) && string.IsNullOrWhiteSpace(input.Password))
            return AccountOperationResult.Failure("Password cannot contain only spaces.", "Password");
        return null;
    }

    private static SystemAccount ToEntity(AccountInput input) => new()
    {
        AccountName = input.Name.Trim(),
        AccountEmail = input.Email.Trim(),
        AccountRole = input.Role,
        AccountPassword = string.IsNullOrEmpty(input.Password) ? null : input.Password
    };

    private static AccountSummary ToSummary(SystemAccount account)
        => new(account.AccountId, account.AccountName ?? string.Empty, account.AccountEmail ?? string.Empty, account.AccountRole);

    private static AccountOperationResult ToResult(AccountWriteStatus status) => status switch
    {
        AccountWriteStatus.Success => AccountOperationResult.Success(),
        AccountWriteStatus.NotFound => AccountOperationResult.Failure("This account no longer exists. Refresh the list and try again."),
        AccountWriteStatus.DuplicateEmail => AccountOperationResult.Failure("This email is already used by another account.", "Email"),
        AccountWriteStatus.WrongPassword => AccountOperationResult.Failure("Current password is incorrect.", "CurrentPassword"),
        AccountWriteStatus.InUse => AccountOperationResult.Failure("This account cannot be deleted because it is linked to news articles."),
        AccountWriteStatus.IdLimitReached => AccountOperationResult.Failure("No more account IDs are available."),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
