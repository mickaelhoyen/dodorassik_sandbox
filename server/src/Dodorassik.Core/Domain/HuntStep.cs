namespace Dodorassik.Core.Domain;

/// <summary>
/// A single ordered step within a <see cref="Hunt"/>. The validation rule
/// depends on <see cref="Type"/>; type-specific parameters live in
/// <see cref="ParamsJson"/> (e.g. {"lat":48.86,"lon":2.34,"radius_m":30}
/// for <see cref="StepType.Location"/>). The client keeps schema parsing on
/// its side so we can introduce new step types server-first without touching
/// the database.
/// </summary>
public class HuntStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HuntId { get; set; }
    public Hunt? Hunt { get; set; }

    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public StepType Type { get; set; } = StepType.Manual;

    /// <summary>JSON blob with type-specific parameters.</summary>
    public string ParamsJson { get; set; } = "{}";

    /// <summary>If true, this step must be solved before the next becomes visible.</summary>
    public bool BlocksNext { get; set; } = true;

    public int Points { get; set; } = 10;
}
