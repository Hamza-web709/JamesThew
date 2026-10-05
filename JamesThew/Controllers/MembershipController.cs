using System.Security.Claims;
using System.Text.RegularExpressions;
using JamesThew.Authorization;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

public class MembershipController(ISubscriptionService subscriptionService) : Controller
{
    private const string DemoPaymentPlanKey = "DemoPaymentPlan";
    private const string DemoPaymentNetworkKey = "DemoPaymentNetwork";
    private const string DemoPaymentLastFourKey = "DemoPaymentLastFour";

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
    public IActionResult Subscribe(SubscriptionRequestViewModel model)
    {
        if (model.Plan != SubscriptionPlan.Monthly && model.Plan != SubscriptionPlan.Yearly)
        {
            ModelState.AddModelError(nameof(model.Plan), "Please select a valid demo plan (Monthly or Yearly).");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please correct the form errors and try again.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Checkout), new { plan = model.Plan });
    }

    [HttpGet("membership/checkout")]
    [Authorize(Policy = AppPolicies.MemberAccount)]
    public async Task<IActionResult> Checkout(SubscriptionPlan plan = SubscriptionPlan.Monthly)
    {
        if (!IsSupportedPlan(plan))
        {
            TempData["ErrorMessage"] = "Please select Monthly or Yearly demo membership.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var status = await subscriptionService.GetCurrentSubscriptionStatusAsync(userId);
        if (status?.HasActiveSubscription == true)
        {
            TempData["SuccessMessage"] = "Your premium membership is already active.";
            return RedirectToAction(nameof(Index));
        }

        return View(new DemoCheckoutViewModel { Plan = plan });
    }

    [HttpPost("membership/checkout")]
    [Authorize(Policy = AppPolicies.MemberAccount)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(DemoCheckoutViewModel model)
    {
        if (!IsSupportedPlan(model.Plan))
            ModelState.AddModelError(nameof(model.Plan), "Please select Monthly or Yearly demo membership.");

        var digits = DemoCardValidator.DigitsOnly(model.CardNumber);
        var network = DemoCardValidator.DetectNetwork(digits);
        model.DetectedNetwork = network;

        if (!DemoCardValidator.IsSupportedNetwork(network))
            ModelState.AddModelError(nameof(model.CardNumber), "Use a valid Visa or Mastercard demo card number.");
        if (!DemoCardValidator.IsValidLuhn(digits))
            ModelState.AddModelError(nameof(model.CardNumber), "Card number failed the Luhn validation check.");
        if (!IsValidExpiry(model.Expiry))
            ModelState.AddModelError(nameof(model.Expiry), "Enter a valid future expiry date in MM/YY format.");
        if (!Regex.IsMatch(model.Cvv ?? string.Empty, @"^\d{3}$"))
            ModelState.AddModelError(nameof(model.Cvv), "CVV must be exactly three digits for this demo.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var status = await subscriptionService.GetCurrentSubscriptionStatusAsync(userId);
        if (status?.HasActiveSubscription == true)
        {
            TempData["SuccessMessage"] = "Your premium membership is already active.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
            return View(model);

        TempData[DemoPaymentPlanKey] = ((int)model.Plan).ToString();
        TempData[DemoPaymentNetworkKey] = network;
        TempData[DemoPaymentLastFourKey] = digits[^4..];
        return RedirectToAction(nameof(PaymentOtp));
    }

    [HttpGet("membership/payment-otp")]
    [Authorize(Policy = AppPolicies.MemberAccount)]
    public IActionResult PaymentOtp()
    {
        if (!TryReadDemoPaymentTempData(out var plan, out var network, out var lastFour))
        {
            TempData["ErrorMessage"] = "Start from checkout to receive a demo payment OTP challenge.";
            return RedirectToAction(nameof(Index));
        }

        KeepDemoPaymentTempData();
        return View(new DemoPaymentOtpViewModel
        {
            Plan = plan,
            Network = network,
            LastFour = lastFour
        });
    }

    [HttpPost("membership/payment-otp")]
    [Authorize(Policy = AppPolicies.MemberAccount)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PaymentOtp(DemoPaymentOtpViewModel model)
    {
        if (!TryReadDemoPaymentTempData(out var plan, out var network, out var lastFour))
        {
            TempData["ErrorMessage"] = "Start from checkout to receive a demo payment OTP challenge.";
            return RedirectToAction(nameof(Index));
        }

        model.Plan = plan;
        model.Network = network;
        model.LastFour = lastFour;

        if (!ModelState.IsValid)
        {
            KeepDemoPaymentTempData();
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var result = await subscriptionService.ActivateDemoCheckoutAsync(
            userId,
            plan,
            $"Demo Payment / Academic Simulation verified for {network} ending {lastFour}.");
        if (!result.Success)
        {
            KeepDemoPaymentTempData();
            TempData["ErrorMessage"] = result.Message;
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        ClearDemoPaymentTempData();
        return RedirectToAction(nameof(Index));
    }

    private static bool IsSupportedPlan(SubscriptionPlan plan) =>
        plan is SubscriptionPlan.Monthly or SubscriptionPlan.Yearly;

    private static bool IsValidExpiry(string? expiry)
    {
        var match = Regex.Match(expiry ?? string.Empty, @"^(0[1-9]|1[0-2])\/(\d{2})$");
        if (!match.Success)
            return false;

        var month = int.Parse(match.Groups[1].Value);
        var year = 2000 + int.Parse(match.Groups[2].Value);
        var lastDay = DateTime.DaysInMonth(year, month);
        var expiresAt = new DateTime(year, month, lastDay, 23, 59, 59, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        return expiresAt >= now.Date && expiresAt <= now.AddYears(15);
    }

    private bool TryReadDemoPaymentTempData(out SubscriptionPlan plan, out string network, out string lastFour)
    {
        plan = SubscriptionPlan.Monthly;
        network = "Unknown Card";
        lastFour = string.Empty;

        if (!int.TryParse(TempData[DemoPaymentPlanKey]?.ToString(), out var planValue)
            || !Enum.IsDefined(typeof(SubscriptionPlan), planValue))
            return false;

        plan = (SubscriptionPlan)planValue;
        network = TempData[DemoPaymentNetworkKey]?.ToString() ?? "Unknown Card";
        lastFour = TempData[DemoPaymentLastFourKey]?.ToString() ?? string.Empty;
        return IsSupportedPlan(plan)
            && DemoCardValidator.IsSupportedNetwork(network)
            && Regex.IsMatch(lastFour, @"^\d{4}$");
    }

    private void KeepDemoPaymentTempData()
    {
        TempData.Keep(DemoPaymentPlanKey);
        TempData.Keep(DemoPaymentNetworkKey);
        TempData.Keep(DemoPaymentLastFourKey);
    }

    private void ClearDemoPaymentTempData()
    {
        TempData.Remove(DemoPaymentPlanKey);
        TempData.Remove(DemoPaymentNetworkKey);
        TempData.Remove(DemoPaymentLastFourKey);
    }
}
