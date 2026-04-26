using Dodorassik.Core.Domain;

namespace Dodorassik.Core.Abstractions;

/// <summary>
/// Validates step submissions in competitive hunts to detect common cheating
/// patterns: skipping steps or physically impossible movement speed.
/// </summary>
public interface IAntiCheatService
{
    /// <summary>
    /// Returns true if the family/team has already completed all steps that must
    /// be solved before <paramref name="step"/> (i.e. no earlier BlocksNext step
    /// has been skipped).
    /// </summary>
    Task<bool> IsStepOrderValidAsync(
        Guid huntId,
        Guid stepId,
        Guid familyId,
        Guid? teamId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true if moving from the previous GPS submission to the current one
    /// is physically plausible (under <see cref="MaxSpeedKmh"/>).
    /// Returns true unconditionally when there is no previous GPS submission or
    /// when either payload lacks coordinates.
    /// </summary>
    Task<bool> IsGpsSpeedPlausibleAsync(
        Guid huntId,
        Guid familyId,
        Guid? teamId,
        double? newLat,
        double? newLon,
        DateTime clientTimestampUtc,
        CancellationToken ct = default);

    /// <summary>Max plausible speed in km/h. Defaults to 40 (bike/light jog).</summary>
    double MaxSpeedKmh { get; }
}
