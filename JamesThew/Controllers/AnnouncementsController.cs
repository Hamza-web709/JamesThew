using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[AllowAnonymous]
public class AnnouncementsController(IContestJudgingService contestJudgingService) : Controller
{
    [HttpGet("announcements")]
    public async Task<IActionResult> Index()
    {
        var announcements = await contestJudgingService.GetPublicAnnouncementsAsync();
        var model = new ContestAnnouncementsViewModel
        {
            Announcements = announcements
        };
        return View(model);
    }
}

