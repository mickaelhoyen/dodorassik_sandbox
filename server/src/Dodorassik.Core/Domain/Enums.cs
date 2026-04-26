namespace Dodorassik.Core.Domain;

public enum UserRole
{
    Player = 0,
    Creator = 1,
    SuperAdmin = 2
}

/// <summary>
/// Validation type used by a <see cref="HuntStep"/>. Kept as an open enum
/// so we can extend the gameplay without shipping a schema migration each
/// time (params for each type live in <see cref="HuntStep.ParamsJson"/>).
/// </summary>
public enum StepType
{
    Manual = 0,
    Location = 1,
    Photo = 2,
    Bluetooth = 3,
    TextAnswer = 4,
    ClueCollect = 5
}

public enum HuntStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public enum HuntMode
{
    /// <summary>Relaxed family run, everyone wins, no timing.</summary>
    Relaxed = 0,
    /// <summary>Competitive: timing and ordering matter.</summary>
    Competitive = 1
}
