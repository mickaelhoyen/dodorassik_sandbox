namespace Dodorassik.Core.Domain;

/// <summary>
/// A reusable step definition that creators can save and share. Public
/// templates are browsable by all creators; private ones are only visible
/// to their author. No player-identifiable data is stored here.
/// </summary>
public class StepTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public StepType Type { get; set; } = StepType.Manual;

    /// <summary>Default params for the step (e.g. radius, expected answer).</summary>
    public string ParamsJson { get; set; } = "{}";

    /// <summary>Comma-separated tags (e.g. "nature,ville,enigme").</summary>
    public string Tags { get; set; } = string.Empty;

    public int DefaultPoints { get; set; } = 10;

    /// <summary>If true, visible to all creators in the community library.</summary>
    public bool IsPublic { get; set; } = false;

    public Guid CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
