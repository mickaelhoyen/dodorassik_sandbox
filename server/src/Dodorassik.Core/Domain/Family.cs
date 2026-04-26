namespace Dodorassik.Core.Domain;

/// <summary>
/// A family groups one or more phone-holding adults plus the kids that play
/// without a phone. Useful for scoring (a single team result for all kids of
/// the family) and for cross-device sync of pending submissions.
/// </summary>
public class Family
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<User> Members { get; set; } = new();
    public List<HuntScore> Scores { get; set; } = new();
}
