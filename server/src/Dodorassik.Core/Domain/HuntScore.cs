namespace Dodorassik.Core.Domain;

/// <summary>
/// Aggregated score for a family on a given hunt. Materialised so leader
/// boards stay fast regardless of how many submissions the family produced.
/// </summary>
public class HuntScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HuntId { get; set; }
    public Hunt? Hunt { get; set; }

    public Guid FamilyId { get; set; }
    public Family? Family { get; set; }

    public int TotalPoints { get; set; }
    public int StepsCompleted { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>Total run duration; null while the hunt is still ongoing.</summary>
    public TimeSpan? Duration =>
        StartedAtUtc.HasValue && FinishedAtUtc.HasValue
            ? FinishedAtUtc - StartedAtUtc
            : null;
}
