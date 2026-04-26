using System.Text.Json;
using Dodorassik.Core.Domain;

namespace Dodorassik.Api.Dtos;

public record HuntDto(
    Guid Id,
    string Name,
    string Description,
    string Status,
    string Mode,
    Guid CreatorId,
    List<HuntStepDto> Steps);

public record HuntStepDto(
    Guid Id,
    int Order,
    string Title,
    string Description,
    string Type,
    JsonElement? Params,
    int Points,
    bool BlocksNext);

public record CreateHuntRequest(
    string Name,
    string? Description,
    string? Mode,
    List<CreateHuntStepRequest>? Steps);

public record CreateHuntStepRequest(
    Guid? Id,
    string Title,
    string? Description,
    string Type,
    JsonElement? Params,
    int? Points,
    bool? BlocksNext);

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
        h.CreatorId,
        h.Steps
            .OrderBy(s => s.Order)
            .Select(s => s.ToDto())
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
}
