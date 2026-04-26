namespace Dodorassik.Api.Dtos;

public record UsageStatsDto(
    int TotalFamilies,
    int ActiveFamiliesLast30Days,
    int TotalUsers,
    int TotalHunts,
    int PublishedHunts,
    int PendingReviewHunts,
    int TotalSubmissions,
    int SubmissionsLast7Days,
    int TotalStepTemplates,
    List<PopularHuntDto> TopHunts,
    DateTime GeneratedAtUtc);

public record PopularHuntDto(
    Guid Id,
    string Name,
    int FamiliesPlayed,
    int TotalSubmissions);
