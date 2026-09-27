using System.Security.Claims;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

public class FeedbackController(IFeedbackService feedbackService) : Controller
{
    [AllowAnonymous]
    [HttpGet("feedback")]
    public async Task<IActionResult> Index()
    {
        var model = new FeedbackIndexViewModel
        {
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            UserDisplayName = User.Identity?.Name
        };

        if (model.IsAuthenticated)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                model.UserFeedbacks = await feedbackService.GetUserFeedbackHistoryAsync(userId);
            }
        }

        return View(model);
    }

    [Authorize(Roles = "Member,Admin")]
    [HttpPost("feedback/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(FeedbackIndexViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            model.IsAuthenticated = true;
            model.UserDisplayName = User.Identity?.Name;
            model.UserFeedbacks = await feedbackService.GetUserFeedbackHistoryAsync(userId);
            return View("Index", model);
        }

        var result = await feedbackService.SubmitFeedbackAsync(userId, model.Form);
        if (result)
        {
            TempData["SuccessMessage"] = "Thank you! Your feedback has been submitted successfully for administrator review.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not submit feedback. Please check your message and try again.";
        }

        return RedirectToAction(nameof(Index));
    }
}
