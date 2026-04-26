using System.Security.Claims;
using Dodorassik.Api.Dtos;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

/// <summary>
/// GDPR endpoints. Each one operates on the *current* user only — admins
/// don't act on behalf of users without explicit ticketing (out of scope
/// for the API).
/// </summary>
[ApiController]
[Route("api/users/me")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<UsersController> _log;

    public UsersController(AppDbContext db, ILogger<UsersController> log)
    {
        _db = db;
        _log = log;
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> Me()
    {
        var u = await CurrentUserAsync();
        return u is null ? Unauthorized() : new UserDto(u.Id, u.Email, u.DisplayName, Auth.JwtTokenService.RoleToSnake(u.Role), u.FamilyId);
    }

    [HttpPatch]
    public async Task<ActionResult<UserDto>> Update([FromBody] UpdateProfileRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var u = await CurrentUserAsync();
        if (u is null) return Unauthorized();

        if (!string.IsNullOrWhiteSpace(req.DisplayName)) u.DisplayName = req.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var newEmail = req.Email.Trim().ToLowerInvariant();
            if (newEmail != u.Email && await _db.Users.AnyAsync(x => x.Email == newEmail))
                return Conflict(new { error = "email_taken" });
            u.Email = newEmail;
        }
        await _db.SaveChangesAsync();
        return new UserDto(u.Id, u.Email, u.DisplayName, Auth.JwtTokenService.RoleToSnake(u.Role), u.FamilyId);
    }

    /// <summary>
    /// GDPR portability — returns every record concerning the user. The
    /// response is JSON; the client is responsible for offering a download.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var u = await CurrentUserAsync();
        if (u is null) return Unauthorized();

        var huntsCreated = await _db.Hunts
            .Where(h => h.CreatorId == u.Id)
            .Select(h => new { h.Id, h.Name, h.Status, h.CreatedAtUtc })
            .ToListAsync();

        var submissions = await _db.Submissions
            .Where(s => s.SubmittedById == u.Id)
            .Select(s => new { s.Id, s.HuntStepId, s.AwardedPoints, s.ClientCreatedAtUtc, s.ServerReceivedAtUtc })
            .ToListAsync();

        return Ok(new
        {
            exportedAtUtc = DateTime.UtcNow,
            user = new { u.Id, u.Email, u.DisplayName, role = u.Role.ToString(), u.CreatedAtUtc, u.LastLoginUtc, u.FamilyId },
            huntsCreated,
            submissions,
        });
    }

    /// <summary>
    /// GDPR erasure. Deletes the user record and anonymises their submissions
    /// (we keep the row so leaderboards stay consistent, but link is broken).
    /// Hunts the user authored stay published — they belong to the platform
    /// once shared, but the creator name is detached.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] string? confirm)
    {
        if (confirm != "yes") return BadRequest(new { error = "confirmation_required", hint = "?confirm=yes" });

        var u = await CurrentUserAsync();
        if (u is null) return Unauthorized();

        await _db.Submissions
            .Where(s => s.SubmittedById == u.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.SubmittedById, Guid.Empty));

        await _db.Hunts
            .Where(h => h.CreatorId == u.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.CreatorId, Guid.Empty));

        _db.Users.Remove(u);
        await _db.SaveChangesAsync();

        _log.LogInformation("User erased (GDPR) {UserId}", u.Id);
        return NoContent();
    }

    private async Task<User?> CurrentUserAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? await _db.Users.FirstOrDefaultAsync(u => u.Id == id) : null;
    }
}
