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
    /// <summary>Creator is still editing.</summary>
    Draft = 0,
    /// <summary>Approved by a super-admin and visible publicly.</summary>
    Published = 1,
    /// <summary>Removed from listings by the creator.</summary>
    Archived = 2,
    /// <summary>Creator submitted for super-admin review (moderation queue).</summary>
    Submitted = 3,
    /// <summary>Super-admin rejected. <see cref="Hunt.RejectionReason"/> explains why.</summary>
    Rejected = 4,
}

public enum HuntMode
{
    /// <summary>Relaxed family run, everyone wins, no timing.</summary>
    Relaxed = 0,
    /// <summary>Competitive: timing and ordering matter.</summary>
    Competitive = 1
}

public enum HuntCategory
{
    /// <summary>Always available published hunt (e.g. a permanent nature trail).</summary>
    Permanent = 0,
    /// <summary>Time-bounded event hunt (festival, seasonal edition…).</summary>
    Event = 1,
}

/// <summary>
/// Niveau d'abonnement d'un créateur.
/// Free : joueur + créateur sans accès à la couche C3 (Claude).
/// Pro : accès illimité à la génération IA (C3).
/// Enterprise : idem Pro, limites plus hautes, SLA dédié.
/// </summary>
public enum SubscriptionTier
{
    Free = 0,
    Pro = 1,
    Enterprise = 2,
}
