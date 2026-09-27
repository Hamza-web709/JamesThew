using Microsoft.AspNetCore.Identity;

namespace JamesThew.Models;

// Identity owns credentials; membership/payment state belongs to a later phase.
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
