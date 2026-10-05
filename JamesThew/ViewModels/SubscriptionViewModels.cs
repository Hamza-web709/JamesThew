using System.ComponentModel.DataAnnotations;
using JamesThew.Models;

namespace JamesThew.ViewModels;

public class SubscriptionRequestViewModel
{
    [Required(ErrorMessage = "Please select a membership plan.")]
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Monthly;

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    [Display(Name = "Demo Payment Reference / Notes (Optional)")]
    public string? Notes { get; set; }

    // User's current status information
    public bool IsAuthenticated { get; set; }
    public bool HasActiveSubscription { get; set; }
    public SubscriptionStatus? CurrentStatus { get; set; }
    public SubscriptionPlan? CurrentPlan { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string? LatestMemberNotes { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? LatestAdminNotes { get; set; }
    public DateTime? LatestRequestedAtUtc { get; set; }

    public List<SubscriptionHistoryItemDto> History { get; set; } = [];
}

public class SubscriptionHistoryItemDto
{
    public int Id { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public decimal Amount { get; set; }
    public SubscriptionStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? AdminNotes { get; set; }
}

public class SubscriptionStatusDto
{
    public bool HasActiveSubscription { get; set; }
    public SubscriptionStatus Status { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}


public class SubscriptionAdminListViewModel
{
    public SubscriptionStatus? StatusFilter { get; set; }
    public int TotalPendingCount { get; set; }
    public List<SubscriptionAdminListItemDto> Requests { get; set; } = [];
}

public class SubscriptionAdminListItemDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; }
    public decimal Amount { get; set; }
    public SubscriptionStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? AdminNotes { get; set; }
    public string? ReviewedByAdminId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public class SubscriptionRequestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RequestId { get; set; }
}

public class DemoCheckoutViewModel
{
    [Required]
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Monthly;

    public string PlanName => Plan == SubscriptionPlan.Yearly ? "Yearly Connoisseur" : "Monthly Masterclass";
    public decimal Amount => Services.SubscriptionService.GetPlanPrice(Plan);
    public string Interval => Plan == SubscriptionPlan.Yearly ? "year" : "month";

    [Required(ErrorMessage = "Cardholder name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Cardholder name must be between 2 and 80 characters.")]
    public string CardholderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Card number is required.")]
    [Display(Name = "Card Number")]
    public string CardNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Expiry date is required.")]
    [Display(Name = "Expiry MM/YY")]
    public string Expiry { get; set; } = string.Empty;

    [Required(ErrorMessage = "CVV is required.")]
    public string Cvv { get; set; } = string.Empty;

    public string? DetectedNetwork { get; set; }
}

public class DemoPaymentOtpViewModel
{
    public SubscriptionPlan Plan { get; set; }
    public string PlanName => Plan == SubscriptionPlan.Yearly ? "Yearly Connoisseur" : "Monthly Masterclass";
    public decimal Amount => Services.SubscriptionService.GetPlanPrice(Plan);
    public string Network { get; set; } = "Unknown Card";
    public string LastFour { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the four-digit demo payment OTP.")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Demo payment OTP must be exactly four digits.")]
    public string DemoPaymentOtp { get; set; } = string.Empty;
}

public static class DemoCardValidator
{
    public static string DigitsOnly(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public static string DetectNetwork(string digits)
    {
        if (digits.StartsWith('4') && digits.Length is >= 13 and <= 19)
            return "Visa";
        if (digits.Length >= 2 && int.TryParse(digits[..2], out var prefix2) && prefix2 is >= 51 and <= 55)
            return "Mastercard";
        if (digits.Length >= 4 && int.TryParse(digits[..4], out var prefix4) && prefix4 is >= 2221 and <= 2720)
            return "Mastercard";
        return "Unknown Card";
    }

    public static bool IsValidLuhn(string digits)
    {
        if (digits.Length < 13 || digits.Length > 19 || !digits.All(char.IsDigit))
            return false;

        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    public static bool IsSupportedNetwork(string network) => network is "Visa" or "Mastercard";
}
