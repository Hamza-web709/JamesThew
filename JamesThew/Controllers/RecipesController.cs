using JamesThew.Authorization;
using JamesThew.Models;
using JamesThew.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

[AllowAnonymous]
public class RecipesController(IContentService contentService) : Controller
{
    [HttpGet("recipes")]
    public async Task<IActionResult> Index(string? q, ContentVisibility? visibility, int page = 1)
    {
        var canViewPaid = ContentAccessHelper.CanViewPaidContent(User);
        var model = await contentService.SearchRecipesAsync(q, visibility, page, 12, canViewPaid);
        return View(model);
    }

    [HttpGet("recipes/{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        var canViewPaid = ContentAccessHelper.CanViewPaidContent(User);
        var recipe = await contentService.GetRecipeBySlugAsync(slug, canViewPaid);
        if (recipe is null)
            return NotFound();

        // Never allow public or shared caches to store protected members-only content states.
        if (recipe.Visibility == ContentVisibility.MembersOnly)
        {
            Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
        }

        return View(recipe);
    }
}
