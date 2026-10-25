using BusinessObjects.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenMinhHuyMVC.Models;
using Services.Interfaces;

namespace NguyenMinhHuyMVC.Controllers;

[Authorize(Roles = AccountRoles.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AdminController(INewsArticleService newsService, ILogger<AdminController> logger) : Controller
{
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Report(NewsReportViewModel model, CancellationToken cancellationToken)
    {
        if (model.StartDate.HasValue && model.EndDate.HasValue && model.StartDate.Value.Date > model.EndDate.Value.Date)
            ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after start date.");

        if (ModelState.IsValid)
        {
            try
            {
                model.Report = await newsService.GetReportAsync(
                    model.StartDate!.Value, model.EndDate!.Value, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Unable to generate the news report.");
                ModelState.AddModelError(string.Empty, "Unable to generate the report. Please try again.");
            }
        }
        return View(model);
    }
}
