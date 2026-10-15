using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Tests.Helpers;
using Xunit;

namespace RaceDay.Api.Tests;

public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_ValidUser_Returns201_WithoutPassword(string role)
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "New User",
            email = TestHelper.UniqueEmail("new"),
            password = TestHelper.Password,
            role
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(role, body);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var client = TestHelper.NewClient(_factory);
        var email = TestHelper.UniqueEmail("hash");

        await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Hash Test", email, password = TestHelper.Password, role = "Participant" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = db.Users.Single(u => u.Email == email);

        Assert.NotEqual(TestHelper.Password, user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash); // BCrypt hash
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = TestHelper.NewClient(_factory);
        var payload = new
        {
            fullName = "Dup User",
            email = TestHelper.UniqueEmail("dup"),
            password = TestHelper.Password,
            role = "Participant"
        };

        await client.PostAsJsonAsync("/api/auth/register", payload);
        var second = await client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Bad Role",
            email = TestHelper.UniqueEmail("badrole"),
            password = TestHelper.Password,
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingFields_Returns400()
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "",
            email = "not-an-email",
            password = "123",
            role = "Participant"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_WithRoleAndUserId()
    {
        var client = TestHelper.NewClient(_factory);
        var email = TestHelper.UniqueEmail("login");
        await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Login User", email, password = TestHelper.Password, role = "Organiser" });

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestHelper.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Organiser", json.GetProperty("role").GetString());
        Assert.True(json.GetProperty("userId").GetInt32() > 0);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = TestHelper.NewClient(_factory);
        var email = TestHelper.UniqueEmail("wrongpw");
        await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Wrong Pw", email, password = TestHelper.Password, role = "Participant" });

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = TestHelper.UniqueEmail("ghost"), password = TestHelper.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_Returns401()
    {
        var client = TestHelper.NewClient(_factory);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_AfterLogin_Returns200_AndAfterLogout_Returns401()
    {
        var client = await TestHelper.LoginAsAsync(_factory, "Participant");

        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        await client.PostAsync("/api/auth/logout", null);

        var afterLogout = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }
}