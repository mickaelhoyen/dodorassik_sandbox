using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Dodorassik.Api.Dtos;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

/// <summary>
/// Super-administrator moderation surface. The "Validation par le super-admin
/// avant publication publique" invariant from <c>docs/PRIVACY.md</c> requires
/// that no hunt be exposed to families until a human reviewer has approved it
/// — this controller is the single approval choke-point.
/// </summary>
[ApiController]
[Route("api/admin/hunts")]
[Authorize(Roles = "super_admin")]
public class AdminHuntsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminHuntsController> _log;

    public AdminHuntsController(AppDbContext db, ILogger<AdminHuntsController> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>
    /// Moderation queue. Defaults to <c>?status=submitted</c> so the
    /// reviewer immediately sees what's waiting on them.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<HuntDto>>> Queue([FromQuery] string status = "submitted")
    {
        if (!Enum.TryParse<HuntStatus>(status, ignoreCase: true, out var parsed))
            return BadRequest(new { error = "invalid_status" });

        var hunts = await _db.Hunts
            .Include(h => h.Steps)
            .Include(h => h.Clues)
            .Where(h => h.Status == parsed)
            .OrderBy(h => h.SubmittedAtUtc ?? h.UpdatedAtUtc)
            .ToListAsync();
        return hunts.Select(h => h.ToDto()).ToList();
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var hunt = await _db.Hunts.FindAsync(id);
        if (hunt is null) return NotFound();
        if (hunt.Status != HuntStatus.Submitted)
            return Conflict(new { error = "invalid_status_transition", current = hunt.Status.ToString().ToLowerInvariant() });

        hunt.Status = HuntStatus.Published;
        hunt.RejectionReason = null;
        hunt.ReviewedAtUtc = DateTime.UtcNow;
        hunt.ReviewedById = CurrentUserId();
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _log.LogInformation("Hunt {HuntId} approved by {ReviewerId}", hunt.Id, hunt.ReviewedById);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectHuntRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var hunt = await _db.Hunts.FindAsync(id);
        if (hunt is null) return NotFound();
        if (hunt.Status != HuntStatus.Submitted)
            return Conflict(new { error = "invalid_status_transition", current = hunt.Status.ToString().ToLowerInvariant() });

        hunt.Status = HuntStatus.Rejected;
        hunt.RejectionReason = req.Reason.Trim();
        hunt.ReviewedAtUtc = DateTime.UtcNow;
        hunt.ReviewedById = CurrentUserId();
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _log.LogInformation("Hunt {HuntId} rejected by {ReviewerId}", hunt.Id, hunt.ReviewedById);
        return NoContent();
    }

    /// <summary>
    /// Force-takedown: a super-admin can pull a hunt offline at any time
    /// (e.g. after a complaint). Bypasses the normal Archive flow.
    /// </summary>
    [HttpPost("{id:guid}/takedown")]
    public async Task<IActionResult> Takedown(Guid id, [FromBody] RejectHuntRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var hunt = await _db.Hunts.FindAsync(id);
        if (hunt is null) return NotFound();

        hunt.Status = HuntStatus.Rejected;
        hunt.RejectionReason = req.Reason.Trim();
        hunt.ReviewedAtUtc = DateTime.UtcNow;
        hunt.ReviewedById = CurrentUserId();
        hunt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _log.LogWarning("Hunt {HuntId} taken down by {ReviewerId}", hunt.Id, hunt.ReviewedById);
        return NoContent();
    }

    private Guid? CurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public record RejectHuntRequest(
    [Required, StringLength(InputLimits.HuntDescriptionMaxLength, MinimumLength = 5)]
    string Reason);
