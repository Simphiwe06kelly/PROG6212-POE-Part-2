using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Tests.Helpers;
using Xunit;

namespace RaceDay.Api.Tests;

public class EventTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EventTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static object ValidEvent(string name = "Valid Event") => new
    {
        eventName = name,
        description = "A test event",
        eventDate = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd"),
        eventType = "Run",
        venueId = 1
    };

    [Fact]
    public async Task GetEvents_Anonymous_Returns200()
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_Seeded_Returns200()
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.GetAsync("/api/events/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_Missing_Returns404()
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.GetAsync("/api/events/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsOrganiser_Returns201()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", ValidEvent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_Returns400()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "Bad Type",
            description = "x",
            eventDate = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
            eventType = "Swim",
            venueId = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_UnknownVenue_Returns400()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "No Venue",
            description = "x",
            eventDate = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
            eventType = "Run",
            venueId = 9999
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_Anonymous_Returns401()
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.PostAsJsonAsync("/api/events", ValidEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_Returns403()
    {
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/events", ValidEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_OwnEvent_Returns200()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{eventId}", ValidEvent("Renamed Event"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Renamed Event", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateEvent_OtherOrganisersEvent_Returns403()
    {
        var owner = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(owner);
        var otherOrganiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await otherOrganiser.PutAsJsonAsync($"/api/events/{eventId}", ValidEvent("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_Missing_Returns404()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await organiser.PutAsJsonAsync("/api/events/99999", ValidEvent());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_OwnEvent_Returns204_ThenEventIsGone()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{eventId}");
        var get = await organiser.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_OtherOrganisersEvent_Returns403()
    {
        var owner = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(owner);
        var otherOrganiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await otherOrganiser.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsParticipant_Returns403()
    {
        var owner = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(owner);
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_ToOwnEvent_Returns201_AndOtherOrganiserGets403()
    {
        var owner = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(owner);
        var body = new { categoryName = "5km", distanceKm = 5, entryFee = 50, maxParticipants = 100 };

        var ok = await owner.PostAsJsonAsync($"/api/events/{eventId}/categories", body);

        var other = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var forbidden = await other.PostAsJsonAsync($"/api/events/{eventId}/categories", body);

        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}