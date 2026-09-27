using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JamesThew.Models;

public class SubscriptionRequest
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public SubscriptionPlan Plan { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? AdminNotes { get; set; }

    public string? ReviewedByAdminId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }
}
