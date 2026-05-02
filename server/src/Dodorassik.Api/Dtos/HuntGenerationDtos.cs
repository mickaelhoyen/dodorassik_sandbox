using System.ComponentModel.DataAnnotations;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Domain.Assistant;

namespace Dodorassik.Api.Dtos;

// ─── Request DTOs ──────────────────────────────────────────────────────────

public record AudienceProfileDto(
    [Range(0, 120)] int AgeMin,
    [Range(0, 120)] int AgeMax,
    [Range(1, 100)] int GroupSize,
    MobilityLevel Mobility,
    [Range(5, 480)] int DurationMinutes,
    [StringLength(10, MinimumLength = 2)] string Language);

public record GpsPointDto(
    [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude,
    [StringLength(InputLimits.StepTitleMaxLength)] string? Label);

public record SponsorConstraintDto(
    [Required, StringLength(128, MinimumLength = 1)] string Brand,
    [Required, StringLength(128, MinimumLength = 1)] string Category,
    IReadOnlyList<string> Constraints);

// ─── Response DTOs ─────────────────────────────────────────────────────────

public record NearbyPoiDto(string Name, string Type, double DistanceMeters);

public record WikidataFactDto(string Label, string? Description, string WikidataId);

public record LocationContextDto(
    string? PlaceName,
    IReadOnlyList<NearbyPoiDto> Pois,
    IReadOnlyList<WikidataFactDto> HistoricalFacts);

public record PhotoAnalysisResultDto(
    string FileName,
    string? SceneDescription,
    IReadOnlyList<string> DetectedElements,
    string? ArchitectureStyle);

public record HuntContextDto(
    LocationContextDto Location,
    AudienceProfileDto Audience,
    IReadOnlyList<SponsorConstraintDto> Sponsors,
    IReadOnlyList<PhotoAnalysisResultDto> PhotoAnalyses,
    IReadOnlyList<GpsPointDto> GpsPoints);
