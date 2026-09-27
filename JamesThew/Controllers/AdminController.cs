using JamesThew.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JamesThew.Controllers;

// A minimal protected landing page proves the role boundary; no business workflows yet.
[Authorize(Policy = AppPolicies.AdminOnly)]
[Route("admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
