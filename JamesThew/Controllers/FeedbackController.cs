using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[AllowAnonymous]
public class FeedbackController : Controller
{
    [HttpGet("feedback")]
    public IActionResult Index()
    {
        return View();
    }
}
