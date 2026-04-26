using System.Security.Claims;
using Dodorassik.Api.Dtos;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

/// <summary>
/// Teams are sub-groups within a family for competitive hunts (e.g. "Garçons" vs "Filles").
/// Endpoints: GET/POST /api/hunts/{huntId}/teams, POST join/leave.
/// </summary>
[ApiController]
[Route("api/hunts/{huntId:guid}/teams")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TeamsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<TeamDto>>> List(Guid huntId)
    {
        var hunt = await _db.Hunts.FindAsync(huntId);
        if (hunt is null) return NotFound();

        var teams = await _db.Teams
            .Include(t => t.Members)
            .ThenInclude(m => m.User)
            .Where(t => t.HuntId == huntId)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync();

        return teams.Select(t => t.ToDto()).ToList();
    }

    /// <summary>
    /// Creates a team for the caller's family. Fails with 409 if the family already
    /// has a team on this hunt. The creator is automatically added as a member.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TeamDto>> Create(Guid huntId, [FromBody] CreateTeamRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user?.FamilyId is null) return BadRequest(new { error = "no_family" });

        var hunt = await _db.Hunts.FindAsync(huntId);
        if (hunt is null) return NotFound();

        var team = new Team
        {
            Name = req.Name.Trim(),
            Color = req.Color?.Trim(),
            HuntId = huntId,
            FamilyId = user.FamilyId.Value,
        };
        team.Members.Add(new TeamMember { UserId = user.Id });

        _db.Teams.Add(team);
        await _db.SaveChangesAsync();

        await _db.Entry(team).Collection(t => t.Members).Query()
            .Include(m => m.User).LoadAsync();

        return CreatedAtAction(nameof(List), new { huntId }, team.ToDto());
    }

    /// <summary>Adds the current user to an existing team.</summary>
    [HttpPost("{teamId:guid}/join")]
    public async Task<ActionResult<TeamDto>> Join(Guid huntId, Guid teamId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user is null) return Unauthorized();

        var team = await _db.Teams
            .Include(t => t.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == teamId && t.HuntId == huntId);
        if (team is null) return NotFound();

        // Only members of the same family can join.
        if (team.FamilyId != user.FamilyId)
            return Forbid();

        if (team.Members.Any(m => m.UserId == user.Id))
            return Conflict(new { error = "already_member" });

        team.Members.Add(new TeamMember { TeamId = teamId, UserId = user.Id });
        await _db.SaveChangesAsync();

        return team.ToDto();
    }

    /// <summary>Removes the current user from their team on this hunt.</summary>
    [HttpDelete("{teamId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid huntId, Guid teamId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var member = await _db.TeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId.Value);
        if (member is null) return NotFound();

        _db.TeamMembers.Remove(member);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Guid? CurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
