using System.ComponentModel.DataAnnotations;

namespace JamesThew.ViewModels;

public class AdminUsersIndexViewModel
{
    public IReadOnlyList<AdminUserListItemViewModel> Users { get; set; } = [];
    public string? SearchQuery { get; set; }
    public string RoleFilter { get; set; } = "all";
    public string VerificationFilter { get; set; } = "all";
    public string MembershipFilter { get; set; } = "all";
    public string StatusFilter { get; set; } = "all";

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
    public bool IsActive { get; set; } = true;
    public string MembershipLabel { get; set; } = string.Empty;
    public bool IsTemporarilyLocked { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}

public class AdminUserDetailsViewModel
{
    // Identity
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public bool IsActive { get; set; } = true;

    // Security
    public bool IsTemporarilyLocked { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; }

    // Membership
    public string MembershipLabel { get; set; } = string.Empty;
    public string CurrentPlan { get; set; } = "Free";
    public bool IsMembershipActive { get; set; }
    public DateTime? MembershipExpiry { get; set; }
    public int SubscriptionHistoryCount { get; set; }
    public string? LatestSubscriptionStatus { get; set; }
    public DateTime? LastApprovedSubscriptionDate { get; set; }

    // Activity Summary
    public int RecipeContributionCount { get; set; }
    public int TipContributionCount { get; set; }
    public int ContestEntryCount { get; set; }
    public int WinningContestEntryCount { get; set; }
    public int FeedbackCount { get; set; }

    // Safe Navigation
    public string ReturnUrl { get; set; } = "/admin/users";

    // Governance / Role Management
    public bool IsCurrentAdmin { get; set; }
    public bool IsDemoAdmin { get; set; }
    public bool IsOnlyAdmin { get; set; }
}

public class AdminUserEditViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display Name is required and cannot be empty or whitespace.")]
    [StringLength(100, ErrorMessage = "Display Name cannot exceed 100 characters.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(256, ErrorMessage = "Email address cannot exceed 256 characters.")]
    public string Email { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    // Read-only context for display
    public string CurrentRole { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public bool IsDemoAdmin { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AdminUserChangeRoleViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Target role is required.")]
    public string TargetRole { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
