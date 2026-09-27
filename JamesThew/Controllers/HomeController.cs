using JamesThew.Authorization;
using JamesThew.Models;
using JamesThew.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace JamesThew.Controllers;

[AllowAnonymous]
public class HomeController(IContentService contentService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var canViewPaid = ContentAccessHelper.CanViewPaidContent(User);
        var model = await contentService.GetHomeDataAsync(canViewPaid);
        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
