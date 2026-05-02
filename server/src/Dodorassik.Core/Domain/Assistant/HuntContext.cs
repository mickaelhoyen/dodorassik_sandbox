namespace Dodorassik.Core.Domain.Assistant;

public record HuntContext(
    LocationContext Location,
    AudienceProfile Audience,
    IReadOnlyList<SponsorConstraint> Sponsors,
    IReadOnlyList<PhotoAnalysisResult> PhotoAnalyses,
    IReadOnlyList<GpsPoint> GpsPoints);
