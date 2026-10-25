using BusinessObjects.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenMinhHuyMVC.Models;
using Services.Interfaces;
using Services.Models;

namespace NguyenMinhHuyMVC.Controllers;

[Authorize(Roles = AccountRoles.Staff)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool? isActive, CancellationToken cancellationToken)
        => View(new CategoryListViewModel
        {
            Search = search?.Trim(), IsActive = isActive,
            Categories = await categoryService.SearchAsync(search, isActive, cancellationToken)
        });

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
        => await FormAsync(new CategoryFormViewModel(), cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        ModelState.Remove(nameof(model.Id));
        return await SaveAsync(model, isCreate: true, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(short id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var category = await categoryService.GetByIdAsync(id, cancellationToken);
        if (category is null)
            return NotFound();
        return await FormAsync(new CategoryFormViewModel
        {
            Id = category.Id, Name = category.Name, Description = category.Description,
            ParentId = category.ParentId, IsActive = category.IsActive
        }, cancellationToken);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!model.Id.HasValue)
            return BadRequest();
        return await SaveAsync(model, isCreate: false, cancellationToken);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid category ID." });
        try
        {
            var result = await categoryService.DeleteAsync(id, cancellationToken);
            if (!result.Succeeded)
                return UnprocessableEntity(new { message = result.Error });
            TempData["Success"] = "Category deleted successfully.";
            return Json(new { success = true });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to delete category {CategoryId}.", id);
            return StatusCode(500, new { message = "Unable to delete the category. Please try again." });
        }
    }

    private async Task<IActionResult> SaveAsync(CategoryFormViewModel model, bool isCreate, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var input = new CategoryInput(model.Name, model.Description, model.ParentId, model.IsActive);
                var result = isCreate
                    ? await categoryService.CreateAsync(input, cancellationToken)
                    : await categoryService.UpdateAsync(model.Id!.Value, input, cancellationToken);
                if (result.Succeeded)
                {
                    TempData["Success"] = isCreate ? "Category created successfully." : "Category updated successfully.";
                    return Json(new { success = true });
                }
                ModelState.AddModelError(result.Field, result.Error!);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Unable to save category {CategoryId}.", model.Id);
                ModelState.AddModelError(string.Empty, "Unable to save the category. Please try again.");
            }
        }
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return await FormAsync(model, cancellationToken);
    }

    private async Task<IActionResult> FormAsync(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        model.ParentOptions = await categoryService.GetParentOptionsAsync(model.Id, cancellationToken);
        return PartialView("_CategoryForm", model);
    }
}
