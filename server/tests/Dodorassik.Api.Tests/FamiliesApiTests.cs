using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Dodorassik.Api.Tests;

public class FamiliesApiTests : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory;

    public FamiliesApiTests(TestingWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/families/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PostAsJsonAsync("/api/families", new { name = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PostAsync("/api/families/leave", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_returns_404_when_user_has_no_family()
    {
        var (_, token) = await TestUserHelper.RegisterAsync(_factory.CreateClient(), "alone@fam.test");
        var client = _factory.AuthClient(token);
        var resp = await client.GetAsync("/api/families/me");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_assigns_user_to_family()
    {
        var (_, token) = await TestUserHelper.RegisterAsync(_factory.CreateClient(), "create@fam.test");
        var client = _factory.AuthClient(token);

        var create = await client.PostAsJsonAsync("/api/families", new { name = "Les Aventuriers" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var mine = await client.GetAsync("/api/families/me");
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await mine.Content.ReadFromJsonAsync<FamilyBody>();
        body!.Name.Should().Be("Les Aventuriers");
    }

    [Fact]
    public async Task Create_rejected_if_already_in_family()
    {
        var (_, token) = await TestUserHelper.RegisterAsync(_factory.CreateClient(), "twice@fam.test");
        var client = _factory.AuthClient(token);

        await client.PostAsJsonAsync("/api/families", new { name = "First" });
        var second = await client.PostAsJsonAsync("/api/families", new { name = "Second" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Join_then_leave_round_trip()
    {
        // Adult A creates the family.
        var (_, tokenA) = await TestUserHelper.RegisterAsync(_factory.CreateClient(), "adultA@fam.test");
        var clientA = _factory.AuthClient(tokenA);
        var create = await clientA.PostAsJsonAsync("/api/families", new { name = "Famille A" });
        var family = await create.Content.ReadFromJsonAsync<FamilyBody>();

        // Adult B joins the same family using its id.
        var (_, tokenB) = await TestUserHelper.RegisterAsync(_factory.CreateClient(), "adultB@fam.test");
        var clientB = _factory.AuthClient(tokenB);
        var join = await clientB.PostAsync($"/api/families/{family!.Id}/join", null);
        join.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // B sees the same family.
        var bMine = await clientB.GetAsync("/api/families/me");
        var bBody = await bMine.Content.ReadFromJsonAsync<FamilyBody>();
        bBody!.Id.Should().Be(family.Id);

        // B leaves.
        var leave = await clientB.PostAsync("/api/families/leave", null);
        leave.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await clientB.GetAsync("/api/families/me")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Join_unknown_family_is_404()
    {
        var (_, token) = await TestUserHelper.RegisterAsync(_factory.CreateClient(), "lost@fam.test");
        var client = _factory.AuthClient(token);
        var resp = await client.PostAsync($"/api/families/{Guid.NewGuid()}/join", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record FamilyBody(Guid Id, string Name);
}
