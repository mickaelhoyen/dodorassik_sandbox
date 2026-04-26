using Dodorassik.Api.Dtos;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

[ApiController]
[Route("api/admin/stats")]
[Authorize(Roles = "super_admin")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<UsageStatsDto>> Get()
    {
        var now = DateTime.UtcNow;
        var cutoff30 = now.AddDays(-30);
        var cutoff7 = now.AddDays(-7);

        var totalFamilies = await _db.Families.CountAsync();
        var totalUsers = await _db.Users.CountAsync();

        // A family is "active" if it made any submission in the last 30 days.
        var activeFamilies = await _db.Submissions
            .Where(s => s.ServerReceivedAtUtc >= cutoff30)
            .Select(s => s.FamilyId)
            .Distinct()
            .CountAsync();

        var totalHunts = await _db.Hunts.CountAsync();
        var publishedHunts = await _db.Hunts.CountAsync(h => h.Status == HuntStatus.Published);
        var pendingHunts = await _db.Hunts.CountAsync(h => h.Status == HuntStatus.Submitted);
        var totalSubmissions = await _db.Submissions.CountAsync(s => s.Accepted);
        var submissionsLast7 = await _db.Submissions.CountAsync(s => s.Accepted && s.ServerReceivedAtUtc >= cutoff7);
        var totalTemplates = await _db.StepTemplates.CountAsync();

        // Explicit join avoids navigation-property translation issues in EF GroupBy.
        var topHunts = await _db.Submissions
            .Where(s => s.Accepted)
            .Join(_db.HuntSteps, s => s.HuntStepId, hs => hs.Id, (s, hs) => new { hs.HuntId, s.FamilyId })
            .GroupBy(x => x.HuntId)
            .Select(g => new
            {
                HuntId = g.Key,
                TotalSubmissions = g.Count(),
                FamiliesPlayed = g.Select(x => x.FamilyId).Distinct().Count(),
            })
            .OrderByDescending(x => x.TotalSubmissions)
            .Take(10)
            .Join(_db.Hunts, x => x.HuntId, h => h.Id, (x, h) => new PopularHuntDto(
                h.Id,
                h.Name,
                x.FamiliesPlayed,
                x.TotalSubmissions))
            .ToListAsync();

        return new UsageStatsDto(
            totalFamilies,
            activeFamilies,
            totalUsers,
            totalHunts,
            publishedHunts,
            pendingHunts,
            totalSubmissions,
            submissionsLast7,
            totalTemplates,
            topHunts,
            now);
    }
}
