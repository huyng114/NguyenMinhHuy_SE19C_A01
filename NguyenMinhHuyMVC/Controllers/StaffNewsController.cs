using System.Security.Claims;
using BusinessObjects.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenMinhHuyMVC.Models;
using Services.Interfaces;
using Services.Models;

namespace NguyenMinhHuyMVC.Controllers;

[Authorize(Roles = AccountRoles.Staff)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class StaffNewsController(INewsArticleService newsService, ICategoryService categoryService,
    ILogger<StaffNewsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        if (!short.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)) return Forbid();
        return View(await newsService.GetByAuthorAsync(staffId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool? status, short? categoryId, CancellationToken cancellationToken)
        => View(new NewsListViewModel
        {
            Search = search?.Trim(), Status = status, CategoryId = categoryId,
            Articles = await newsService.SearchAsync(search, status, categoryId, cancellationToken),
            Categories = await categoryService.SearchAsync(null, null, cancellationToken)
        });

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 20) return NotFound();
        var article = await newsService.GetByIdAsync(id, cancellationToken);
        return article is null ? NotFound() : View(article);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
        => await FormAsync(new NewsFormViewModel(), cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewsFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        ModelState.Remove(nameof(model.Id));
        return await SaveAsync(model, true, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 20) return NotFound();
        var article = await newsService.GetByIdAsync(id, cancellationToken);
        if (article is null) return NotFound();
        return await FormAsync(new NewsFormViewModel
        {
            Id = article.Id, Title = article.Title, Headline = article.Headline, Content = article.Content,
            Source = article.Source, CategoryId = article.CategoryId, Status = article.Status,
            TagIds = article.Tags.Select(tag => tag.Id).ToList()
        }, cancellationToken);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(NewsFormViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Id)) return BadRequest();
        return await SaveAsync(model, false, cancellationToken);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 20) return BadRequest(new { message = "Invalid article ID." });
        try
        {
            var result = await newsService.DeleteAsync(id, cancellationToken);
            if (!result.Succeeded) return UnprocessableEntity(new { message = result.Error });
            TempData["Success"] = "Article deleted successfully.";
            return Json(new { success = true });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to delete article {ArticleId}.", id);
            return StatusCode(500, new { message = "Unable to delete the article. Please try again." });
        }
    }

    private async Task<IActionResult> SaveAsync(NewsFormViewModel model, bool isCreate, CancellationToken cancellationToken)
    {
        if (!short.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var input = new NewsArticleInput(model.Title, model.Headline, model.Content, model.Source,
                    model.CategoryId, model.Status, model.TagIds);
                var result = isCreate ? await newsService.CreateAsync(input, staffId, cancellationToken)
                    : await newsService.UpdateAsync(model.Id!, input, staffId, cancellationToken);
                if (result.Succeeded)
                {
                    TempData["Success"] = isCreate ? "Article created successfully." : "Article updated successfully.";
                    return Json(new { success = true });
                }
                ModelState.AddModelError(result.Field, result.Error!);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Unable to save article {ArticleId}.", model.Id);
                ModelState.AddModelError(string.Empty, "Unable to save the article. Please try again.");
            }
        }
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return await FormAsync(model, cancellationToken);
    }

    private async Task<IActionResult> FormAsync(NewsFormViewModel model, CancellationToken cancellationToken)
    {
        model.Categories = await categoryService.SearchAsync(null, null, cancellationToken);
        model.Tags = await newsService.GetTagOptionsAsync(cancellationToken);
        return PartialView("_NewsForm", model);
    }
}
