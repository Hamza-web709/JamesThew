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

    [HttpGet("contributions/recipe/{id:int}/edit")]
    public async Task<IActionResult> EditRecipe(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var model = await contributionService.GetRecipeContributionForEditAsync(id, userId);
        if (model is null)
            return NotFound();

        return View(model);
    }

    [HttpPost("contributions/recipe/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRecipe(int id, RecipeContributionViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var existing = await contributionService.GetRecipeContributionForEditAsync(id, userId);
        if (existing is null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.CurrentStatus = existing.CurrentStatus;
            model.RejectionReason = existing.RejectionReason;
            return View(model);
        }

        var (success, message) = await contributionService.UpdateRecipeContributionAsync(id, userId, model);
        if (success)
        {
            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, message);
        model.Id = id;
        model.CurrentStatus = existing.CurrentStatus;
        model.RejectionReason = existing.RejectionReason;
        return View(model);
    }

    [HttpGet("contributions/tip/{id:int}/edit")]
    public async Task<IActionResult> EditTip(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var model = await contributionService.GetTipContributionForEditAsync(id, userId);
        if (model is null)
            return NotFound();

        return View(model);
    }

    [HttpPost("contributions/tip/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTip(int id, TipContributionViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var existing = await contributionService.GetTipContributionForEditAsync(id, userId);
        if (existing is null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.CurrentStatus = existing.CurrentStatus;
            model.RejectionReason = existing.RejectionReason;
            return View(model);
        }

        var (success, message) = await contributionService.UpdateTipContributionAsync(id, userId, model);
        if (success)
        {
            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, message);
        model.Id = id;
        model.CurrentStatus = existing.CurrentStatus;
        model.RejectionReason = existing.RejectionReason;
        return View(model);
    }

    [HttpPost("contributions/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var (success, message) = await contributionService.DeleteContributionAsync(id, userId);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Index));
    }
}
