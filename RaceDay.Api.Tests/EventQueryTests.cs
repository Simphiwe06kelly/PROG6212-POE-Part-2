using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.Tests.Helpers;
using Xunit;

namespace RaceDay.Api.Tests;

/// <summary>GET /api/events filters and GET /api/events/mine (the Organiser dashboard).</summary>
public class EventQueryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EventQueryTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<int> CreateVenueAsync(HttpClient organiser, string province)
    {
        var response = await organiser.PostAsJsonAsync("/api/venues", new
        {
            venueName = "Filter Venue " + Guid.NewGuid().ToString("N")[..6],
            city = "Filter City",
            province
        });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("venueID").GetInt32();
    }

    private static async Task<int> CreateEventAsync(HttpClient organiser, string name, string type, int venueId, int daysFromNow)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = name,
            description = "Created by a filter test",
            eventDate = DateTime.Today.AddDays(daysFromNow).ToString("yyyy-MM-dd"),
            eventType = type,
            venueId
        });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("eventID").GetInt32();
    }

    private static async Task<List<string>> NamesAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync("/api/events" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.EnumerateArray().Select(e => e.GetProperty("eventName").GetString()!).ToList();
    }

    // ---------- Filters ----------

    [Fact]
    public async Task Filters_NarrowTheEventList_AndCanBeCombined()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var token = Guid.NewGuid().ToString("N")[..10];
        var province = "Province-" + token;
        var venueId = await CreateVenueAsync(organiser, province);
        await CreateEventAsync(organiser, $"Alpha Walk {token}", "Walk", venueId, daysFromNow: 10);
        await CreateEventAsync(organiser, $"Beta Run {token}", "Run", venueId, daysFromNow: 40);
        var anonymous = TestHelper.NewClient(_factory);

        // text search matches the name
        Assert.Equal(2, (await NamesAsync(anonymous, $"?search={token}")).Count);

        // province filter
        Assert.Equal(2, (await NamesAsync(anonymous, $"?province={Uri.EscapeDataString(province)}")).Count);

        // type filter (case-insensitive)
        var walks = await NamesAsync(anonymous, $"?search={token}&type=walk");
        Assert.Equal(new[] { $"Alpha Walk {token}" }, walks);

        // combined: province + type
        var runs = await NamesAsync(anonymous, $"?province={Uri.EscapeDataString(province)}&type=Run");
        Assert.Equal(new[] { $"Beta Run {token}" }, runs);

        // date range: only the event 40 days away
        var from = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd");
        var later = await NamesAsync(anonymous, $"?search={token}&from={from}");
        Assert.Equal(new[] { $"Beta Run {token}" }, later);

        // date range: only the event 10 days away
        var to = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd");
        var sooner = await NamesAsync(anonymous, $"?search={token}&to={to}");
        Assert.Equal(new[] { $"Alpha Walk {token}" }, sooner);
    }

    [Fact]
    public async Task Filters_WithNoMatches_ReturnAnEmptyList()
    {
        var anonymous = TestHelper.NewClient(_factory);

        var names = await NamesAsync(anonymous, "?search=zzz-no-such-event-" + Guid.NewGuid().ToString("N"));

        Assert.Empty(names);
    }

    [Fact]
    public async Task EventList_StillReturnsEverythingUpcoming_WhenNoFilterIsGiven()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var venueId = await CreateVenueAsync(organiser, "Any Province");
        var name = "Unfiltered " + Guid.NewGuid().ToString("N")[..8];
        await CreateEventAsync(organiser, name, "Cycle", venueId, daysFromNow: 5);
        var anonymous = TestHelper.NewClient(_factory);

        Assert.Contains(name, await NamesAsync(anonymous, ""));
    }

    // ---------- Organiser dashboard: GET /api/events/mine ----------

    [Fact]
    public async Task Mine_ReturnsOnlyTheLoggedInOrganisersEvents_WithEnrolmentCounts()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var otherOrganiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);
        var categoryId = await TestHelper.CreateCategoryAsync(organiser, eventId);
        var otherEventId = await TestHelper.CreateEventAsync(otherOrganiser);

        var first = await TestHelper.LoginAsAsync(_factory, "Participant");
        var second = await TestHelper.LoginAsAsync(_factory, "Participant");
        await TestHelper.EnrolAsync(first, categoryId);
        var cancelledId = await TestHelper.EnrolAsync(second, categoryId);
        (await second.DeleteAsync($"/api/enrolments/{cancelledId}")).EnsureSuccessStatusCode(); // cancelled ones do not count

        var response = await organiser.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Contains(events, e => e.GetProperty("eventID").GetInt32() == eventId);
        Assert.DoesNotContain(events, e => e.GetProperty("eventID").GetInt32() == otherEventId);
        var mine = events.Single(e => e.GetProperty("eventID").GetInt32() == eventId);
        Assert.Equal(1, mine.GetProperty("enrolmentCount").GetInt32());
    }

    [Fact]
    public async Task Mine_IncludesPastEvents_ThatTheBrowseListHides()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var pastEventId = await TestHelper.CreateEventAsync(organiser, DateTime.Today.AddDays(-10));

        var mine = (await (await organiser.GetAsync("/api/events/mine")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray();
        var browse = (await (await TestHelper.NewClient(_factory).GetAsync("/api/events")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray();

        Assert.Contains(mine, e => e.GetProperty("eventID").GetInt32() == pastEventId);
        Assert.DoesNotContain(browse, e => e.GetProperty("eventID").GetInt32() == pastEventId);
    }

    [Fact]
    public async Task Mine_AsParticipant_Returns403()
    {
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mine_WhenNotLoggedIn_Returns401()
    {
        var anonymous = TestHelper.NewClient(_factory);

        var response = await anonymous.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
