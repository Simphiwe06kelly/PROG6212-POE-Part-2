using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// The logged-in user, kept in the MVC app's SERVER-SIDE session (user ID, role, name, picture).
/// The browser only holds an opaque session-ID cookie. Menus and page access are decided from this.
/// </summary>
public class UserSession
{
    public const string ApiCookieKey = "RaceDay.ApiCookie";

    private const string UserIdKey = "RaceDay.UserId";
    private const string RoleKey = "RaceDay.Role";
    private const string NameKey = "RaceDay.FullName";
    private const string PictureKey = "RaceDay.Picture";

    private readonly IHttpContextAccessor _accessor;

    public UserSession(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ISession Session =>
        _accessor.HttpContext?.Session ?? throw new InvalidOperationException("No active HTTP session.");

    public int? UserId => Session.GetInt32(UserIdKey);
    public string? Role => Session.GetString(RoleKey);
    public string FullName => Session.GetString(NameKey) ?? string.Empty;
    public string? PictureUrl => Session.GetString(PictureKey);

    public bool IsLoggedIn => UserId.HasValue && !string.IsNullOrEmpty(Role);
    public bool IsOrganiser => IsLoggedIn && Role == Roles.Organiser;
    public bool IsParticipant => IsLoggedIn && Role == Roles.Participant;

    public void SignIn(int userId, string role, string fullName, string? pictureUrl)
    {
        Session.SetInt32(UserIdKey, userId);
        Session.SetString(RoleKey, role);
        UpdateProfile(fullName, pictureUrl);
    }

    public void UpdateProfile(string fullName, string? pictureUrl)
    {
        Session.SetString(NameKey, fullName);
        if (string.IsNullOrEmpty(pictureUrl)) Session.Remove(PictureKey);
        else Session.SetString(PictureKey, pictureUrl);
    }

    /// <summary>Ends the session completely, including the saved API session cookie.</summary>
    public void Clear() => Session.Clear();
}
