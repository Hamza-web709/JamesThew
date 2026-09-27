using Microsoft.AspNetCore.Identity;

namespace JamesThew.Models;

// Identity owns credentials; membership subscription request lifecycle added in Phase 3A.
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public ICollection<SubscriptionRequest> SubscriptionRequests { get; set; } = [];
}

