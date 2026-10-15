using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Attributes;

/// <summary>
/// Requires a logged-in session. If roles are given, the user must have one of them.
/// No session = 401 Unauthorized. Wrong role = 403 Forbidden.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public RequireRoleAttribute(params string[] roles)
    {
        _roles = roles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetUserId();
        var role = session.GetRole();

        if (userId == null || role == null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "You must be logged in." });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(role))
        {
            context.Result = new ObjectResult(new { message = "You do not have permission to do this." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}