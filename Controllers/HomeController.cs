using Microsoft.AspNetCore.Mvc;

namespace ShiftManagementPlatform.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Error() => View();
}
