using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Domain;

namespace Dodorassik.Api.Dtos;

public record HuntDto(
    Guid Id,
    string Name,
    string Description,
    string Status,
    string Mode,
    string Category,
    Guid CreatorId,
    string? LocationLabel,
    DateTime? EventStartUtc,
    DateTime? EventEndUtc,
    DateTime? SubmittedAtUtc,
    DateTime? ReviewedAtUtc,
    string? RejectionReason,
    List<HuntStepDto> Steps,
    List<ClueDto> Clues);

public record HuntStepDto(
    Guid Id,
    int Order,
    string Title,
    string Description,
    string Type,
    JsonElement? Params,
    int Points,
    bool BlocksNext);

public record ClueDto(
    Guid Id,
    string Code,
    string Title,
    string Reveal,
    int Points);

public record CreateHuntRequest(
    [property: Required, StringLength(InputLimits.HuntNameMaxLength, MinimumLength = 1)]
    string Name,
    [property: StringLength(InputLimits.HuntDescriptionMaxLength)]
    string? Description,
    string? Mode,
    string? Category,
    string? LocationLabel,
    DateTime? EventStartUtc,
    DateTime? EventEndUtc,
    [property: MaxLength(InputLimits.StepsPerHuntMax)]
    List<CreateHuntStepRequest>? Steps,
    [property: MaxLength(InputLimits.CluesPerHuntMax)]
    List<CreateClueRequest>? Clues);

public record CreateHuntStepRequest(
    Guid? Id,
    [property: Required, StringLength(InputLimits.StepTitleMaxLength, MinimumLength = 1)]
    string Title,
    [property: StringLength(InputLimits.StepDescriptionMaxLength)]
    string? Description,
    [property: Required]
    string Type,
    JsonElement? Params,
    [property: Range(0, 1_000)]
    int? Points,
    bool? BlocksNext);

public record CreateClueRequest(
    Guid? Id,
    [property: Required, StringLength(InputLimits.ClueCodeMaxLength, MinimumLength = 1)]
    string Code,
    [property: StringLength(InputLimits.ClueTitleMaxLength)]
    string? Title,
    [property: StringLength(InputLimits.ClueRevealMaxLength)]
    string? Reveal,
    [property: Range(0, 1_000)]
    int? Points);

/// <summary>Full replace: fields absent from the request are cleared/reset.</summary>
public record UpdateHuntRequest(
    [property: Required, StringLength(InputLimits.HuntNameMaxLength, MinimumLength = 1)]
    string Name,
    [property: StringLength(InputLimits.HuntDescriptionMaxLength)]
    string? Description,
    string? Mode,
    string? LocationLabel,
    [property: MaxLength(InputLimits.StepsPerHuntMax)]
    List<CreateHuntStepRequest>? Steps,
    [property: MaxLength(InputLimits.CluesPerHuntMax)]
    List<CreateClueRequest>? Clues);

public record SubmitStepRequest(JsonElement Payload);

public record SubmitStepResponse(bool Accepted, int AwardedPoints, string? Message);

public static class HuntMappings
{
    public static HuntDto ToDto(this Hunt h) => new(
        h.Id,
        h.Name,
        h.Description,
        h.Status.ToString().ToLowerInvariant(),
        h.Mode.ToString().ToLowerInvariant(),
        h.Category.ToString().ToLowerInvariant(),
        h.CreatorId,
        h.LocationLabel,
        h.EventStartUtc,
        h.EventEndUtc,
        h.SubmittedAtUtc,
        h.ReviewedAtUtc,
        h.RejectionReason,
        h.Steps
            .OrderBy(s => s.Order)
            .Select(s => s.ToDto())
            .ToList(),
        h.Clues
            .Select(c => c.ToDto())
            .ToList());

    public static HuntStepDto ToDto(this HuntStep s)
    {
        JsonElement? parsed = null;
        if (!string.IsNullOrWhiteSpace(s.ParamsJson))
        {
            try { parsed = JsonDocument.Parse(s.ParamsJson).RootElement.Clone(); }
            catch { parsed = null; }
        }

        return new HuntStepDto(
            s.Id,
            s.Order,
            s.Title,
            s.Description,
            s.Type.ToString().ToLowerInvariant(),
            parsed,
            s.Points,
            s.BlocksNext);
    }

    public static ClueDto ToDto(this Clue c) => new(
        c.Id,
        c.Code,
        c.Title,
        c.Reveal,
        c.Points);

    public static StepType ParseStepType(string raw) => raw.ToLowerInvariant() switch
    {
        "manual" => StepType.Manual,
        "location" => StepType.Location,
        "photo" => StepType.Photo,
        "bluetooth" => StepType.Bluetooth,
        "text_answer" => StepType.TextAnswer,
        "clue_collect" => StepType.ClueCollect,
        _ => StepType.Manual,
    };

    public static HuntMode ParseHuntMode(string? raw) => (raw ?? "").ToLowerInvariant() switch
    {
        "competitive" => HuntMode.Competitive,
        _ => HuntMode.Relaxed,
    };

    public static HuntCategory ParseHuntCategory(string? raw) => (raw ?? "").ToLowerInvariant() switch
    {
        "event" => HuntCategory.Event,
        _ => HuntCategory.Permanent,
    };
}
