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
        var query = _db.Hunts.Include(h => h.Steps).AsQueryable();
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

        _db.Hunts.Add(hunt);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = hunt.Id }, hunt.ToDto());
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
