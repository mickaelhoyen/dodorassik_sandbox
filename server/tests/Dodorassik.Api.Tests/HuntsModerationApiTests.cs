using System.Net;
using System.Net.Http.Json;
using Dodorassik.Core.Domain;
using FluentAssertions;
using Xunit;

namespace Dodorassik.Api.Tests;

/// <summary>
/// End-to-end of the moderation workflow: a creator submits a hunt for
/// review, a super-admin approves or rejects, the public catalogue updates
/// accordingly. Direct creator → Published is impossible by design.
/// </summary>
public class HuntsModerationApiTests : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory;

    public HuntsModerationApiTests(TestingWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Full_happy_path_creator_submits_admin_approves()
    {
        var (_, creatorToken) = await TestUserHelper.RegisterAndPromoteAsync(_factory, "creator@mod.test", UserRole.Creator);
        var (_, adminToken)   = await TestUserHelper.RegisterAndPromoteAsync(_factory, "admin@mod.test",   UserRole.SuperAdmin);
        var creator = _factory.AuthClient(creatorToken);
        var admin   = _factory.AuthClient(adminToken);

        var hunt = await CreateMinimalHuntAsync(creator, "Chasse à valider");
        var huntId = hunt.Id;

        var submit = await creator.PostAsync($"/api/hunts/{huntId}/submit-for-review", null);
        submit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Public catalogue: not yet visible.
        var anonBefore = await _factory.CreateClient().GetFromJsonAsync<PublicHuntsBody>("/api/public/hunts");
        anonBefore!.Permanent.Should().NotContain(h => h.Id == huntId);

        // Admin queue: hunt is there.
        var queue = await admin.GetFromJsonAsync<List<HuntBody>>("/api/admin/hunts?status=submitted");
        queue.Should().Contain(h => h.Id == huntId);

        var approve = await admin.PostAsync($"/api/admin/hunts/{huntId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonAfter = await _factory.CreateClient().GetFromJsonAsync<PublicHuntsBody>("/api/public/hunts");
        anonAfter!.Permanent.Should().Contain(h => h.Id == huntId);
    }

    [Fact]
    public async Task Reject_records_reason_and_blocks_publication()
    {
        var (_, creatorToken) = await TestUserHelper.RegisterAndPromoteAsync(_factory, "creator@reject.test", UserRole.Creator);
        var (_, adminToken)   = await TestUserHelper.RegisterAndPromoteAsync(_factory, "admin@reject.test",   UserRole.SuperAdmin);
        var creator = _factory.AuthClient(creatorToken);
        var admin   = _factory.AuthClient(adminToken);

        var hunt = await CreateMinimalHuntAsync(creator, "À rejeter");
        await creator.PostAsync($"/api/hunts/{hunt.Id}/submit-for-review", null);

        var reject = await admin.PostAsJsonAsync($"/api/admin/hunts/{hunt.Id}/reject", new
        {
            reason = "Contient une référence à l'école — voir CLAUDE.md §3 (pas d'enfant identifiable).",
        });
        reject.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshed = await creator.GetFromJsonAsync<HuntBody>($"/api/hunts/{hunt.Id}");
        refreshed!.Status.Should().Be("rejected");
        refreshed.RejectionReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Reject_requires_a_reason()
    {
        var (_, creatorToken) = await TestUserHelper.RegisterAndPromoteAsync(_factory, "creator@reasonless.test", UserRole.Creator);
        var (_, adminToken)   = await TestUserHelper.RegisterAndPromoteAsync(_factory, "admin@reasonless.test",   UserRole.SuperAdmin);
        var creator = _factory.AuthClient(creatorToken);
        var admin   = _factory.AuthClient(adminToken);

        var hunt = await CreateMinimalHuntAsync(creator, "Reasonless");
        await creator.PostAsync($"/api/hunts/{hunt.Id}/submit-for-review", null);

        var noReason = await admin.PostAsJsonAsync($"/api/admin/hunts/{hunt.Id}/reject", new { reason = "" });
        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var tooShort = await admin.PostAsJsonAsync($"/api/admin/hunts/{hunt.Id}/reject", new { reason = "no" });
        tooShort.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Withdraw_returns_hunt_to_draft()
    {
        var (_, creatorToken) = await TestUserHelper.RegisterAndPromoteAsync(_factory, "creator@withdraw.test", UserRole.Creator);
        var creator = _factory.AuthClient(creatorToken);

        var hunt = await CreateMinimalHuntAsync(creator, "À retirer");
        await creator.PostAsync($"/api/hunts/{hunt.Id}/submit-for-review", null);

        var withdraw = await creator.PostAsync($"/api/hunts/{hunt.Id}/withdraw", null);
        withdraw.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshed = await creator.GetFromJsonAsync<HuntBody>($"/api/hunts/{hunt.Id}");
        refreshed!.Status.Should().Be("draft");
    }

    [Fact]
    public async Task Submitted_hunt_cannot_be_edited()
    {
        var (_, creatorToken) = await TestUserHelper.RegisterAndPromoteAsync(_factory, "creator@locked.test", UserRole.Creator);
        var creator = _factory.AuthClient(creatorToken);
        var hunt = await CreateMinimalHuntAsync(creator, "Locked");
        await creator.PostAsync($"/api/hunts/{hunt.Id}/submit-for-review", null);

        var tryEdit = await creator.PutAsJsonAsync($"/api/hunts/{hunt.Id}", new
        {
            name = "Renamed",
            steps = new[] { new { title = "x", type = "manual" } },
            clues = Array.Empty<object>(),
        });
        tryEdit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Hunt_without_steps_cannot_be_submitted()
    {
        var (_, creatorToken) = await TestUserHelper.RegisterAndPromoteAsync(_factory, "creator@empty.test", UserRole.Creator);
        var creator = _factory.AuthClient(creatorToken);

        var resp = await creator.PostAsJsonAsync("/api/hunts", new { name = "Vide" });
        var hunt = await resp.Content.ReadFromJsonAsync<HuntBody>();

        var submit = await creator.PostAsync($"/api/hunts/{hunt!.Id}/submit-for-review", null);
        submit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Player_cannot_access_admin_endpoints()
    {
        var (_, playerToken) = await TestUserHelper.RegisterAsync(_factory.CreateClient(), "player@admin.test");
        var player = _factory.AuthClient(playerToken);

        (await player.GetAsync("/api/admin/hunts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await player.PostAsync($"/api/admin/hunts/{Guid.NewGuid()}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_only_works_on_submitted_hunts()
    {
        var (_, creatorToken) = await TestUserHelper.RegisterAndPromoteAsync(_factory, "creator@order.test", UserRole.Creator);
        var (_, adminToken)   = await TestUserHelper.RegisterAndPromoteAsync(_factory, "admin@order.test",   UserRole.SuperAdmin);
        var creator = _factory.AuthClient(creatorToken);
        var admin   = _factory.AuthClient(adminToken);

        var hunt = await CreateMinimalHuntAsync(creator, "Pas encore submitted");
        // No submit-for-review call → still Draft.

        var approve = await admin.PostAsync($"/api/admin/hunts/{hunt.Id}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Archive_only_works_on_published_hunts()
    {
        var (_, creatorToken) = await TestUserHelper.RegisterAndPromoteAsync(_factory, "creator@archive.test", UserRole.Creator);
        var creator = _factory.AuthClient(creatorToken);

        var hunt = await CreateMinimalHuntAsync(creator, "Pas publiée");
        var archive = await creator.PostAsync($"/api/hunts/{hunt.Id}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task<HuntBody> CreateMinimalHuntAsync(HttpClient creator, string name)
    {
        var resp = await creator.PostAsJsonAsync("/api/hunts", new
        {
            name,
            description = "seed",
            mode = "relaxed",
            steps = new[] { new { title = "step", type = "manual", points = 1 } },
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<HuntBody>())!;
    }

    private record HuntBody(Guid Id, string Status, string? RejectionReason);
    private record PublicHuntsBody(List<PublicHunt> Permanent, List<PublicHunt> Events);
    private record PublicHunt(Guid Id, string Name);
}
