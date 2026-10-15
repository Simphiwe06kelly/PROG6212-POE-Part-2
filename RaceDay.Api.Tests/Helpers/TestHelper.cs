using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RaceDay.Api.Tests.Helpers;

public static class TestHelper
{
    public const string Password = "Password123";

    /// <summary>A new client with its own cookie jar (so its own session).</summary>
    public static HttpClient NewClient(CustomWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    public static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@test.com";

    /// <summary>Registers a brand-new user with the role, logs them in, and returns the logged-in client.</summary>
    public static async Task<HttpClient> LoginAsAsync(CustomWebApplicationFactory factory, string role)
    {
        var client = NewClient(factory);
        var email = UniqueEmail(role.ToLower());

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Test " + role, email, password = Password, role });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        return client;
    }

    public static async Task<int> CreateEventAsync(HttpClient organiser, DateTime? date = null)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "Test Event " + Guid.NewGuid().ToString("N")[..6],
            description = "Created by a unit test",
            eventDate = (date ?? DateTime.Today.AddDays(30)).ToString("yyyy-MM-dd"),
            eventType = "Run",
            venueId = 1
        });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("eventID").GetInt32();
    }

    public static async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId, int maxParticipants = 100)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            categoryName = "10km",
            distanceKm = 10,
            entryFee = 100,
            maxParticipants
        });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("categoryID").GetInt32();
    }

    public static async Task<int> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("enrolmentID").GetInt32();
    }
}