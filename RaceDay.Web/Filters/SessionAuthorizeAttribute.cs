using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects a page using the MVC app's server-side session.
///   [SessionAuthorize]                       any logged-in user
///   [SessionAuthorize(Roles.Organiser)]      Organisers only
/// Not logged in => sent to the login page (and brought back afterwards). Wrong role => "access denied" page.
/// (The API checks the role again on every call, so this is the friendly front door, not the only lock.)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public SessionAuthorizeAttribute(params string[] roles)
    {
        _roles = roles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<UserSession>();

        if (!user.IsLoggedIn)
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Method == HttpMethods.Get ? request.Path + request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(user.Role))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
