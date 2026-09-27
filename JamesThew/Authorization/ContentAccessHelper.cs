using System.Security.Claims;

namespace JamesThew.Authorization;

public static class ContentAccessHelper
{
    /// <summary>
    /// Evaluates whether the current principal can view paid (Members-Only) content.
    /// In Phase 3A:
    /// - Admins always have access.
    /// - Logged-in Members have access ONLY if they have an active approved subscription.
    /// - Unapproved, pending, rejected members and guests cannot access paid content.
    /// </summary>
    public static bool CanViewPaidContent(ClaimsPrincipal? user, bool hasActiveSubscription = false)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        // Admin role has full editorial preview and content inspection rights.
        if (user.IsInRole(AppRoles.Admin))
            return true;

        // Ordinary Member accounts can only view paid content if their subscription is approved and active.
        if (user.IsInRole(AppRoles.Member) && hasActiveSubscription)
            return true;

        return false;
    }
}
