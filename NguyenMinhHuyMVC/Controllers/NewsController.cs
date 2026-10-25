using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace NguyenMinhHuyMVC.Controllers;

[AllowAnonymous]
public sealed class NewsController(INewsArticleService newsArticleService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await newsArticleService.GetActiveAsync(cancellationToken));
}
