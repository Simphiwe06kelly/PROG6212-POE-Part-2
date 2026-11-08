using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    public AccountController(IRaceDayApi api, UserSession currentUser) : base(api, currentUser)
    {
    }

    // ---------------- Register ----------------

    [HttpGet]
    public IActionResult Register()
    {
        if (CurrentUser.IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await Api.RegisterAsync(new RegisterRequest(model.FullName.Trim(), model.Email.Trim(), model.Password, model.Role));

        if (!result.Success)
        {
            // "email already registered" belongs next to the email box; anything else goes at the top
            ModelState.AddModelError(result.StatusCode == 409 ? nameof(model.Email) : string.Empty,
                result.Error ?? "We couldn't create your account. Please try again.");
            return View(model);
        }

        ShowSuccess("Your account has been created. Please log in.");
        return RedirectToAction(nameof(Login));
    }

    // ---------------- Login / logout ----------------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (CurrentUser.IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Start from a clean session (this also forgets any old API session cookie)
        CurrentUser.Clear();

        var login = await Api.LoginAsync(model.Email.Trim(), model.Password);
        if (!login.Success || login.Data == null)
        {
            ModelState.AddModelError(string.Empty,
                login.IsUnauthorized ? "Incorrect email or password." : login.Error ?? "We couldn't log you in. Please try again.");
            return View(model);
        }

        // The API has started its session (its cookie is saved by ApiSessionCookieHandler).
        // Now fetch the profile so the menu can show the name and picture.
        var profile = await Api.GetProfileAsync();
        CurrentUser.SignIn(login.Data.UserId, login.Data.Role, profile.Data?.FullName ?? model.Email.Trim(), profile.Data?.ProfilePictureUrl);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return login.Data.Role == Roles.Organiser
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (CurrentUser.IsLoggedIn)
        {
            await Api.LogoutAsync();   // ends the API's session too
        }

        CurrentUser.Clear();           // ends this app's session
        ShowSuccess("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    // ---------------- Profile (both roles) ----------------

    [SessionAuthorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var result = await Api.GetProfileAsync();
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success || result.Data == null)
        {
            ShowError(result.Error ?? "We couldn't load your profile.");
            return RedirectToAction("Index", "Home");
        }

        return View(ToProfile(result.Data));
    }

    [SessionAuthorize]
    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Role = CurrentUser.Role ?? string.Empty;
            model.PictureUrl = CurrentUser.PictureUrl;
            return View(model);
        }

        var result = await Api.UpdateProfileAsync(model.FullName.Trim(), model.Email.Trim());
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(result.StatusCode == 409 ? nameof(model.Email) : string.Empty,
                result.Error ?? "We couldn't save your changes.");
            model.Role = CurrentUser.Role ?? string.Empty;
            model.PictureUrl = CurrentUser.PictureUrl;
            return View(model);
        }

        CurrentUser.UpdateProfile(result.Data.FullName, result.Data.ProfilePictureUrl);
        ShowSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    [SessionAuthorize]
    [HttpPost]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture == null || picture.Length == 0)
        {
            ShowError("Please choose a picture to upload.");
            return RedirectToAction(nameof(Profile));
        }

        // The file goes to the API, which stores it in Azure Blob Storage
        var result = await Api.UploadProfilePictureAsync(picture);
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success || result.Data == null)
        {
            ShowError(result.Error ?? "We couldn't upload that picture.");
        }
        else
        {
            CurrentUser.UpdateProfile(result.Data.FullName, result.Data.ProfilePictureUrl);
            ShowSuccess("Your profile picture has been updated.");
        }

        return RedirectToAction(nameof(Profile));
    }

    private static ProfileViewModel ToProfile(UserDto user) => new()
    {
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role,
        PictureUrl = user.ProfilePictureUrl,
        MemberSince = user.CreatedAt
    };
}
