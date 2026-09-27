using JamesThew.Models;
using JamesThew.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[AllowAnonymous]
public class ContestsController(IContestService contestService) : Controller
{
    [HttpGet("contests")]
    public async Task<IActionResult> Index(ContestTimelinePhase? phase, ContestType? type)
    {
        var model = await contestService.GetPublicContestsAsync(phase, type);
        return View(model);
    }

    [HttpGet("contests/{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var model = await contestService.GetPublicContestBySlugAsync(slug);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }
}
