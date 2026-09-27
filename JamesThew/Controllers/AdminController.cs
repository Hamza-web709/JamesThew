using System.Security.Claims;
using JamesThew.Authorization;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
[Route("admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminController(
    ISubscriptionService subscriptionService,
    IFeedbackService feedbackService,
    IContributionService contributionService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.PendingSubscriptionsCount = await subscriptionService.GetPendingCountAsync();
        ViewBag.PendingFeedbackCount = await feedbackService.GetPendingFeedbackCountAsync();
        ViewBag.PendingContributionsCount = await contributionService.GetPendingContributionCountAsync();
        return View();
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> Subscriptions(SubscriptionStatus? status)
    {
        var requests = await subscriptionService.GetAllRequestsAsync(status);
        var pendingCount = await subscriptionService.GetPendingCountAsync();
        var model = new SubscriptionAdminListViewModel
        {
            StatusFilter = status,
            TotalPendingCount = pendingCount,
            Requests = requests
        };
        return View(model);
    }

    [HttpPost("subscriptions/{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSubscription(int id, string? adminNotes)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var success = await subscriptionService.ApproveRequestAsync(id, adminUserId, adminNotes);
        if (success)
            TempData["SuccessMessage"] = $"Subscription request #{id} successfully approved. Paid masterclass access is now active.";
        else
            TempData["ErrorMessage"] = $"Unable to approve subscription request #{id}.";

        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost("subscriptions/{id:int}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectSubscription(int id, string? adminNotes)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var success = await subscriptionService.RejectRequestAsync(id, adminUserId, adminNotes);
        if (success)
            TempData["SuccessMessage"] = $"Subscription request #{id} has been rejected.";
        else
            TempData["ErrorMessage"] = $"Unable to reject subscription request #{id}.";

        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpGet("feedback")]
    public async Task<IActionResult> Feedback(FeedbackStatus? status)
    {
        var model = await feedbackService.GetAdminFeedbackListAsync(status);
        return View(model);
    }

    [HttpPost("feedback/{id:int}/moderate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModerateFeedback(int id, FeedbackStatus status, string? adminNotes)
    {
        var (success, message) = await feedbackService.ModerateFeedbackAsync(id, status, adminNotes);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Feedback));
    }

    [HttpGet("contributions")]
    public async Task<IActionResult> Contributions(ContentKind? kind, PublicationStatus? status)
    {
        var model = await contributionService.GetAdminContributionListAsync(kind, status);
        return View(model);
    }

    [HttpPost("contributions/{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveContribution(int id, ContentVisibility visibility = ContentVisibility.Free)
    {
        var (success, message) = await contributionService.ApproveContributionAsync(id, visibility);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contributions));
    }

    [HttpPost("contributions/{id:int}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectContribution(int id, string? rejectionReason)
    {
        var (success, message) = await contributionService.RejectContributionAsync(id, rejectionReason);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contributions));
    }
}
