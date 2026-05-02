namespace Dodorassik.Core.Domain.Assistant;

public record NearbyPoi(string Name, string Type, double DistanceMeters);

public record WikidataFact(string Label, string? Description, string WikidataId);

public record LocationContext(
    string? PlaceName,
    IReadOnlyList<NearbyPoi> Pois,
    IReadOnlyList<WikidataFact> HistoricalFacts);
