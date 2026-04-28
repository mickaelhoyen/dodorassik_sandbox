using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

[ApiController]
[Route("api/families")]
[Authorize]
public class FamiliesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FamiliesController(AppDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<ActionResult<FamilyDto>> Mine()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        if (user.FamilyId is null) return NotFound();
        var family = await _db.Families.FirstAsync(f => f.Id == user.FamilyId);
        return new FamilyDto(family.Id, family.Name);
    }

    [HttpPost]
    public async Task<ActionResult<FamilyDto>> Create([FromBody] CreateFamilyRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        if (user.FamilyId is not null) return Conflict(new { error = "already_in_family" });

        var family = new Family { Name = req.Name.Trim() };
        _db.Families.Add(family);
        user.Family = family;
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Mine), new FamilyDto(family.Id, family.Name));
    }

    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        if (user.FamilyId is not null) return Conflict(new { error = "already_in_family" });

        var family = await _db.Families.FirstOrDefaultAsync(f => f.Id == id);
        if (family is null) return NotFound();

        user.Family = family;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        user.FamilyId = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<User?> CurrentUserAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? await _db.Users.FirstOrDefaultAsync(u => u.Id == id) : null;
    }
}

public record FamilyDto(Guid Id, string Name);

public record CreateFamilyRequest(
    [Required, StringLength(InputLimits.DisplayNameMaxLength, MinimumLength = 1)]
    string Name);
