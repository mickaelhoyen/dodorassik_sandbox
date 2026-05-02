namespace Dodorassik.Core.Domain.Assistant;

public record SponsorConstraint(
    string Brand,
    string Category,
    IReadOnlyList<string> Constraints);
