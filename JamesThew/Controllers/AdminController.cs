using System.Security.Claims;
using JamesThew.Authorization;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
[Route("admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminController(
    ISubscriptionService subscriptionService,
    IFeedbackService feedbackService,
    IContributionService contributionService,
    IAdminContentService adminContentService,
    IMediaService mediaService,
    IContestService contestService,
    IContestEntryService contestEntryService,
    IContestJudgingService contestJudgingService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.PendingSubscriptionsCount = await subscriptionService.GetPendingCountAsync();
        ViewBag.PendingFeedbackCount = await feedbackService.GetPendingFeedbackCountAsync();
        ViewBag.PendingContributionsCount = await contributionService.GetPendingContributionCountAsync();
        var editorialData = await adminContentService.GetEditorialContentListAsync();
        ViewBag.EditorialRecipesCount = editorialData.RecipesCount;
        ViewBag.EditorialTipsCount = editorialData.TipsCount;
        var mediaData = await mediaService.GetMediaLibraryAsync();
        ViewBag.MediaItemsCount = mediaData.TotalCount;
        ViewBag.MediaTotalSize = mediaData.FormattedTotalSize;
        ViewBag.TotalContestsCount = await contestService.GetTotalContestsCountAsync();
        ViewBag.ActiveOpenContestsCount = await contestService.GetActiveOpenCountAsync();
        return View();
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> Subscriptions(SubscriptionStatus? status)
    {
        var requests = await subscriptionService.GetAllRequestsAsync(status);
        var pendingCount = await subscriptionService.GetPendingCountAsync();
        var model = new SubscriptionAdminListViewModel
        {
            StatusFilter = status,
            TotalPendingCount = pendingCount,
            Requests = requests
        };
        return View(model);
    }

    [HttpPost("subscriptions/{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSubscription(int id, string? adminNotes)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var success = await subscriptionService.ApproveRequestAsync(id, adminUserId, adminNotes);
        if (success)
            TempData["SuccessMessage"] = $"Subscription request #{id} successfully approved. Paid masterclass access is now active.";
        else
            TempData["ErrorMessage"] = $"Unable to approve subscription request #{id}.";

        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost("subscriptions/{id:int}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectSubscription(int id, string? adminNotes)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var success = await subscriptionService.RejectRequestAsync(id, adminUserId, adminNotes);
        if (success)
            TempData["SuccessMessage"] = $"Subscription request #{id} has been rejected.";
        else
            TempData["ErrorMessage"] = $"Unable to reject subscription request #{id}.";

        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpGet("feedback")]
    public async Task<IActionResult> Feedback(FeedbackStatus? status)
    {
        var model = await feedbackService.GetAdminFeedbackListAsync(status);
        return View(model);
    }

    [HttpPost("feedback/{id:int}/moderate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModerateFeedback(int id, FeedbackStatus status, string? adminNotes)
    {
        var (success, message) = await feedbackService.ModerateFeedbackAsync(id, status, adminNotes);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Feedback));
    }

    [HttpGet("contributions")]
    public async Task<IActionResult> Contributions(ContentKind? kind, PublicationStatus? status)
    {
        var model = await contributionService.GetAdminContributionListAsync(kind, status);
        return View(model);
    }

    [HttpPost("contributions/{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveContribution(int id, ContentVisibility visibility = ContentVisibility.Free)
    {
        var (success, message) = await contributionService.ApproveContributionAsync(id, visibility);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contributions));
    }

    [HttpPost("contributions/{id:int}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectContribution(int id, string? rejectionReason)
    {
        var (success, message) = await contributionService.RejectContributionAsync(id, rejectionReason);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contributions));
    }

    // ==========================================
    // Phase 4: Editorial Content CRUD
    // ==========================================

    [HttpGet("content")]
    public async Task<IActionResult> Content(ContentKind? kind, ContentVisibility? visibility, PublicationStatus? status, bool showDeleted = false)
    {
        var model = await adminContentService.GetEditorialContentListAsync(kind, visibility, status, showDeleted);
        return View(model);
    }

    [HttpGet("content/recipes/new")]
    public IActionResult NewRecipe()
    {
        return View("RecipeForm", new AdminRecipeEditViewModel());
    }

    [HttpPost("content/recipes/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewRecipe(AdminRecipeEditViewModel model)
    {
        string? newlyUploadedFile = null;

        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            if (!ModelState.IsValid)
                return View("RecipeForm", model);

            var uploadResult = await mediaService.UploadImageAsync(model.ImageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError("ImageFile", uploadResult.Message);
                return View("RecipeForm", model);
            }
            model.ImageUrl = uploadResult.Url;
            newlyUploadedFile = uploadResult.FileName;
        }
        else if (model.RemoveImage)
        {
            model.ImageUrl = null;
        }

        if (!ModelState.IsValid)
            return View("RecipeForm", model);

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminDisplayName = User.Identity?.Name ?? "James Thew";

        var (success, message, id, slug) = await adminContentService.SaveRecipeAsync(model, adminUserId, adminDisplayName);
        if (!success)
        {
            if (!string.IsNullOrEmpty(newlyUploadedFile))
            {
                await mediaService.DeleteMediaAsync(newlyUploadedFile);
            }
            ModelState.AddModelError(string.Empty, message);
            return View("RecipeForm", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Content), new { kind = ContentKind.Recipe });
    }

    [HttpGet("content/recipes/{id:int}/edit")]
    public async Task<IActionResult> EditRecipe(int id)
    {
        var model = await adminContentService.GetRecipeForEditAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = $"Editorial recipe #{id} was not found.";
            return RedirectToAction(nameof(Content));
        }

        return View("RecipeForm", model);
    }

    [HttpPost("content/recipes/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRecipe(int id, AdminRecipeEditViewModel model)
    {
        model.Id = id;
        string? newlyUploadedFile = null;

        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            if (!ModelState.IsValid)
                return View("RecipeForm", model);

            var uploadResult = await mediaService.UploadImageAsync(model.ImageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError("ImageFile", uploadResult.Message);
                return View("RecipeForm", model);
            }
            model.ImageUrl = uploadResult.Url;
            newlyUploadedFile = uploadResult.FileName;
        }
        else if (model.RemoveImage)
        {
            model.ImageUrl = null;
        }

        if (!ModelState.IsValid)
            return View("RecipeForm", model);

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminDisplayName = User.Identity?.Name ?? "James Thew";

        var (success, message, _, _) = await adminContentService.SaveRecipeAsync(model, adminUserId, adminDisplayName);
        if (!success)
        {
            if (!string.IsNullOrEmpty(newlyUploadedFile))
            {
                await mediaService.DeleteMediaAsync(newlyUploadedFile);
            }
            ModelState.AddModelError(string.Empty, message);
            return View("RecipeForm", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Content), new { kind = ContentKind.Recipe });
    }

    [HttpGet("content/tips/new")]
    public IActionResult NewTip()
    {
        return View("TipForm", new AdminTipEditViewModel());
    }

    [HttpPost("content/tips/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewTip(AdminTipEditViewModel model)
    {
        string? newlyUploadedFile = null;

        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            if (!ModelState.IsValid)
                return View("TipForm", model);

            var uploadResult = await mediaService.UploadImageAsync(model.ImageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError("ImageFile", uploadResult.Message);
                return View("TipForm", model);
            }
            model.ImageUrl = uploadResult.Url;
            newlyUploadedFile = uploadResult.FileName;
        }
        else if (model.RemoveImage)
        {
            model.ImageUrl = null;
        }

        if (!ModelState.IsValid)
            return View("TipForm", model);

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminDisplayName = User.Identity?.Name ?? "James Thew";

        var (success, message, id, slug) = await adminContentService.SaveTipAsync(model, adminUserId, adminDisplayName);
        if (!success)
        {
            if (!string.IsNullOrEmpty(newlyUploadedFile))
            {
                await mediaService.DeleteMediaAsync(newlyUploadedFile);
            }
            ModelState.AddModelError(string.Empty, message);
            return View("TipForm", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Content), new { kind = ContentKind.Tip });
    }

    [HttpGet("content/tips/{id:int}/edit")]
    public async Task<IActionResult> EditTip(int id)
    {
        var model = await adminContentService.GetTipForEditAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = $"Editorial tip #{id} was not found.";
            return RedirectToAction(nameof(Content));
        }

        return View("TipForm", model);
    }

    [HttpPost("content/tips/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTip(int id, AdminTipEditViewModel model)
    {
        model.Id = id;
        string? newlyUploadedFile = null;

        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            if (!ModelState.IsValid)
                return View("TipForm", model);

            var uploadResult = await mediaService.UploadImageAsync(model.ImageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError("ImageFile", uploadResult.Message);
                return View("TipForm", model);
            }
            model.ImageUrl = uploadResult.Url;
            newlyUploadedFile = uploadResult.FileName;
        }
        else if (model.RemoveImage)
        {
            model.ImageUrl = null;
        }

        if (!ModelState.IsValid)
            return View("TipForm", model);

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminDisplayName = User.Identity?.Name ?? "James Thew";

        var (success, message, _, _) = await adminContentService.SaveTipAsync(model, adminUserId, adminDisplayName);
        if (!success)
        {
            if (!string.IsNullOrEmpty(newlyUploadedFile))
            {
                await mediaService.DeleteMediaAsync(newlyUploadedFile);
            }
            ModelState.AddModelError(string.Empty, message);
            return View("TipForm", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Content), new { kind = ContentKind.Tip });
    }

    [HttpPost("content/{id:int}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveContent(int id)
    {
        var (success, message) = await adminContentService.SoftDeleteContentAsync(id);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Content));
    }

    [HttpPost("content/{id:int}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreContent(int id)
    {
        var (success, message) = await adminContentService.RestoreContentAsync(id);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Content));
    }

    // ==========================================
    // Phase 4 Step 2: Media Management
    // ==========================================

    [HttpGet("media")]
    public async Task<IActionResult> Media(string? search)
    {
        var model = await mediaService.GetMediaLibraryAsync(search);
        return View(model);
    }

    [HttpPost("media/upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadMedia([FromForm] MediaUploadInputModel input)
    {
        if (input.File == null || input.File.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select an image file to upload.";
            return RedirectToAction(nameof(Media));
        }

        var (success, message, url, fileName) = await mediaService.UploadImageAsync(input.File);
        if (success)
        {
            TempData["SuccessMessage"] = $"Image '{fileName}' successfully uploaded.";
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Media));
    }

    [HttpPost("media/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMedia([FromForm] string fileName)
    {
        var (success, message, _) = await mediaService.DeleteMediaAsync(fileName);
        if (success)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Media));
    }

    [HttpGet("media/picker")]
    public async Task<IActionResult> MediaPicker(string? search)
    {
        var items = await mediaService.GetPickerMediaAsync(search);
        return Json(new { items });
    }

    // ==========================================
    // Phase 5A: Contest Management
    // ==========================================

    [HttpGet("contests")]
    public async Task<IActionResult> Contests(ContestStatus? status, ContestType? type, bool showArchived = false)
    {
        var model = await contestService.GetAdminContestsAsync(status, type, showArchived);
        return View(model);
    }

    [HttpGet("contests/new")]
    public async Task<IActionResult> NewContest()
    {
        var availableMedia = await mediaService.GetPickerMediaAsync();
        var model = new AdminContestEditViewModel
        {
            OpensAt = DateTime.UtcNow,
            ClosesAt = DateTime.UtcNow.AddDays(30),
            AvailableMedia = availableMedia
        };
        return View("ContestEditor", model);
    }

    [HttpPost("contests/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContest(AdminContestEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableMedia = await mediaService.GetPickerMediaAsync();
            return View("ContestEditor", model);
        }

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var (success, message, contestId) = await contestService.CreateContestAsync(model, adminUserId);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            model.AvailableMedia = await mediaService.GetPickerMediaAsync();
            return View("ContestEditor", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Contests));
    }

    [HttpGet("contests/{id:int}/edit")]
    public async Task<IActionResult> EditContest(int id)
    {
        var model = await contestService.GetAdminContestByIdAsync(id);
        if (model == null)
        {
            return NotFound();
        }

        model.AvailableMedia = await mediaService.GetPickerMediaAsync();
        return View("ContestEditor", model);
    }

    [HttpPost("contests/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateContest(int id, AdminContestEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.AvailableMedia = await mediaService.GetPickerMediaAsync();
            return View("ContestEditor", model);
        }

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var (success, message) = await contestService.UpdateContestAsync(model, adminUserId);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            model.AvailableMedia = await mediaService.GetPickerMediaAsync();
            return View("ContestEditor", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishContest(int id)
    {
        var (success, message) = await contestService.ChangeStatusAsync(id, ContestStatus.Published);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/unpublish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpublishContest(int id)
    {
        var (success, message) = await contestService.ChangeStatusAsync(id, ContestStatus.Draft);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseContest(int id)
    {
        var (success, message) = await contestService.ChangeStatusAsync(id, ContestStatus.Closed);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveContest(int id)
    {
        var (success, message) = await contestService.ArchiveContestAsync(id);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreContest(int id)
    {
        var (success, message) = await contestService.RestoreContestAsync(id);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpGet("contests/{id:int}/entries")]
    public async Task<IActionResult> ContestEntries(int id)
    {
        var model = await contestEntryService.GetAdminContestEntriesAsync(id);
        if (model == null)
            return NotFound();

        return View(model);
    }

    [HttpPost("contests/{id:int}/entries/{entryId:int}/review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewContestEntry(int id, int entryId, ReviewContestEntryInputModel input)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Challenge();

        var (success, message) = await contestJudgingService.ReviewEntryAsync(
            id,
            entryId,
            adminUserId,
            input.Status,
            input.AdminReviewNotes,
            input.DisqualificationReason);

        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(ContestEntries), new { id });
    }

    [HttpPost("contests/{id:int}/entries/{entryId:int}/select-winner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectContestWinner(int id, int entryId)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Challenge();

        var (success, message) = await contestJudgingService.SelectWinnerAsync(id, entryId, adminUserId);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(ContestEntries), new { id });
    }

    [HttpPost("contests/{id:int}/announce-winner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnnounceContestWinner(int id)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Challenge();

        var (success, message) = await contestJudgingService.AnnounceWinnerAsync(id, adminUserId);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(ContestEntries), new { id });
    }

    [HttpPost("contests/{id:int}/revoke-winner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeContestWinner(int id, string? reason)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Challenge();

        var (success, message) = await contestJudgingService.RevokeWinnerAsync(id, adminUserId, reason);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(ContestEntries), new { id });
    }
}
