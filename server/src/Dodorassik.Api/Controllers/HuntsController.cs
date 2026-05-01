using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dodorassik.Api.Dtos;
using Dodorassik.Api.Hubs;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

[ApiController]
[Route("api/hunts")]
public class HuntsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAntiCheatService _antiCheat;
    private readonly IHubContext<CompetitiveHuntHub> _hub;

    public HuntsController(
        AppDbContext db,
        IAntiCheatService antiCheat,
        IHubContext<CompetitiveHuntHub> hub)
    {
        _db = db;
        _antiCheat = antiCheat;
        _hub = hub;
    }

    /// <summary>
    /// Lists hunts. By default, anonymous callers see only published hunts.
    /// Authenticated creators get their own hunts (any status) by passing
    /// <c>?mine=true</c>; super-admins see everything.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<HuntDto>>> List([FromQuery] string? status, [FromQuery] bool mine = false)
    {
        var query = _db.Hunts.Include(h => h.Steps).Include(h => h.Clues).AsQueryable();

        if (mine)
        {
            var uid = CurrentUserId();
            if (uid is null) return Unauthorized();
            query = query.Where(h => h.CreatorId == uid.Value);
        }
        else if (User.IsInRole("super_admin"))
        {
            // Super-admins can additionally filter by ?status=submitted etc.
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<HuntStatus>(status, true, out var st))
                query = query.Where(h => h.Status == st);
        }
        else
        {
            // Anonymous + non-creator authenticated callers: only published.
            query = query.Where(h => h.Status == HuntStatus.Published);
        }

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

        // Editing a Submitted or Published hunt would silently bypass
        // moderation. Force the creator to withdraw / unpublish first.
        if (hunt.Status is HuntStatus.Submitted or HuntStatus.Published && !User.IsInRole("super_admin"))
            return Conflict(new { error = "hunt_locked", status = hunt.Status.ToString().ToLowerInvariant() });

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
        {
            hunt.Steps.Remove(s);
            _db.HuntSteps.Remove(s);
        }

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
        {
            hunt.Clues.Remove(c);
            _db.Clues.Remove(c);
        }

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

    /// <summary>
    /// Creator submits a draft for super-admin review. The hunt becomes
    /// invisible to editing until either approved (Published) or rejected
    /// (back to Draft via /withdraw, optionally re-submit after fixing).
    /// </summary>
    [HttpPost("{id:guid}/submit-for-review")]
    [Authorize(Roles = "creator,super_admin")]
    public async Task<IActionResult> SubmitForReview(Guid id)
    {
        var creatorId = CurrentUserId();
        if (creatorId is null) return Unauthorized();

        var hunt = await _db.Hunts
            .Include(h => h.Steps)
            .FirstOrDefaultAsync(h => h.Id == id);
        if (hunt is null) return NotFound();
        if (hunt.CreatorId != creatorId.Value && !User.IsInRole("super_admin"))
            return Forbid();

        if (hunt.Status is not (HuntStatus.Draft or HuntStatus.Rejected))
            return Conflict(new { error = "invalid_status_transition", current = hunt.Status.ToString().ToLowerInvariant() });

        if (hunt.Steps.Count == 0)
            return BadRequest(new { error = "hunt_has_no_steps" });

        hunt.Status = HuntStatus.Submitted;
        hunt.SubmittedAtUtc = DateTime.UtcNow;
        hunt.RejectionReason = null;
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Creator pulls a submitted hunt back to draft (e.g. to fix a typo
    /// before the admin reviews it). Allowed only while still Submitted.
    /// </summary>
    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Roles = "creator,super_admin")]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        var creatorId = CurrentUserId();
        if (creatorId is null) return Unauthorized();

        var hunt = await _db.Hunts.FindAsync(id);
        if (hunt is null) return NotFound();
        if (hunt.CreatorId != creatorId.Value && !User.IsInRole("super_admin"))
            return Forbid();
        if (hunt.Status != HuntStatus.Submitted)
            return Conflict(new { error = "invalid_status_transition", current = hunt.Status.ToString().ToLowerInvariant() });

        hunt.Status = HuntStatus.Draft;
        hunt.SubmittedAtUtc = null;
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Creator removes a published hunt from the public catalogue. Existing
    /// scores remain — anonymising users is the GDPR endpoint's job.
    /// </summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "creator,super_admin")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var creatorId = CurrentUserId();
        if (creatorId is null) return Unauthorized();

        var hunt = await _db.Hunts.FindAsync(id);
        if (hunt is null) return NotFound();
        if (hunt.CreatorId != creatorId.Value && !User.IsInRole("super_admin"))
            return Forbid();
        if (hunt.Status != HuntStatus.Published)
            return Conflict(new { error = "invalid_status_transition", current = hunt.Status.ToString().ToLowerInvariant() });

        hunt.Status = HuntStatus.Archived;
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
        if (hunt.Status is HuntStatus.Submitted or HuntStatus.Published && !User.IsInRole("super_admin"))
            return Conflict(new { error = "hunt_locked", status = hunt.Status.ToString().ToLowerInvariant() });
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
        if (hunt.Status is HuntStatus.Submitted or HuntStatus.Published && !User.IsInRole("super_admin"))
            return Conflict(new { error = "hunt_locked", status = hunt.Status.ToString().ToLowerInvariant() });

        var clue = await _db.Clues.FirstOrDefaultAsync(c => c.Id == clueId && c.HuntId == huntId);
        if (clue is null) return NotFound();

        _db.Clues.Remove(clue);
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---- Leaderboard -------------------------------------------------------

    /// <summary>
    /// Returns a ranked list of teams (competitive mode) or families (relaxed
    /// mode) for the given hunt. Public once the hunt is Published.
    /// </summary>
    [HttpGet("{huntId:guid}/leaderboard")]
    public async Task<ActionResult<LeaderboardDto>> Leaderboard(Guid huntId)
    {
        var hunt = await _db.Hunts.FindAsync(huntId);
        if (hunt is null) return NotFound();
        if (hunt.Status != HuntStatus.Published && !User.IsInRole("super_admin"))
        {
            var uid = CurrentUserId();
            if (uid is null || hunt.CreatorId != uid.Value)
                return Forbid();
        }

        var totalSteps = await _db.HuntSteps.CountAsync(s => s.HuntId == huntId);

        List<LeaderboardEntryDto> rankings;

        if (hunt.Mode == HuntMode.Competitive)
        {
            // Competitive: rank by team score, then duration.
            var teams = await _db.Teams
                .Include(t => t.Family)
                .Where(t => t.HuntId == huntId)
                .ToListAsync();

            var teamScores = new List<(Team team, int points, int steps, DateTime? start, DateTime? end)>();
            foreach (var team in teams)
            {
                var subs = await _db.Submissions
                    .Where(s => s.TeamId == team.Id && s.Accepted && s.Step != null && s.Step.HuntId == huntId)
                    .Select(s => new { s.AwardedPoints, s.ClientCreatedAtUtc })
                    .ToListAsync();

                var pts = subs.Sum(s => s.AwardedPoints);
                var completedSteps = subs.Count;
                var start = subs.Count > 0 ? subs.Min(s => s.ClientCreatedAtUtc) : (DateTime?)null;
                var end = completedSteps >= totalSteps && totalSteps > 0 ? subs.Max(s => s.ClientCreatedAtUtc) : (DateTime?)null;

                teamScores.Add((team, pts, completedSteps, start, end));
            }

            rankings = teamScores
                .OrderByDescending(x => x.points)
                .ThenBy(x => x.end.HasValue ? (x.end - x.start)?.TotalSeconds : double.MaxValue)
                .Select((x, i) => new LeaderboardEntryDto(
                    i + 1,
                    x.team.Id,
                    x.team.Name,
                    x.team.FamilyId,
                    x.team.Family?.Name ?? "?",
                    x.points,
                    x.steps,
                    totalSteps,
                    x.start.HasValue && x.end.HasValue ? x.end - x.start : null,
                    x.end.HasValue))
                .ToList();
        }
        else
        {
            // Relaxed: rank families by total points.
            var scores = await _db.HuntScores
                .Include(s => s.Family)
                .Where(s => s.HuntId == huntId)
                .OrderByDescending(s => s.TotalPoints)
                .ToListAsync();

            rankings = scores
                .Select((s, i) => new LeaderboardEntryDto(
                    i + 1,
                    null,
                    s.Family?.Name ?? "?",
                    s.FamilyId,
                    s.Family?.Name ?? "?",
                    s.TotalPoints,
                    s.StepsCompleted,
                    totalSteps,
                    s.Duration,
                    s.FinishedAtUtc.HasValue))
                .ToList();
        }

        return new LeaderboardDto(huntId, hunt.Mode.ToString().ToLowerInvariant(), rankings, DateTime.UtcNow);
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

        var step = await _db.HuntSteps
            .Include(s => s.Hunt)
            .FirstOrDefaultAsync(s => s.Id == stepId && s.HuntId == huntId);
        if (step is null) return NotFound();

        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var user = await _db.Users.FindAsync(userId.Value);
        if (user is null) return Unauthorized();

        var familyId = user.FamilyId ?? Guid.Empty;

        // Resolve team if the client sent one, and verify membership.
        Guid? teamId = null;
        if (req.TeamId.HasValue)
        {
            var member = await _db.TeamMembers.FirstOrDefaultAsync(
                m => m.TeamId == req.TeamId.Value && m.UserId == user.Id);
            if (member is not null) teamId = req.TeamId.Value;
        }

        var accepted = true;
        string? rejectionReason = null;

        // Anti-cheat only applies in competitive hunts.
        if (step.Hunt?.Mode == HuntMode.Competitive)
        {
            if (!await _antiCheat.IsStepOrderValidAsync(huntId, stepId, familyId, teamId))
            {
                accepted = false;
                rejectionReason = "step_order_violation";
            }
            else
            {
                // Extract GPS coordinates from payload for speed check.
                double? lat = null, lon = null;
                if (step.Type == StepType.Location)
                {
                    try
                    {
                        var doc = JsonDocument.Parse(payloadText);
                        if (doc.RootElement.TryGetProperty("lat", out var latEl))
                            lat = latEl.GetDouble();
                        if (doc.RootElement.TryGetProperty("lon", out var lonEl))
                            lon = lonEl.GetDouble();
                    }
                    catch { }
                }

                if (!await _antiCheat.IsGpsSpeedPlausibleAsync(huntId, familyId, teamId, lat, lon, DateTime.UtcNow))
                {
                    accepted = false;
                    rejectionReason = "gps_speed_implausible";
                }
            }
        }

        var awarded = accepted ? step.Points : 0;

        var submission = new StepSubmission
        {
            HuntStepId = step.Id,
            FamilyId = familyId,
            TeamId = teamId,
            SubmittedById = user.Id,
            Accepted = accepted,
            AwardedPoints = awarded,
            PayloadJson = payloadText,
            ClientCreatedAtUtc = DateTime.UtcNow,
        };
        _db.Submissions.Add(submission);

        if (user.FamilyId is { } fid && accepted)
        {
            var score = await _db.HuntScores.FirstOrDefaultAsync(s => s.HuntId == huntId && s.FamilyId == fid);
            if (score is null)
            {
                score = new HuntScore { HuntId = huntId, FamilyId = fid, StartedAtUtc = DateTime.UtcNow };
                _db.HuntScores.Add(score);
            }
            score.TotalPoints += awarded;
            score.StepsCompleted += 1;

            var totalSteps = await _db.HuntSteps.CountAsync(s => s.HuntId == huntId);
            if (score.StepsCompleted >= totalSteps)
                score.FinishedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Push real-time leaderboard update for competitive hunts (awaited to
        // keep the HTTP context alive while we compute and broadcast).
        if (step.Hunt?.Mode == HuntMode.Competitive && accepted)
        {
            await BroadcastLeaderboardAsync(huntId);
        }

        return new SubmitStepResponse(accepted, awarded, rejectionReason);
    }

    private async Task BroadcastLeaderboardAsync(Guid huntId)
    {
        var leaderboard = await BuildLeaderboardAsync(huntId);
        if (leaderboard is not null)
        {
            await _hub.Clients.Group(CompetitiveHuntHub.HuntGroupName(huntId.ToString()))
                .SendAsync("LeaderboardUpdated", leaderboard);
        }
    }

    private async Task<LeaderboardDto?> BuildLeaderboardAsync(Guid huntId)
    {
        var result = await Leaderboard(huntId);
        return result.Value;
    }

    private Guid? CurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
