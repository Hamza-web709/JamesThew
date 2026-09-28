using System.Security.Claims;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

public class ContestsController(IContestService contestService, IContestEntryService contestEntryService) : Controller
{
    [AllowAnonymous]
    [HttpGet("contests")]
    public async Task<IActionResult> Index(ContestTimelinePhase? phase, ContestType? type)
    {
        var model = await contestService.GetPublicContestsAsync(phase, type);
        return View(model);
    }

    [AllowAnonymous]
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

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                model.HasEntered = await contestEntryService.HasMemberEnteredAsync(model.Id, userId);
                if (model.HasEntered)
                {
                    model.UserEntryId = await contestEntryService.GetMemberEntryIdAsync(model.Id, userId);
                }
            }
        }

        return View(model);
    }

    [Authorize(Roles = "Member,Admin")]
    [HttpGet("contests/{slug}/entry")]
    public async Task<IActionResult> Entry(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var model = await contestEntryService.GetEntryFormAsync(slug, userId);
        if (model == null)
        {
            return NotFound();
        }

        if (model.IsReadonly && !model.IsEdit)
        {
            TempData["ErrorMessage"] = "Submissions for this competition are not currently open.";
            return RedirectToAction(nameof(Detail), new { slug });
        }

        return View("EntryForm", model);
    }

    [Authorize(Roles = "Member,Admin")]
    [HttpPost("contests/{slug}/entry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entry(string slug, ContestEntryFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var formInfo = await contestEntryService.GetEntryFormAsync(slug, userId);
        if (formInfo == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            // Repopulate contest context if form has validation errors
            var existingForm = await contestEntryService.GetEntryFormAsync(slug, userId);
            if (existingForm != null)
            {
                model.ContestTitle = existingForm.ContestTitle;
                model.ContestType = existingForm.ContestType;
                model.ContestSlug = existingForm.ContestSlug;
                model.ClosesAtUtc = existingForm.ClosesAtUtc;
                model.IsReadonly = existingForm.IsReadonly;
            }
            return View("EntryForm", model);
        }

        var (success, message, entryId) = await contestEntryService.SubmitOrUpdateEntryAsync(slug, userId, model);
        if (success)
        {
            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(MyEntries));
        }

        ModelState.AddModelError(string.Empty, message);
        var refreshedForm = await contestEntryService.GetEntryFormAsync(slug, userId);
        if (refreshedForm != null)
        {
            model.ContestTitle = refreshedForm.ContestTitle;
            model.ContestType = refreshedForm.ContestType;
            model.ContestSlug = refreshedForm.ContestSlug;
            model.ClosesAtUtc = refreshedForm.ClosesAtUtc;
            model.IsReadonly = refreshedForm.IsReadonly;
        }

        return View("EntryForm", model);
    }

    [Authorize(Roles = "Member,Admin")]
    [HttpGet("contests/my-entries")]
    public async Task<IActionResult> MyEntries()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var model = await contestEntryService.GetMyEntriesAsync(userId);
        return View(model);
    }

    [Authorize(Roles = "Member,Admin")]
    [HttpGet("contests/{slug}/my-entry")]
    public async Task<IActionResult> MyEntryDetail(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var model = await contestEntryService.GetMemberEntryByContestSlugAsync(slug, userId);
        if (model == null)
        {
            return NotFound();
        }

        return View("MyEntryDetail", model);
    }
}
