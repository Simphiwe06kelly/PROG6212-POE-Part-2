using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared helpers for every controller: the API client, the logged-in user and flash messages.</summary>
public abstract class AppController : Controller
{
    protected AppController(IRaceDayApi api, UserSession currentUser)
    {
        Api = api;
        CurrentUser = currentUser;
    }

    protected IRaceDayApi Api { get; }
    protected UserSession CurrentUser { get; }

    protected void ShowSuccess(string message) => TempData["Success"] = message;

    protected void ShowError(string message) => TempData["Error"] = message;

    /// <summary>
    /// If the API says the user is no longer logged in (401), end the web session and go to the login page.
    /// If it says the user is not allowed (403), go to the access-denied page. Otherwise returns null.
    /// </summary>
    protected IActionResult? RedirectIfNotAllowed(ApiResult result)
    {
        if (result.IsUnauthorized)
        {
            CurrentUser.Clear();
            ShowError("Your session has expired. Please log in again.");
            return RedirectToAction("Login", "Account");
        }

        if (result.IsForbidden)
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        return null;
    }

    /// <summary>All validation messages of the current form as one sentence.</summary>
    protected string ModelErrors() =>
        string.Join(" ", ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m)));
}
