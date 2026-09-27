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
