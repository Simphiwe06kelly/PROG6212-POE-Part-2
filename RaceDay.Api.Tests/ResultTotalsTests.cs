using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.Tests.Helpers;
using Xunit;

namespace RaceDay.Api.Tests;

/// <summary>"My Results" needs the event date and how many people finished (e.g. 47th out of 312).</summary>
public class ResultTotalsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ResultTotalsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task CaptureAsync(HttpClient organiser, int enrolmentId, string status, string? finishTime, int? position)
    {
        var response = await organiser.PostAsJsonAsync("/api/results", new
        {
            enrolmentId,
            finishTime,
            overallPosition = position,
            categoryPosition = position,
            status
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MyResults_IncludeTheEventDate_AndTheNumberOfFinishers()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var date = DateTime.Today.AddDays(15);
        var eventId = await TestHelper.CreateEventAsync(organiser, date);
        var categoryId = await TestHelper.CreateCategoryAsync(organiser, eventId);

        var runner1 = await TestHelper.LoginAsAsync(_factory, "Participant");
        var runner2 = await TestHelper.LoginAsAsync(_factory, "Participant");
        var runner3 = await TestHelper.LoginAsAsync(_factory, "Participant");
        var enrolment1 = await TestHelper.EnrolAsync(runner1, categoryId);
        var enrolment2 = await TestHelper.EnrolAsync(runner2, categoryId);
        var enrolment3 = await TestHelper.EnrolAsync(runner3, categoryId);

        await CaptureAsync(organiser, enrolment1, "Finished", "00:41:10", 1);
        await CaptureAsync(organiser, enrolment2, "Finished", "00:47:32", 2);
        await CaptureAsync(organiser, enrolment3, "DNF", null, null); // did not finish: not counted

        var response = await runner2.GetAsync("/api/results/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var result = Assert.Single(results);
        Assert.Equal(eventId, result.GetProperty("eventID").GetInt32());
        Assert.Equal(2, result.GetProperty("overallPosition").GetInt32());
        Assert.Equal(2, result.GetProperty("totalFinishers").GetInt32());
        Assert.StartsWith(date.ToString("yyyy-MM-dd"), result.GetProperty("eventDate").GetString());
    }

    [Fact]
    public async Task EventResults_ShowTheSameFinisherTotalOnEveryRow()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);
        var categoryId = await TestHelper.CreateCategoryAsync(organiser, eventId);
        var runner1 = await TestHelper.LoginAsAsync(_factory, "Participant");
        var runner2 = await TestHelper.LoginAsAsync(_factory, "Participant");
        await CaptureAsync(organiser, await TestHelper.EnrolAsync(runner1, categoryId), "Finished", "00:30:00", 1);
        await CaptureAsync(organiser, await TestHelper.EnrolAsync(runner2, categoryId), "Finished", "00:35:00", 2);

        var response = await TestHelper.NewClient(_factory).GetAsync($"/api/events/{eventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(2, r.GetProperty("totalFinishers").GetInt32()));
        Assert.Equal(1, rows[0].GetProperty("overallPosition").GetInt32());
    }

    [Fact]
    public async Task MyResults_WithNoResultsYet_IsAnEmptyList()
    {
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.GetAsync("/api/results/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, results.GetArrayLength());
    }
}
