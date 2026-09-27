using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[AllowAnonymous]
public class AnnouncementsController : Controller
{
    [HttpGet("announcements")]
    public IActionResult Index()
    {
        return View();
    }
}
