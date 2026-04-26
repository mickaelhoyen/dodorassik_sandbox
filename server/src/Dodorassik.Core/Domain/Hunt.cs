namespace Dodorassik.Core.Domain;

/// <summary>
/// A treasure-hunt parcours authored by a creator. Contains an ordered list
/// of <see cref="HuntStep"/>s and optional reusable <see cref="Clue"/>s the
/// kids physically collect during the run.
/// </summary>
public class Hunt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatorId { get; set; }
    public User? Creator { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? LocationLabel { get; set; }

    public HuntStatus Status { get; set; } = HuntStatus.Draft;
    public HuntMode Mode { get; set; } = HuntMode.Relaxed;
    public HuntCategory Category { get; set; } = HuntCategory.Permanent;

    // Non-null only when Category == HuntCategory.Event.
    public DateTime? EventStartUtc { get; set; }
    public DateTime? EventEndUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<HuntStep> Steps { get; set; } = new();
    public List<Clue> Clues { get; set; } = new();
    public List<HuntScore> Scores { get; set; } = new();
}
