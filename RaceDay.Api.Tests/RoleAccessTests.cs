using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Tests.Helpers;
using Xunit;

namespace RaceDay.Api.Tests;

public class RoleAccessTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleAccessTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { });
        return await client.SendAsync(request);
    }

    // Endpoints only an Organiser may call
    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("POST", "/api/events")]
    [InlineData("PUT", "/api/events/1")]
    [InlineData("DELETE", "/api/events/1")]
    [InlineData("POST", "/api/events/1/categories")]
    [InlineData("PUT", "/api/categories/1")]
    [InlineData("DELETE", "/api/categories/1")]
    [InlineData("GET", "/api/events/1/enrolments")]
    [InlineData("POST", "/api/results")]
    [InlineData("PUT", "/api/results/1")]
    public async Task OrganiserOnlyEndpoints_AsParticipant_Return403(string method, string url)
    {
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await SendAsync(participant, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Endpoints only a Participant may call
    [Theory]
    [InlineData("POST", "/api/enrolments")]
    [InlineData("GET", "/api/enrolments/mine")]
    [InlineData("DELETE", "/api/enrolments/1")]
    [InlineData("GET", "/api/results/mine")]
    public async Task ParticipantOnlyEndpoints_AsOrganiser_Return403(string method, string url)
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await SendAsync(organiser, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Every protected endpoint rejects someone who is not logged in
    [Theory]
    [InlineData("GET", "/api/users/me")]
    [InlineData("PUT", "/api/users/me")]
    [InlineData("GET", "/api/users")]
    [InlineData("POST", "/api/events")]
    [InlineData("PUT", "/api/events/1")]
    [InlineData("DELETE", "/api/events/1")]
    [InlineData("POST", "/api/events/1/categories")]
    [InlineData("PUT", "/api/categories/1")]
    [InlineData("DELETE", "/api/categories/1")]
    [InlineData("POST", "/api/enrolments")]
    [InlineData("GET", "/api/enrolments/mine")]
    [InlineData("GET", "/api/events/1/enrolments")]
    [InlineData("DELETE", "/api/enrolments/1")]
    [InlineData("POST", "/api/results")]
    [InlineData("PUT", "/api/results/1")]
    [InlineData("GET", "/api/results/mine")]
    public async Task ProtectedEndpoints_WhenNotLoggedIn_Return401(string method, string url)
    {
        var client = TestHelper.NewClient(_factory);

        var response = await SendAsync(client, method, url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Public endpoints work without logging in
    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/events/1")]
    [InlineData("/api/events/1/categories")]
    [InlineData("/api/events/1/results")]
    public async Task PublicEndpoints_WhenNotLoggedIn_Return200(string url)
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}