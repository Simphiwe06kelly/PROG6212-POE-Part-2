namespace RaceDay.Web.Services;

/// <summary>
/// The API remembers who is logged in using ITS session cookie. Because this MVC app calls the API from the
/// server, the browser never sees that cookie. This handler therefore:
///   1. saves the API's session cookie in the MVC user's own server-side session when the API sends one
///      (this happens when logging in), and
///   2. adds it to every later request to the API on behalf of that same user.
/// </summary>
public class ApiSessionCookieHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    private readonly string _cookieName;

    public ApiSessionCookieHandler(IHttpContextAccessor accessor, IConfiguration configuration)
    {
        _accessor = accessor;
        _cookieName = configuration["ApiSettings:SessionCookieName"] ?? ".AspNetCore.Session";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var session = _accessor.HttpContext?.Session;

        var saved = session?.GetString(UserSession.ApiCookieKey);
        if (!string.IsNullOrEmpty(saved))
        {
            request.Headers.TryAddWithoutValidation("Cookie", saved);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (session != null && response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                // "name=value; path=/; httponly" -> keep only "name=value"
                var pair = setCookie.Split(';', 2)[0].Trim();
                if (pair.StartsWith(_cookieName + "=", StringComparison.Ordinal))
                {
                    session.SetString(UserSession.ApiCookieKey, pair);
                }
            }
        }

        return response;
    }
}
