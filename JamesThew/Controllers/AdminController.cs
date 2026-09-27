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
public class AdminController(ISubscriptionService subscriptionService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var pendingCount = await subscriptionService.GetPendingCountAsync();
        ViewBag.PendingSubscriptionsCount = pendingCount;
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
}
