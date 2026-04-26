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
    public async Task Create_rejects_duplicate_clue_codes()
    {
        var client = _factory.CreateClient();
        var (_, token) = await RegisterUser(client, "dup-clue@example.com", role: UserRole.Creator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsJsonAsync("/api/hunts", new
        {
            name = "Dup",
            steps = new[] { new { title = "s", type = "manual" } },
            clues = new[]
            {
                new { code = "K3-42", title = "A", reveal = "" },
                new { code = "k3-42", title = "B", reveal = "" }, // case-insensitive duplicate
            },
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_too_many_clues()
    {
        var client = _factory.CreateClient();
        var (_, token) = await RegisterUser(client, "many-clues@example.com", role: UserRole.Creator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clues = Enumerable.Range(0, 250).Select(i => new { code = $"K{i}", title = "", reveal = "" }).ToArray();
        var resp = await client.PostAsJsonAsync("/api/hunts", new { name = "Many", clues });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_replaces_steps_and_removes_orphans()
    {
        var client = _factory.CreateClient();
        var (_, token) = await RegisterUser(client, "putter@example.com", role: UserRole.Creator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await client.PostAsJsonAsync("/api/hunts", new
        {
            name = "Original",
            steps = new[]
            {
                new { title = "s1", type = "manual" },
                new { title = "s2", type = "manual" },
            },
        });
        var hunt = await create.Content.ReadFromJsonAsync<HuntBody>();
        var firstStepId = hunt!.Steps[0].Id;

        // Replace: keep first step (by id), drop second, add a new third.
        var put = await client.PutAsJsonAsync($"/api/hunts/{hunt.Id}", new
        {
            name = "Renamed",
            steps = new object[]
            {
                new { id = firstStepId, title = "s1 renamed", type = "manual" },
                new { title = "brand-new", type = "manual" },
            },
            clues = Array.Empty<object>(),
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await client.GetFromJsonAsync<HuntBody>($"/api/hunts/{hunt.Id}");
        refreshed!.Name.Should().Be("Renamed");
        refreshed.Steps.Should().HaveCount(2);
        refreshed.Steps.Should().Contain(s => s.Id == firstStepId && s.Title == "s1 renamed");
        refreshed.Steps.Should().NotContain(s => s.Title == "s2");
        refreshed.Steps.Should().Contain(s => s.Title == "brand-new");
    }

    [Fact]
    public async Task Update_forbidden_for_other_creators()
    {
        var clientA = _factory.CreateClient();
        var (_, tokenA) = await RegisterUser(clientA, "ownerA@example.com", role: UserRole.Creator);
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var create = await clientA.PostAsJsonAsync("/api/hunts", new { name = "A's", steps = new[] { new { title = "x", type = "manual" } } });
        var hunt = await create.Content.ReadFromJsonAsync<HuntBody>();

        var clientB = _factory.CreateClient();
        var (_, tokenB) = await RegisterUser(clientB, "ownerB@example.com", role: UserRole.Creator);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var hijack = await clientB.PutAsJsonAsync($"/api/hunts/{hunt!.Id}", new
        {
            name = "stolen", steps = Array.Empty<object>(), clues = Array.Empty<object>(),
        });
        hijack.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Add_clue_rejects_duplicate_codes()
    {
        var client = _factory.CreateClient();
        var (_, token) = await RegisterUser(client, "clueadder@example.com", role: UserRole.Creator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await client.PostAsJsonAsync("/api/hunts", new
        {
            name = "Clue host",
            steps = new[] { new { title = "s", type = "manual" } },
            clues = new[] { new { code = "ABC", title = "", reveal = "" } },
        });
        var hunt = await create.Content.ReadFromJsonAsync<HuntBody>();

        var dup = await client.PostAsJsonAsync($"/api/hunts/{hunt!.Id}/clues", new
        {
            code = "abc", title = "Other", reveal = "Z", points = 1,
        });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_with_mine_filter_returns_own_drafts()
    {
        var client = _factory.CreateClient();
        var (_, token) = await RegisterUser(client, "mine@example.com", role: UserRole.Creator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.PostAsJsonAsync("/api/hunts", new { name = "Draft mine", steps = new[] { new { title = "s", type = "manual" } } });

        var anonResp = await _factory.CreateClient().GetFromJsonAsync<List<HuntBody>>("/api/hunts");
        anonResp.Should().NotContain(h => h.Name == "Draft mine"); // Drafts hidden from public.

        var mineResp = await client.GetFromJsonAsync<List<HuntBody>>("/api/hunts?mine=true");
        mineResp.Should().Contain(h => h.Name == "Draft mine");
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
    private record HuntBody(Guid Id, string Name, string Status, List<StepBody> Steps);
    private record StepBody(Guid Id, string Title, string Type);
}
