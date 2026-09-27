using System.Security.Claims;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[Authorize(Roles = "Member,Admin")]
public class ContributionsController(IContributionService contributionService) : Controller
{
    [HttpGet("contributions")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var model = await contributionService.GetMemberContributionsAsync(userId);
        return View(model);
    }

    [HttpGet("contributions/recipe/new")]
    public IActionResult NewRecipe()
    {
        return View(new RecipeContributionViewModel());
    }

    [HttpPost("contributions/recipe/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewRecipe(RecipeContributionViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!ModelState.IsValid)
            return View(model);

        var displayName = User.Identity?.Name ?? "Community Member";
        var result = await contributionService.SubmitRecipeContributionAsync(userId, displayName, model);
        if (result)
        {
            TempData["SuccessMessage"] = "Your recipe contribution has been submitted successfully and is currently pending administrator review.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, "Could not submit recipe. Please check that you provided at least one ingredient and step.");
        return View(model);
    }

    [HttpGet("contributions/tip/new")]
    public IActionResult NewTip()
    {
        return View(new TipContributionViewModel());
    }

    [HttpPost("contributions/tip/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewTip(TipContributionViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!ModelState.IsValid)
            return View(model);

        var displayName = User.Identity?.Name ?? "Community Member";
        var result = await contributionService.SubmitTipContributionAsync(userId, displayName, model);
        if (result)
        {
            TempData["SuccessMessage"] = "Your cooking tip contribution has been submitted successfully and is currently pending administrator review.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, "Could not submit cooking tip. Please check your submission and try again.");
        return View(model);
    }
}
