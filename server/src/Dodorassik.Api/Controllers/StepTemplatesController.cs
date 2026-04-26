using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dodorassik.Api.Dtos;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

/// <summary>
/// Community / personal library of reusable step templates.
/// Creators save frequently-used steps here so they can be applied directly
/// in the hunt editor.
/// </summary>
[ApiController]
[Route("api/step-templates")]
[Authorize(Roles = "creator,super_admin")]
public class StepTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public StepTemplatesController(AppDbContext db) => _db = db;

    /// <summary>
    /// Search templates. By default returns public templates + the caller's own.
    /// <c>?mine=true</c> limits to the caller's templates regardless of visibility.
    /// <c>?type=location</c> filters by step type.
    /// <c>?tag=nature</c> filters by tag.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<StepTemplateDto>>> Search(
        [FromQuery] bool mine = false,
        [FromQuery] string? type = null,
        [FromQuery] string? tag = null)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var query = _db.StepTemplates
            .Include(t => t.CreatedBy)
            .AsQueryable();

        if (mine)
        {
            query = query.Where(t => t.CreatedById == userId.Value);
        }
        else
        {
            query = query.Where(t => t.IsPublic || t.CreatedById == userId.Value);
        }

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<StepType>(type, ignoreCase: true, out var stepType))
            query = query.Where(t => t.Type == stepType);

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(t => t.Tags.Contains(tag));

        var results = await query
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Take(100)
            .ToListAsync();

        return results.Select(t => t.ToDto()).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StepTemplateDto>> Get(Guid id)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var template = await _db.StepTemplates
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template is null) return NotFound();
        if (!template.IsPublic && template.CreatedById != userId.Value && !User.IsInRole("super_admin"))
            return Forbid();

        return template.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<StepTemplateDto>> Create([FromBody] CreateStepTemplateRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var paramsJson = req.Params?.GetRawText() ?? "{}";
        if (Encoding.UTF8.GetByteCount(paramsJson) > InputLimits.JsonParamsMaxBytes)
            return BadRequest(new { error = "params_too_large" });

        var template = new StepTemplate
        {
            Title = req.Title.Trim(),
            Description = req.Description?.Trim() ?? string.Empty,
            Type = HuntMappings.ParseStepType(req.Type),
            ParamsJson = paramsJson,
            Tags = req.Tags?.Trim().ToLowerInvariant() ?? string.Empty,
            DefaultPoints = req.DefaultPoints ?? 10,
            IsPublic = req.IsPublic,
            CreatedById = userId.Value,
        };

        _db.StepTemplates.Add(template);
        await _db.SaveChangesAsync();
        await _db.Entry(template).Reference(t => t.CreatedBy).LoadAsync();

        return CreatedAtAction(nameof(Get), new { id = template.Id }, template.ToDto());
    }

    /// <summary>Soft-update: title, description, tags, visibility only.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<StepTemplateDto>> Update(Guid id, [FromBody] CreateStepTemplateRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var template = await _db.StepTemplates.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
        if (template is null) return NotFound();
        if (template.CreatedById != userId.Value && !User.IsInRole("super_admin"))
            return Forbid();

        template.Title = req.Title.Trim();
        template.Description = req.Description?.Trim() ?? string.Empty;
        template.Tags = req.Tags?.Trim().ToLowerInvariant() ?? string.Empty;
        template.IsPublic = req.IsPublic;
        template.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return template.ToDto();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var template = await _db.StepTemplates.FindAsync(id);
        if (template is null) return NotFound();
        if (template.CreatedById != userId.Value && !User.IsInRole("super_admin"))
            return Forbid();

        _db.StepTemplates.Remove(template);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Guid? CurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
