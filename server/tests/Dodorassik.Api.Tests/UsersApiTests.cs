using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Dodorassik.Api.Tests;

public class UsersApiTests : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory;

    public UsersApiTests(TestingWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Export_returns_only_current_user_data()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndToken(client, "exporter@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/users/me/export");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        json.Should().Contain("exporter@example.com");
        json.Should().Contain("huntsCreated");
        json.Should().Contain("submissions");
    }

    [Fact]
    public async Task Delete_requires_explicit_confirmation()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndToken(client, "tobedeleted@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var noConfirm = await client.DeleteAsync("/api/users/me");
        noConfirm.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await client.DeleteAsync("/api/users/me?confirm=yes");
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // After deletion, the token claim is stale → 401 expected.
        var after = await client.GetAsync("/api/users/me");
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> RegisterAndToken(HttpClient client, string email)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "correcthorse42",
            displayName = email.Split('@')[0],
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuthBody>();
        return body!.Token;
    }

    private record AuthBody(string Token);
}
