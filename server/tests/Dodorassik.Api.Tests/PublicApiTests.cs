using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dodorassik.Api.Tests;

/// <summary>
/// The public catalogue is unauthenticated and returns only Published
/// hunts. Drafts, Submitted, Rejected and Archived hunts must never leak
/// (privacy: a parent could submit a hunt referencing a private location;
/// it should stay invisible until reviewed).
/// </summary>
public class PublicApiTests
{
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public async Task Endpoint_is_anonymous()
    {
        using var factory = new TestingWebAppFactory();
        var resp = await factory.CreateClient().GetAsync("/api/public/hunts");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Returns_only_published_hunts()
    {
        using var factory = new TestingWebAppFactory();
        await SeedHuntAsync(factory, "Brouillon", HuntStatus.Draft, HuntCategory.Permanent);
        await SeedHuntAsync(factory, "À modérer", HuntStatus.Submitted, HuntCategory.Permanent);
        await SeedHuntAsync(factory, "Rejetée", HuntStatus.Rejected, HuntCategory.Permanent);
        await SeedHuntAsync(factory, "Archivée", HuntStatus.Archived, HuntCategory.Permanent);
        await SeedHuntAsync(factory, "Publiée permanente", HuntStatus.Published, HuntCategory.Permanent);

        var resp = await factory.CreateClient().GetAsync("/api/public/hunts");
        var body = await resp.Content.ReadFromJsonAsync<PublicHuntsBody>(_json);

        body!.Permanent.Should().ContainSingle(h => h.Name == "Publiée permanente");
        body.Permanent.Should().NotContain(h => h.Name == "Brouillon" || h.Name == "À modérer" || h.Name == "Rejetée" || h.Name == "Archivée");
    }

    [Fact]
    public async Task Event_window_filters_out_past_and_future_events()
    {
        using var factory = new TestingWebAppFactory();
        var now = DateTime.UtcNow;
        await SeedHuntAsync(factory, "Événement passé", HuntStatus.Published, HuntCategory.Event,
            eventStart: now.AddDays(-30), eventEnd: now.AddDays(-1));
        await SeedHuntAsync(factory, "Événement futur", HuntStatus.Published, HuntCategory.Event,
            eventStart: now.AddDays(1), eventEnd: now.AddDays(30));
        await SeedHuntAsync(factory, "Événement en cours", HuntStatus.Published, HuntCategory.Event,
            eventStart: now.AddDays(-1), eventEnd: now.AddDays(1));

        var resp = await factory.CreateClient().GetAsync("/api/public/hunts");
        var body = await resp.Content.ReadFromJsonAsync<PublicHuntsBody>(_json);

        body!.Events.Should().ContainSingle(h => h.Name == "Événement en cours");
    }

    [Fact]
    public async Task Category_filter_narrows_response()
    {
        using var factory = new TestingWebAppFactory();
        await SeedHuntAsync(factory, "P", HuntStatus.Published, HuntCategory.Permanent);
        await SeedHuntAsync(factory, "E", HuntStatus.Published, HuntCategory.Event,
            eventStart: DateTime.UtcNow.AddDays(-1), eventEnd: DateTime.UtcNow.AddDays(1));

        var perm = await factory.CreateClient().GetFromJsonAsync<PublicHuntsBody>("/api/public/hunts?category=permanent", _json);
        var evt = await factory.CreateClient().GetFromJsonAsync<PublicHuntsBody>("/api/public/hunts?category=event", _json);

        perm!.Permanent.Should().HaveCount(1);
        perm.Events.Should().BeEmpty();
        evt!.Events.Should().HaveCount(1);
        evt.Permanent.Should().BeEmpty();
    }

    [Fact]
    public async Task Public_response_does_not_leak_creator_or_submission_data()
    {
        using var factory = new TestingWebAppFactory();
        await SeedHuntAsync(factory, "Visible", HuntStatus.Published, HuntCategory.Permanent);
        var raw = await factory.CreateClient().GetStringAsync("/api/public/hunts");

        // Public DTO has no creatorId, no email, no scores, no submission data.
        raw.Should().NotContain("creatorId", "the public catalogue must not expose creator identity");
        raw.Should().NotContain("@", "no email field should leak");
        raw.Should().NotContain("submissions");
        raw.Should().NotContain("rejectionReason");
    }

    private static async Task SeedHuntAsync(
        TestingWebAppFactory factory,
        string name,
        HuntStatus status,
        HuntCategory category,
        DateTime? eventStart = null,
        DateTime? eventEnd = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var creator = new User
        {
            Email = $"creator-{Guid.NewGuid():N}@public.test",
            DisplayName = "Test Creator",
            PasswordHash = "v1$1$AAAA$AAAA",
            Role = UserRole.Creator,
        };
        db.Users.Add(creator);

        db.Hunts.Add(new Hunt
        {
            Name = name,
            Description = "seed",
            CreatorId = creator.Id,
            Status = status,
            Category = category,
            EventStartUtc = eventStart,
            EventEndUtc = eventEnd,
        });
        await db.SaveChangesAsync();
    }

    private record PublicHuntsBody(List<PublicHunt> Permanent, List<PublicHunt> Events);
    private record PublicHunt(Guid Id, string Name, string Description, string? LocationLabel, int StepCount, HuntCategory Category, DateTime? EventStartUtc, DateTime? EventEndUtc);
}
