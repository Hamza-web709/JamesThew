namespace JamesThew.ViewModels;

public class AdminUsersIndexViewModel
{
    public IReadOnlyList<AdminUserListItemViewModel> Users { get; set; } = [];
    public string? SearchQuery { get; set; }
    public string RoleFilter { get; set; } = "all";
    public string VerificationFilter { get; set; } = "all";
    public string MembershipFilter { get; set; } = "all";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalItems { get; set; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    // Summary counters
    public int TotalUsersCount { get; set; }
    public int TotalMembersCount { get; set; }
    public int TotalAdminsCount { get; set; }
    public int TotalVerifiedCount { get; set; }
    public int TotalActiveMembershipsCount { get; set; }
}

public class AdminUserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string MembershipLabel { get; set; } = string.Empty;
    public bool IsTemporarilyLocked { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}
