using System.Text.Json;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Services;

public sealed class AntiCheatService : IAntiCheatService
{
    private readonly AppDbContext _db;

    public AntiCheatService(AppDbContext db) => _db = db;

    public double MaxSpeedKmh => 40.0;

    public async Task<bool> IsStepOrderValidAsync(
        Guid huntId, Guid stepId, Guid familyId, Guid? teamId,
        CancellationToken ct = default)
    {
        // Load the hunt steps ordered by position.
        var steps = await _db.HuntSteps
            .Where(s => s.HuntId == huntId)
            .OrderBy(s => s.Order)
            .Select(s => new { s.Id, s.Order, s.BlocksNext })
            .ToListAsync(ct);

        var targetIndex = steps.FindIndex(s => s.Id == stepId);
        if (targetIndex <= 0) return true; // first step or not found — always valid

        // Collect all steps that must be completed before the target.
        var prerequisiteIds = steps
            .Take(targetIndex)
            .Where(s => s.BlocksNext)
            .Select(s => s.Id)
            .ToHashSet();

        if (prerequisiteIds.Count == 0) return true;

        // Check that we have accepted submissions for each prerequisite.
        var completedIds = await _db.Submissions
            .Where(sub =>
                sub.Step != null &&
                sub.Step.HuntId == huntId &&
                sub.FamilyId == familyId &&
                (teamId == null || sub.TeamId == teamId) &&
                sub.Accepted &&
                prerequisiteIds.Contains(sub.HuntStepId))
            .Select(sub => sub.HuntStepId)
            .Distinct()
            .ToListAsync(ct);

        return prerequisiteIds.All(id => completedIds.Contains(id));
    }

    public async Task<bool> IsGpsSpeedPlausibleAsync(
        Guid huntId, Guid familyId, Guid? teamId,
        double? newLat, double? newLon, DateTime clientTimestampUtc,
        CancellationToken ct = default)
    {
        if (newLat is null || newLon is null) return true;

        // Find the most recent location-type submission for this family/team.
        var previous = await _db.Submissions
            .Where(sub =>
                sub.Step != null &&
                sub.Step.HuntId == huntId &&
                sub.Step.Type == StepType.Location &&
                sub.FamilyId == familyId &&
                (teamId == null || sub.TeamId == teamId) &&
                sub.Accepted)
            .OrderByDescending(sub => sub.ClientCreatedAtUtc)
            .Select(sub => new { sub.PayloadJson, sub.ClientCreatedAtUtc })
            .FirstOrDefaultAsync(ct);

        if (previous is null) return true;

        double? prevLat = null, prevLon = null;
        try
        {
            var doc = JsonDocument.Parse(previous.PayloadJson);
            if (doc.RootElement.TryGetProperty("lat", out var latEl))
                prevLat = latEl.GetDouble();
            if (doc.RootElement.TryGetProperty("lon", out var lonEl))
                prevLon = lonEl.GetDouble();
        }
        catch { return true; }

        if (prevLat is null || prevLon is null) return true;

        var elapsedHours = (clientTimestampUtc - previous.ClientCreatedAtUtc).TotalHours;
        if (elapsedHours <= 0) return true;

        var distanceKm = HaversineKm(prevLat.Value, prevLon.Value, newLat.Value, newLon.Value);
        var speedKmh = distanceKm / elapsedHours;

        return speedKmh <= MaxSpeedKmh;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
