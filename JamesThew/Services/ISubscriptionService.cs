using System.Security.Claims;
using JamesThew.Models;
using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface ISubscriptionService
{
    /// <summary>
    /// Checks whether the user has access to paid members-only content.
    /// Returns true if user is Admin, or if user is Member with an approved, unexpired subscription.
    /// </summary>
    Task<bool> CanAccessPaidContentAsync(ClaimsPrincipal? user);

    /// <summary>
    /// Gets the current/latest subscription status for a given user.
    /// </summary>
    Task<SubscriptionStatusDto?> GetCurrentSubscriptionStatusAsync(string userId);

    /// <summary>
    /// Gets the full subscription history for a given user.
    /// </summary>
    Task<List<SubscriptionHistoryItemDto>> GetSubscriptionHistoryAsync(string userId);

    /// <summary>
    /// Submits a new manual subscription request for a member.
    /// </summary>
    Task<SubscriptionRequestResult> SubmitRequestAsync(string userId, SubscriptionPlan plan, string? notes);

    /// <summary>
    /// Activates a subscription after successful simulated demo checkout.
    /// </summary>
    Task<SubscriptionRequestResult> ActivateDemoCheckoutAsync(string userId, SubscriptionPlan plan, string? safePaymentSummary = null);

    /// <summary>
    /// Gets all subscription requests for administrative review.
    /// </summary>
    Task<List<SubscriptionAdminListItemDto>> GetAllRequestsAsync(SubscriptionStatus? statusFilter = null);

    /// <summary>
    /// Approves a pending subscription request, granting paid access.
    /// </summary>
    Task<bool> ApproveRequestAsync(int id, string adminUserId, string? adminNotes = null);

    /// <summary>
    /// Rejects a pending subscription request with optional admin notes.
    /// </summary>
    Task<bool> RejectRequestAsync(int id, string adminUserId, string? adminNotes = null);

    /// <summary>
    /// Gets the count of pending subscription requests.
    /// </summary>
    Task<int> GetPendingCountAsync();
}
