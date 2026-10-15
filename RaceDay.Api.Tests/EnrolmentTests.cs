using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.Tests.Helpers;
using Xunit;

namespace RaceDay.Api.Tests;

public class EnrolmentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EnrolmentTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient organiser, int eventId, int categoryId)> SetUpEventAsync(
        int maxParticipants = 100, DateTime? date = null)
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser, date);
        var categoryId = await TestHelper.CreateCategoryAsync(organiser, eventId, maxParticipants);
        return (organiser, eventId, categoryId);
    }

    [Fact]
    public async Task Enrol_AsParticipant_Returns201_AndIsRecorded()
    {
        var (_, eventId, categoryId) = await SetUpEventAsync();
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(categoryId, created.GetProperty("categoryID").GetInt32());
        Assert.Equal(eventId, created.GetProperty("eventID").GetInt32());
        Assert.Equal("Confirmed", created.GetProperty("status").GetString());

        // The enrolment appears in the participant's own history
        var mine = await participant.GetAsync("/api/enrolments/mine");
        var list = await mine.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Contains(list.EnumerateArray(), e => e.GetProperty("categoryID").GetInt32() == categoryId);
    }

    [Fact]
    public async Task Enrol_SameCategoryTwice_Returns409()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");
        await TestHelper.EnrolAsync(participant, categoryId);

        var second = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_Returns404()
    {
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = 99999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InvalidCategoryId_Returns400()
    {
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_EventAlreadyTookPlace_Returns400()
    {
        var (_, _, categoryId) = await SetUpEventAsync(date: DateTime.Today.AddDays(-5));
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_FullCategory_Returns400()
    {
        var (_, _, categoryId) = await SetUpEventAsync(maxParticipants: 1);
        var first = await TestHelper.LoginAsAsync(_factory, "Participant");
        var second = await TestHelper.LoginAsAsync(_factory, "Participant");
        await TestHelper.EnrolAsync(first, categoryId);

        var response = await second.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_AsOrganiser_Returns403()
    {
        var (organiser, _, categoryId) = await SetUpEventAsync();

        var response = await organiser.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_OwnEnrolment_Returns204_AndStatusBecomesCancelled()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");
        var enrolmentId = await TestHelper.EnrolAsync(participant, categoryId);

        var cancel = await participant.DeleteAsync($"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var mine = await participant.GetFromJsonAsync<JsonElement>("/api/enrolments/mine");
        var enrolment = mine.EnumerateArray()
            .First(e => e.GetProperty("enrolmentID").GetInt32() == enrolmentId);
        Assert.Equal("Cancelled", enrolment.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancel_AfterCancelling_CanEnrolAgain()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");
        var enrolmentId = await TestHelper.EnrolAsync(participant, categoryId);
        await participant.DeleteAsync($"/api/enrolments/{enrolmentId}");

        var again = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Cancel_SomeoneElsesEnrolment_Returns403()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var owner = await TestHelper.LoginAsAsync(_factory, "Participant");
        var enrolmentId = await TestHelper.EnrolAsync(owner, categoryId);
        var stranger = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await stranger.DeleteAsync($"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_MissingEnrolment_Returns404()
    {
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.DeleteAsync("/api/enrolments/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Roster_OwningOrganiser_Returns200_WithTheEnrolment()
    {
        var (organiser, eventId, categoryId) = await SetUpEventAsync();
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");
        var enrolmentId = await TestHelper.EnrolAsync(participant, categoryId);

        var response = await organiser.GetAsync($"/api/events/{eventId}/enrolments");
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(list.EnumerateArray(), e => e.GetProperty("enrolmentID").GetInt32() == enrolmentId);
    }

    [Fact]
    public async Task Roster_OtherOrganiser_Returns403()
    {
        var (_, eventId, _) = await SetUpEventAsync();
        var otherOrganiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await otherOrganiser.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Results_OrganiserCapturesResult_ParticipantSeesIt()
    {
        var (organiser, _, categoryId) = await SetUpEventAsync();
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");
        var enrolmentId = await TestHelper.EnrolAsync(participant, categoryId);

        var capture = await organiser.PostAsJsonAsync("/api/results", new
        {
            enrolmentId,
            finishTime = "00:45:30",
            overallPosition = 1,
            categoryPosition = 1,
            status = "Finished"
        });
        var duplicate = await organiser.PostAsJsonAsync("/api/results", new
        {
            enrolmentId,
            finishTime = "00:46:00",
            overallPosition = 2,
            categoryPosition = 2,
            status = "Finished"
        });
        var mine = await participant.GetAsync("/api/results/mine");
        var list = await mine.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Contains(list.EnumerateArray(), r => r.GetProperty("enrolmentID").GetInt32() == enrolmentId);
    }

    [Fact]
    public async Task Results_OtherOrganiserCannotCapture_Returns403()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");
        var enrolmentId = await TestHelper.EnrolAsync(participant, categoryId);
        var otherOrganiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await otherOrganiser.PostAsJsonAsync("/api/results", new
        {
            enrolmentId,
            finishTime = "00:30:00",
            overallPosition = 1,
            categoryPosition = 1,
            status = "Finished"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}