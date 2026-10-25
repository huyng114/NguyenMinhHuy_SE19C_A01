using BusinessObjects.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NguyenMinhHuyMVC.Controllers;

[Authorize(Roles = AccountRoles.Staff)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class StaffController : Controller
{
    public IActionResult Index() => View();
}
