using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[AllowAnonymous]
public class ContestsController : Controller
{
    [HttpGet("contests")]
    public IActionResult Index()
    {
        return View();
    }
}
