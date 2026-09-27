using System.Security.Claims;

namespace JamesThew.Authorization;

public static class ContentAccessHelper
{
    /// <summary>
    /// Evaluates whether the current principal can view paid (Members-Only) content.
    /// In Phase 2, Phase 3 membership approval has not been implemented yet.
    /// Therefore, ordinary Member accounts DO NOT grant paid access yet.
    /// Only Admins (editorial staff) can view paid content in this phase.
    /// </summary>
    public static bool CanViewPaidContent(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        // Admin role has full editorial preview and content inspection rights.
        if (user.IsInRole(AppRoles.Admin))
            return true;

        // Ordinary Member accounts cannot view paid content until Phase 3 demo subscription approval is implemented.
        return false;
    }
}
