using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dodorassik.Api.Dtos;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

[ApiController]
[Route("api/hunts")]
public class HuntsController : ControllerBase
{
    private readonly AppDbContext _db;

    public HuntsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<HuntDto>>> List([FromQuery] string? status)
    {
        var query = _db.Hunts.Include(h => h.Steps).Include(h => h.Clues).AsQueryable();
        if (string.Equals(status, "published", StringComparison.OrdinalIgnoreCase))
            query = query.Where(h => h.Status == HuntStatus.Published);

        var hunts = await query.OrderByDescending(h => h.UpdatedAtUtc).ToListAsync();
        return hunts.Select(h => h.ToDto()).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HuntDto>> Get(Guid id)
    {
        var hunt = await _db.Hunts
            .Include(h => h.Steps)
            .Include(h => h.Clues)
            .FirstOrDefaultAsync(h => h.Id == id);
        return hunt is null ? NotFound() : hunt.ToDto();
    }

    [HttpPost]
    [Authorize(Roles = "creator,super_admin")]
    public async Task<ActionResult<HuntDto>> Create([FromBody] CreateHuntRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var creatorId = CurrentUserId();
        if (creatorId is null) return Unauthorized();

        var hunt = new Hunt
        {
            Name = req.Name.Trim(),
            Description = req.Description ?? string.Empty,
            CreatorId = creatorId.Value,
            Mode = HuntMappings.ParseHuntMode(req.Mode),
            Category = HuntMappings.ParseHuntCategory(req.Category),
            LocationLabel = req.LocationLabel?.Trim(),
            EventStartUtc = req.EventStartUtc,
            EventEndUtc = req.EventEndUtc,
            Status = HuntStatus.Draft,
        };

        if (req.Steps is not null)
        {
            if (req.Steps.Count > InputLimits.StepsPerHuntMax)
                return BadRequest(new { error = "too_many_steps" });

            var order = 0;
            foreach (var s in req.Steps)
            {
                var paramsJson = s.Params?.GetRawText() ?? "{}";
                if (Encoding.UTF8.GetByteCount(paramsJson) > InputLimits.JsonParamsMaxBytes)
                    return BadRequest(new { error = "step_params_too_large" });

                hunt.Steps.Add(new HuntStep
                {
                    Order = order++,
                    Title = s.Title,
                    Description = s.Description ?? string.Empty,
                    Type = HuntMappings.ParseStepType(s.Type),
                    ParamsJson = paramsJson,
                    Points = s.Points ?? 10,
                    BlocksNext = s.BlocksNext ?? true,
                });
            }
        }

        if (req.Clues is not null)
        {
            if (req.Clues.Count > InputLimits.CluesPerHuntMax)
                return BadRequest(new { error = "too_many_clues" });

            var codesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in req.Clues)
            {
                if (!codesSeen.Add(c.Code))
                    return BadRequest(new { error = "duplicate_clue_code" });

                hunt.Clues.Add(new Clue
                {
                    Code = c.Code.Trim().ToUpperInvariant(),
                    Title = c.Title?.Trim() ?? string.Empty,
                    Reveal = c.Reveal?.Trim() ?? string.Empty,
                    Points = c.Points ?? 5,
                });
            }
        }

        _db.Hunts.Add(hunt);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = hunt.Id }, hunt.ToDto());
    }

    /// <summary>
    /// Full replace of steps and clues. The hunt metadata (name, description,
    /// mode) is overwritten. Steps and clues not present in the request are
    /// deleted; existing ones whose Id matches are updated in place.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "creator,super_admin")]
    public async Task<ActionResult<HuntDto>> Update(Guid id, [FromBody] UpdateHuntRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var creatorId = CurrentUserId();
        if (creatorId is null) return Unauthorized();

        var hunt = await _db.Hunts
            .Include(h => h.Steps)
            .Include(h => h.Clues)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hunt is null) return NotFound();
        if (hunt.CreatorId != creatorId.Value && !User.IsInRole("super_admin"))
            return Forbid();

        hunt.Name = req.Name.Trim();
        hunt.Description = req.Description ?? string.Empty;
        hunt.Mode = HuntMappings.ParseHuntMode(req.Mode);
        hunt.LocationLabel = req.LocationLabel?.Trim();
        hunt.UpdatedAtUtc = DateTime.UtcNow;

        // ---- Steps: upsert + remove orphans ----------------------------
        var incomingStepIds = req.Steps?
            .Where(s => s.Id.HasValue)
            .Select(s => s.Id!.Value)
            .ToHashSet() ?? [];

        var stepsToRemove = hunt.Steps
            .Where(s => !incomingStepIds.Contains(s.Id))
            .ToList();
        foreach (var s in stepsToRemove)
            _db.HuntSteps.Remove(s);

        var order = 0;
        foreach (var sr in req.Steps ?? [])
        {
            var paramsJson = sr.Params?.GetRawText() ?? "{}";
            if (Encoding.UTF8.GetByteCount(paramsJson) > InputLimits.JsonParamsMaxBytes)
                return BadRequest(new { error = "step_params_too_large" });

            if (sr.Id.HasValue)
            {
                var existing = hunt.Steps.FirstOrDefault(s => s.Id == sr.Id.Value);
                if (existing is not null)
                {
                    existing.Order = order;
                    existing.Title = sr.Title;
                    existing.Description = sr.Description ?? string.Empty;
                    existing.Type = HuntMappings.ParseStepType(sr.Type);
                    existing.ParamsJson = paramsJson;
                    existing.Points = sr.Points ?? 10;
                    existing.BlocksNext = sr.BlocksNext ?? true;
                    order++;
                    continue;
                }
            }
            hunt.Steps.Add(new HuntStep
            {
                Order = order++,
                Title = sr.Title,
                Description = sr.Description ?? string.Empty,
                Type = HuntMappings.ParseStepType(sr.Type),
                ParamsJson = paramsJson,
                Points = sr.Points ?? 10,
                BlocksNext = sr.BlocksNext ?? true,
            });
        }

        // ---- Clues: upsert + remove orphans ----------------------------
        var incomingClueIds = req.Clues?
            .Where(c => c.Id.HasValue)
            .Select(c => c.Id!.Value)
            .ToHashSet() ?? [];

        var cluesToRemove = hunt.Clues
            .Where(c => !incomingClueIds.Contains(c.Id))
            .ToList();
        foreach (var c in cluesToRemove)
            _db.Clues.Remove(c);

        var codesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cr in req.Clues ?? [])
        {
            var normalized = cr.Code.Trim().ToUpperInvariant();
            if (!codesSeen.Add(normalized))
                return BadRequest(new { error = "duplicate_clue_code" });

            if (cr.Id.HasValue)
            {
                var existing = hunt.Clues.FirstOrDefault(c => c.Id == cr.Id.Value);
                if (existing is not null)
                {
                    existing.Code = normalized;
                    existing.Title = cr.Title?.Trim() ?? string.Empty;
                    existing.Reveal = cr.Reveal?.Trim() ?? string.Empty;
                    existing.Points = cr.Points ?? 5;
                    continue;
                }
            }
            hunt.Clues.Add(new Clue
            {
                Code = normalized,
                Title = cr.Title?.Trim() ?? string.Empty,
                Reveal = cr.Reveal?.Trim() ?? string.Empty,
                Points = cr.Points ?? 5,
            });
        }

        await _db.SaveChangesAsync();
        return hunt.ToDto();
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "creator,super_admin")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var hunt = await _db.Hunts.FindAsync(id);
        if (hunt is null) return NotFound();
        hunt.Status = HuntStatus.Published;
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---- Clue sub-resource -------------------------------------------------

    [HttpPost("{huntId:guid}/clues")]
    [Authorize(Roles = "creator,super_admin")]
    public async Task<ActionResult<ClueDto>> AddClue(Guid huntId, [FromBody] CreateClueRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var creatorId = CurrentUserId();
        if (creatorId is null) return Unauthorized();

        var hunt = await _db.Hunts.Include(h => h.Clues).FirstOrDefaultAsync(h => h.Id == huntId);
        if (hunt is null) return NotFound();
        if (hunt.CreatorId != creatorId.Value && !User.IsInRole("super_admin"))
            return Forbid();
        if (hunt.Clues.Count >= InputLimits.CluesPerHuntMax)
            return BadRequest(new { error = "too_many_clues" });

        var normalized = req.Code.Trim().ToUpperInvariant();
        if (hunt.Clues.Any(c => c.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = "duplicate_clue_code" });

        var clue = new Clue
        {
            HuntId = huntId,
            Code = normalized,
            Title = req.Title?.Trim() ?? string.Empty,
            Reveal = req.Reveal?.Trim() ?? string.Empty,
            Points = req.Points ?? 5,
        };
        _db.Clues.Add(clue);
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = huntId }, clue.ToDto());
    }

    [HttpDelete("{huntId:guid}/clues/{clueId:guid}")]
    [Authorize(Roles = "creator,super_admin")]
    public async Task<IActionResult> DeleteClue(Guid huntId, Guid clueId)
    {
        var creatorId = CurrentUserId();
        if (creatorId is null) return Unauthorized();

        var hunt = await _db.Hunts.FindAsync(huntId);
        if (hunt is null) return NotFound();
        if (hunt.CreatorId != creatorId.Value && !User.IsInRole("super_admin"))
            return Forbid();

        var clue = await _db.Clues.FirstOrDefaultAsync(c => c.Id == clueId && c.HuntId == huntId);
        if (clue is null) return NotFound();

        _db.Clues.Remove(clue);
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---- Step submission ---------------------------------------------------

    [HttpPost("{huntId:guid}/steps/{stepId:guid}/submit")]
    [Authorize]
    [EnableRateLimiting("submit")]
    public async Task<ActionResult<SubmitStepResponse>> Submit(Guid huntId, Guid stepId, [FromBody] SubmitStepRequest req)
    {
        var payloadText = req.Payload.GetRawText();
        if (Encoding.UTF8.GetByteCount(payloadText) > InputLimits.JsonPayloadMaxBytes)
            return BadRequest(new { error = "payload_too_large" });

        var step = await _db.HuntSteps.FirstOrDefaultAsync(s => s.Id == stepId && s.HuntId == huntId);
        if (step is null) return NotFound();

        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var user = await _db.Users.FindAsync(userId.Value);
        if (user is null) return Unauthorized();

        // Server-side validation is intentionally light here — kids physically
        // perform the action, the adult phone reports the result. Anti-cheat
        // belongs in the competitive runtime, not the MVP.
        var accepted = true;
        var awarded = accepted ? step.Points : 0;

        var submission = new StepSubmission
        {
            HuntStepId = step.Id,
            FamilyId = user.FamilyId ?? Guid.Empty,
            SubmittedById = user.Id,
            Accepted = accepted,
            AwardedPoints = awarded,
            PayloadJson = payloadText,
            ClientCreatedAtUtc = DateTime.UtcNow,
        };
        _db.Submissions.Add(submission);

        if (user.FamilyId is { } familyId)
        {
            var score = await _db.HuntScores.FirstOrDefaultAsync(s => s.HuntId == huntId && s.FamilyId == familyId);
            if (score is null)
            {
                score = new HuntScore { HuntId = huntId, FamilyId = familyId, StartedAtUtc = DateTime.UtcNow };
                _db.HuntScores.Add(score);
            }
            score.TotalPoints += awarded;
            score.StepsCompleted += accepted ? 1 : 0;
        }

        await _db.SaveChangesAsync();
        return new SubmitStepResponse(accepted, awarded, null);
    }

    private Guid? CurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
