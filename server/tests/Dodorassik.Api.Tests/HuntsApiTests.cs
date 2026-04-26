using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dodorassik.Api.Tests;

public class HuntsApiTests : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory;

    public HuntsApiTests(TestingWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_is_public_and_returns_array()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/hunts");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_requires_creator_role()
    {
        var client = _factory.CreateClient();

        // Anonymous → 401
        var anon = await client.PostAsJsonAsync("/api/hunts", new { name = "x" });
        anon.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Player role → 403
        var (_, playerToken) = await RegisterUser(client, "player@example.com", role: UserRole.Player);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
        var player = await client.PostAsJsonAsync("/api/hunts", new { name = "x" });
        player.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Creator_can_create_and_list_hunts()
    {
        var client = _factory.CreateClient();
        var (_, token) = await RegisterUser(client, "creator@example.com", role: UserRole.Creator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await client.PostAsJsonAsync("/api/hunts", new
        {
            name = "Chasse au parc",
            description = "Pour les 6-10 ans",
            mode = "relaxed",
            steps = new[]
            {
                new { title = "Trouver l'arbre", type = "manual", points = 5 },
                new { title = "Photo du lac", type = "photo", points = 5 },
            },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetAsync("/api/hunts");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_rejects_oversized_steps_array()
    {
        var client = _factory.CreateClient();
        var (_, token) = await RegisterUser(client, "creator2@example.com", role: UserRole.Creator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var steps = Enumerable.Range(0, 200).Select(i => new
        {
            title = $"step{i}", type = "manual", points = 1,
        }).ToArray();

        var resp = await client.PostAsJsonAsync("/api/hunts", new { name = "huge", steps });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Registers a user via the API, then promotes them server-side to the
    /// requested role (the public `register` endpoint always returns Player).
    /// Re-issues a token after promotion so the role claim is up to date.
    /// </summary>
    private async Task<(Guid Id, string Token)> RegisterUser(HttpClient client, string email, UserRole role)
    {
        var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "correcthorse42",
            displayName = email.Split('@')[0],
        });
        first.EnsureSuccessStatusCode();

        if (role == UserRole.Player)
        {
            var body = await first.Content.ReadFromJsonAsync<AuthBody>();
            return (body!.User.Id, body.Token);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<Dodorassik.Core.Abstractions.IJwtTokenService>();
        var user = db.Users.Single(u => u.Email == email);
        user.Role = role;
        db.SaveChanges();
        return (user.Id, jwt.Issue(user));
    }

    private record AuthBody(string Token, UserBody User);
    private record UserBody(Guid Id, string Email, string DisplayName, string Role);
}
