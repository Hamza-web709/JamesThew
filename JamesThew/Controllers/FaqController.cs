using JamesThew.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[AllowAnonymous]
public class FaqController(IContentService contentService) : Controller
{
    [HttpGet("faq")]
    public async Task<IActionResult> Index()
    {
        var model = await contentService.GetFaqsAsync();
        return View(model);
    }
}
