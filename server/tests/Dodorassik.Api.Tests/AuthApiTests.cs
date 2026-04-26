using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Dodorassik.Api.Tests;

public class AuthApiTests : IClassFixture<TestingWebAppFactory>
{
    private readonly HttpClient _client;

    public AuthApiTests(TestingWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_creates_account_and_returns_token()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "alice@example.com",
            password = "correcthorse42",
            displayName = "Alice",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<AuthBody>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be("alice@example.com");
        body.User.Role.Should().Be("player");
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "bob@example.com",
            password = "correcthorse42",
            displayName = "Bob",
        });

        var resp = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "bob@example.com",
            password = "anotherone42",
            displayName = "Bob 2",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("", "validpass1", "name")]
    [InlineData("not-an-email", "validpass1", "name")]
    [InlineData("ok@example.com", "short", "name")]
    [InlineData("ok@example.com", "validpass1", "")]
    public async Task Register_rejects_invalid_input(string email, string password, string displayName)
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_returns_generic_401_for_wrong_password_and_unknown_email()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "carol@example.com",
            password = "correcthorse42",
            displayName = "Carol",
        });

        var wrongPass = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "carol@example.com",
            password = "WRONG_password!",
        });
        var unknown = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@example.com",
            password = "anything12",
        });

        wrongPass.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var w = await wrongPass.Content.ReadAsStringAsync();
        var u = await unknown.Content.ReadAsStringAsync();
        // Privacy: same body for both — does not leak whether email exists.
        w.Should().Be(u);
    }

    [Fact]
    public async Task Login_succeeds_with_correct_credentials()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "dave@example.com",
            password = "correcthorse42",
            displayName = "Dave",
        });

        var resp = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "dave@example.com",
            password = "correcthorse42",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<AuthBody>();
        body!.Token.Should().NotBeNullOrEmpty();
    }

    private record AuthBody(string Token, UserBody User);
    private record UserBody(Guid Id, string Email, string DisplayName, string Role);
}
