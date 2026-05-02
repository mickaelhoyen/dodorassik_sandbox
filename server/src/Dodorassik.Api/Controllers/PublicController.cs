using Dodorassik.Api.Dtos;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

/// <summary>
/// Unauthenticated endpoints surfaced on the public website and the Godot
/// player catalogue. No PII is returned — hunt data only.
/// </summary>
[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns all published hunts, optionally filtered by category.
    /// Query param: category = "permanent" | "event" (omit for both).
    /// For events, only those whose window includes today are returned.
    /// </summary>
    [HttpGet("hunts")]
    public async Task<ActionResult<PublicHuntsResponse>> ListPublicHunts([FromQuery] string? category)
    {
        var now = DateTime.UtcNow;

        // OrderBy must be applied to the entity (Hunts) before projecting to
        // PublicHuntDto, otherwise SQLite's EF provider cannot translate the
        // expression (only Npgsql tolerates ORDER BY on a projected member).
        var rows = await _db.Hunts
            .Where(h => h.Status == HuntStatus.Published)
            .OrderByDescending(h => h.EventEndUtc)
            .Select(h => new PublicHuntDto(
                h.Id,
                h.Name,
                h.Description,
                h.LocationLabel,
                h.Steps.Count,
                h.Category,
                h.EventStartUtc,
                h.EventEndUtc))
            .ToListAsync();

        var permanent = rows
            .Where(h => h.Category == HuntCategory.Permanent)
            .ToList();

        var events = rows
            .Where(h => h.Category == HuntCategory.Event)
            .Where(h => (h.EventStartUtc == null || h.EventStartUtc <= now)
                     && (h.EventEndUtc == null || h.EventEndUtc >= now))
            .ToList();

        if (string.Equals(category, "permanent", StringComparison.OrdinalIgnoreCase))
            return new PublicHuntsResponse(permanent, []);
        if (string.Equals(category, "event", StringComparison.OrdinalIgnoreCase))
            return new PublicHuntsResponse([], events);

        return new PublicHuntsResponse(permanent, events);
    }
}

public record PublicHuntDto(
    Guid Id,
    string Name,
    string Description,
    string? LocationLabel,
    int StepCount,
    HuntCategory Category,
    DateTime? EventStartUtc,
    DateTime? EventEndUtc);

public record PublicHuntsResponse(
    List<PublicHuntDto> Permanent,
    List<PublicHuntDto> Events);
