namespace Dodorassik.Core.Domain;

/// <summary>
/// A team is a sub-group within a family that competes independently in a
/// competitive hunt (e.g. "Garçons" vs "Filles"). A family may have at most
/// one team per hunt.
/// </summary>
public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Hex colour used to distinguish teams on the leaderboard (optional).</summary>
    public string? Color { get; set; }

    public Guid HuntId { get; set; }
    public Hunt? Hunt { get; set; }

    public Guid FamilyId { get; set; }
    public Family? Family { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<TeamMember> Members { get; set; } = new();
}

/// <summary>Join between a <see cref="Team"/> and a <see cref="User"/>.</summary>
public class TeamMember
{
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
