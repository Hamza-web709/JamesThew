using System.Security.Claims;
using JamesThew.Authorization;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

public class MembershipController(ISubscriptionService subscriptionService) : Controller
{
    [HttpGet("membership")]
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var model = new SubscriptionRequestViewModel
        {
            IsAuthenticated = User.Identity?.IsAuthenticated == true
        };

        if (model.IsAuthenticated)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var status = await subscriptionService.GetCurrentSubscriptionStatusAsync(userId);
                if (status != null)
                {
                    model.HasActiveSubscription = status.HasActiveSubscription;
                    model.CurrentStatus = status.Status;
                    model.CurrentPlan = status.Plan;
                    model.CurrentAmount = status.Amount;
                    model.ExpiresAtUtc = status.ExpiresAtUtc;
                    model.LatestAdminNotes = status.AdminNotes;
                    model.LatestMemberNotes = status.Notes;
                    model.LatestRequestedAtUtc = status.CreatedAtUtc;
                }

                // If user is Admin, they have implicit access
                if (User.IsInRole(AppRoles.Admin))
                {
                    model.HasActiveSubscription = true;
                }

                model.History = await subscriptionService.GetSubscriptionHistoryAsync(userId);
            }
        }

        return View(model);
    }

    [HttpPost("membership/subscribe")]
    [Authorize(Policy = AppPolicies.MemberAccount)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(SubscriptionRequestViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (model.Plan != SubscriptionPlan.Monthly && model.Plan != SubscriptionPlan.Yearly)
        {
            ModelState.AddModelError(nameof(model.Plan), "Please select a valid demo plan (Monthly or Yearly).");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please correct the form errors and try again.";
            return RedirectToAction(nameof(Index));
        }

        var result = await subscriptionService.SubmitRequestAsync(userId, model.Plan, model.Notes);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
