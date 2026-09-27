namespace JamesThew.Authorization;

public static class AppPolicies
{
    // Account access is NOT proof of an active subscription or paid entitlement.
    public const string MemberAccount = "MemberAccount";
    public const string AdminOnly = "AdminOnly";
}
