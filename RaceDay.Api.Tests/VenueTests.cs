using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.Tests.Helpers;
using Xunit;

namespace RaceDay.Api.Tests;

public class VenueTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VenueTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static object ValidVenue(string? city = null) => new
    {
        venueName = "Test Stadium " + Guid.NewGuid().ToString("N")[..6],
        address = "1 Race Road",
        city = city ?? "Stellenbosch",
        province = "Western Cape"
    };

    [Fact]
    public async Task GetVenues_WithoutLoggingIn_ReturnsTheSeededVenues()
    {
        var anonymous = TestHelper.NewClient(_factory);

        var response = await anonymous.GetAsync("/api/venues");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var venues = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(venues.GetArrayLength() >= 3);
        Assert.Contains(venues.EnumerateArray(), v => v.GetProperty("venueName").GetString() == "Green Point Park");
    }

    [Fact]
    public async Task CreateVenue_AsOrganiser_Returns201_AndItAppearsInTheList()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/venues", ValidVenue("Paarl"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("venueID").GetInt32();
        Assert.Equal("Paarl", created.GetProperty("city").GetString());

        var list = await (await organiser.GetAsync("/api/venues")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(list.EnumerateArray(), v => v.GetProperty("venueID").GetInt32() == id);
    }

    [Fact]
    public async Task CreateVenue_AsParticipant_Returns403()
    {
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/venues", ValidVenue());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateVenue_WhenNotLoggedIn_Returns401()
    {
        var anonymous = TestHelper.NewClient(_factory);

        var response = await anonymous.PostAsJsonAsync("/api/venues", ValidVenue());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateVenue_WithoutACity_Returns400()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/venues", new { venueName = "No City Park", province = "Gauteng" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AtANewlyCreatedVenue_Works()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var venue = await (await organiser.PostAsJsonAsync("/api/venues", ValidVenue())).Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("venueID").GetInt32();

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "Venue Test Run",
            description = "Held at a brand new venue",
            eventDate = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd"),
            eventType = "Run",
            venueId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(venueId, created.GetProperty("venue").GetProperty("venueID").GetInt32());
    }
}
