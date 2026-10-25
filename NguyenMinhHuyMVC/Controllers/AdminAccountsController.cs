using BusinessObjects.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenMinhHuyMVC.Models;
using Services.Interfaces;
using Services.Models;

namespace NguyenMinhHuyMVC.Controllers;

[Authorize(Roles = AccountRoles.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AdminAccountsController(IAccountService accountService, ILogger<AdminAccountsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? role, CancellationToken cancellationToken)
        => View(new AccountListViewModel
        {
            Search = search?.Trim(),
            Role = role,
            Accounts = await accountService.SearchAsync(search, role, cancellationToken)
        });

    [HttpGet]
    public IActionResult Create() => PartialView("_AccountForm", new AccountFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccountFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        ModelState.Remove(nameof(model.Id));
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        return await SaveAsync(model, isCreate: true, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(short id, CancellationToken cancellationToken)
    {
        var account = await accountService.GetByIdAsync(id, cancellationToken);
        if (account is null)
            return NotFound();
        return PartialView("_AccountForm", new AccountFormViewModel
        {
            Id = account.Id, Name = account.Name, Email = account.Email, Role = account.Role ?? 0
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AccountFormViewModel model, CancellationToken cancellationToken)
    {
        if (!model.Id.HasValue)
            return BadRequest();
        return await SaveAsync(model, isCreate: false, cancellationToken);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid account ID." });
        try
        {
            var result = await accountService.DeleteAsync(id, cancellationToken);
            if (!result.Succeeded)
                return UnprocessableEntity(new { message = result.Error });
            TempData["Success"] = "Account deleted successfully.";
            return Json(new { success = true });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to delete account {AccountId}.", id);
            return StatusCode(500, new { message = "Unable to delete the account. Please try again." });
        }
    }

    private async Task<IActionResult> SaveAsync(AccountFormViewModel model, bool isCreate, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var input = new AccountInput(model.Name, model.Email, model.Role, model.Password);
                var result = isCreate
                    ? await accountService.CreateAsync(input, cancellationToken)
                    : await accountService.UpdateAsync(model.Id!.Value, input, cancellationToken);
                if (result.Succeeded)
                {
                    TempData["Success"] = isCreate ? "Account created successfully." : "Account updated successfully.";
                    return Json(new { success = true });
                }
                ModelState.AddModelError(result.Field, result.Error!);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Unable to save account {AccountId}.", model.Id);
                ModelState.AddModelError(string.Empty, "Unable to save the account. Please try again.");
            }
        }

        // Never echo a submitted password back into a validation response.
        model.Password = null;
        if (ModelState.TryGetValue(nameof(model.Password), out var passwordState))
        {
            passwordState.RawValue = null;
            passwordState.AttemptedValue = null;
        }
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return PartialView("_AccountForm", model);
    }
}
