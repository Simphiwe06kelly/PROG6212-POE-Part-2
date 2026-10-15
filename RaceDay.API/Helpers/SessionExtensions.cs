namespace RaceDay.Api.Helpers;

public static class RaceDaySession
{
    public const string UserIdKey = "UserID";
    public const string RoleKey = "Role";

    public static int? GetUserId(this ISession session) => session.GetInt32(UserIdKey);

    public static string? GetRole(this ISession session) => session.GetString(RoleKey);
}