using System.ComponentModel.DataAnnotations;
using Dodorassik.Core.Domain;

namespace Dodorassik.Api.Dtos;

public record TeamDto(
    Guid Id,
    string Name,
    string? Color,
    Guid HuntId,
    Guid FamilyId,
    DateTime CreatedAtUtc,
    List<TeamMemberDto> Members);

public record TeamMemberDto(Guid UserId, string DisplayName, DateTime JoinedAtUtc);

public record CreateTeamRequest(
    [Required, StringLength(64, MinimumLength = 1)]
    string Name,
    [StringLength(7)]
    string? Color);

public record LeaderboardEntryDto(
    int Rank,
    Guid? TeamId,
    string TeamName,
    Guid FamilyId,
    string FamilyName,
    int TotalPoints,
    int StepsCompleted,
    int TotalSteps,
    TimeSpan? Duration,
    bool Finished);

public record LeaderboardDto(
    Guid HuntId,
    string HuntMode,
    List<LeaderboardEntryDto> Rankings,
    DateTime GeneratedAtUtc);

public static class TeamMappings
{
    public static TeamDto ToDto(this Team t) => new(
        t.Id,
        t.Name,
        t.Color,
        t.HuntId,
        t.FamilyId,
        t.CreatedAtUtc,
        t.Members.Select(m => new TeamMemberDto(
            m.UserId,
            m.User?.DisplayName ?? "?",
            m.JoinedAtUtc)).ToList());
}
