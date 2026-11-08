namespace RaceDay.Web.Services;

/// <summary>The outcome of one call to the API: success or a friendly error message, never an exception.</summary>
public class ApiResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Error { get; init; }

    /// <summary>401: the API no longer knows this user (for example the API was restarted).</summary>
    public bool IsUnauthorized => StatusCode == 401;

    /// <summary>403: logged in, but the API says this user may not do that.</summary>
    public bool IsForbidden => StatusCode == 403;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}

public static class ApiResults
{
    public const string UnavailableMessage = "RaceDay is having trouble reaching its service right now. Please try again in a moment.";

    public static ApiResult Ok(int statusCode) => new() { Success = true, StatusCode = statusCode };

    public static ApiResult<T> Ok<T>(int statusCode, T? data) => new() { Success = true, StatusCode = statusCode, Data = data };

    public static ApiResult Fail(int statusCode, string error) => new() { Success = false, StatusCode = statusCode, Error = error };

    public static ApiResult<T> Fail<T>(int statusCode, string error) => new() { Success = false, StatusCode = statusCode, Error = error };

    public static ApiResult Down() => Fail(0, UnavailableMessage);

    public static ApiResult<T> Down<T>() => Fail<T>(0, UnavailableMessage);
}
