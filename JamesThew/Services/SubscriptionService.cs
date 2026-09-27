using System.Security.Claims;
using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class SubscriptionService(ApplicationDbContext db) : ISubscriptionService
{
    public static decimal GetPlanPrice(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Monthly => 10.00m,
        SubscriptionPlan.Yearly => 100.00m,
        _ => 10.00m
    };

    public async Task<bool> CanAccessPaidContentAsync(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        // Admin always has editorial inspection and preview rights
        if (user.IsInRole(AppRoles.Admin))
            return true;

        if (!user.IsInRole(AppRoles.Member))
            return false;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return false;

        var now = DateTime.UtcNow;
        return await db.SubscriptionRequests
            .AsNoTracking()
            .AnyAsync(s => s.UserId == userId
                           && s.Status == SubscriptionStatus.Approved
                           && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now));
    }

    public async Task<SubscriptionStatusDto?> GetCurrentSubscriptionStatusAsync(string userId)
    {
        var now = DateTime.UtcNow;

        // Check for active approved subscription first
        var active = await db.SubscriptionRequests
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Approved && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now))
            .OrderByDescending(s => s.ExpiresAtUtc)
            .FirstOrDefaultAsync();

        if (active != null)
        {
            return new SubscriptionStatusDto
            {
                HasActiveSubscription = true,
                Status = active.Status,
                Plan = active.Plan,
                Amount = active.Amount,
                Notes = active.Notes,
                ExpiresAtUtc = active.ExpiresAtUtc,
                AdminNotes = active.AdminNotes,
                CreatedAtUtc = active.CreatedAtUtc
            };
        }

        // Check latest request (could be pending or rejected)
        var latest = await db.SubscriptionRequests
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (latest == null)
            return null;

        return new SubscriptionStatusDto
        {
            HasActiveSubscription = false,
            Status = latest.Status,
            Plan = latest.Plan,
            Amount = latest.Amount,
            Notes = latest.Notes,
            ExpiresAtUtc = latest.ExpiresAtUtc,
            AdminNotes = latest.AdminNotes,
            CreatedAtUtc = latest.CreatedAtUtc
        };
    }

    public async Task<List<SubscriptionHistoryItemDto>> GetSubscriptionHistoryAsync(string userId)
    {
        return await db.SubscriptionRequests
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SubscriptionHistoryItemDto
            {
                Id = s.Id,
                Plan = s.Plan,
                Amount = s.Amount,
                Status = s.Status,
                Notes = s.Notes,
                CreatedAtUtc = s.CreatedAtUtc,
                ReviewedAtUtc = s.ReviewedAtUtc,
                ExpiresAtUtc = s.ExpiresAtUtc,
                AdminNotes = s.AdminNotes
            })
            .ToListAsync();
    }

    public async Task<SubscriptionRequestResult> SubmitRequestAsync(string userId, SubscriptionPlan plan, string? notes)
    {
        // Check if user already has an active or pending subscription
        var now = DateTime.UtcNow;
        var existingPending = await db.SubscriptionRequests
            .AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Pending);

        if (existingPending)
        {
            return new SubscriptionRequestResult
            {
                Success = false,
                Message = "You already have a subscription request pending administrator review."
            };
        }

        var amount = GetPlanPrice(plan);
        var request = new SubscriptionRequest
        {
            UserId = userId,
            Plan = plan,
            Amount = amount,
            Status = SubscriptionStatus.Pending,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = now
        };

        db.SubscriptionRequests.Add(request);
        await db.SaveChangesAsync();

        return new SubscriptionRequestResult
        {
            Success = true,
            Message = $"Your {plan} Demo Subscription request for ${amount:F2} has been submitted and is pending administrator review.",
            RequestId = request.Id
        };
    }

    public async Task<List<SubscriptionAdminListItemDto>> GetAllRequestsAsync(SubscriptionStatus? statusFilter = null)
    {
        var query = db.SubscriptionRequests
            .Include(s => s.User)
            .AsNoTracking()
            .AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(s => s.Status == statusFilter.Value);
        }

        return await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SubscriptionAdminListItemDto
            {
                Id = s.Id,
                UserId = s.UserId,
                UserDisplayName = s.User != null ? s.User.DisplayName : "Unknown",
                UserEmail = s.User != null ? s.User.Email ?? "" : "",
                Plan = s.Plan,
                Amount = s.Amount,
                Status = s.Status,
                Notes = s.Notes,
                AdminNotes = s.AdminNotes,
                ReviewedByAdminId = s.ReviewedByAdminId,
                CreatedAtUtc = s.CreatedAtUtc,
                ReviewedAtUtc = s.ReviewedAtUtc,
                ExpiresAtUtc = s.ExpiresAtUtc
            })
            .ToListAsync();
    }

    public async Task<bool> ApproveRequestAsync(int id, string adminUserId, string? adminNotes = null)
    {
        var request = await db.SubscriptionRequests.FirstOrDefaultAsync(s => s.Id == id);
        if (request == null || request.Status != SubscriptionStatus.Pending)
            return false;

        var now = DateTime.UtcNow;
        request.Status = SubscriptionStatus.Approved;
        request.ReviewedAtUtc = now;
        request.ReviewedByAdminId = adminUserId;
        request.AdminNotes = string.IsNullOrWhiteSpace(adminNotes) ? "Approved for academic demo access." : adminNotes.Trim();

        // Calculate expiration: 30 days for Monthly, 365 days for Yearly
        request.ExpiresAtUtc = request.Plan == SubscriptionPlan.Monthly
            ? now.AddDays(30)
            : now.AddDays(365);

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectRequestAsync(int id, string adminUserId, string? adminNotes = null)
    {
        var request = await db.SubscriptionRequests.FirstOrDefaultAsync(s => s.Id == id);
        if (request == null || request.Status != SubscriptionStatus.Pending)
            return false;

        var now = DateTime.UtcNow;
        request.Status = SubscriptionStatus.Rejected;
        request.ReviewedAtUtc = now;
        request.ReviewedByAdminId = adminUserId;
        request.AdminNotes = string.IsNullOrWhiteSpace(adminNotes) ? "Subscription request rejected." : adminNotes.Trim();
        request.ExpiresAtUtc = null;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await db.SubscriptionRequests
            .AsNoTracking()
            .CountAsync(s => s.Status == SubscriptionStatus.Pending);
    }
}
