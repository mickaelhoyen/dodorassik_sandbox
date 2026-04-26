using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Domain;

namespace Dodorassik.Api.Dtos;

public record StepTemplateDto(
    Guid Id,
    string Title,
    string Description,
    string Type,
    JsonElement? Params,
    string Tags,
    int DefaultPoints,
    bool IsPublic,
    Guid CreatedById,
    string CreatedByName,
    DateTime CreatedAtUtc);

public record CreateStepTemplateRequest(
    [property: Required, StringLength(InputLimits.StepTitleMaxLength, MinimumLength = 1)]
    string Title,
    [property: StringLength(InputLimits.StepDescriptionMaxLength)]
    string? Description,
    [property: Required]
    string Type,
    JsonElement? Params,
    [property: StringLength(512)]
    string? Tags,
    [property: Range(0, 1_000)]
    int? DefaultPoints,
    bool IsPublic = false);

public static class StepTemplateMappings
{
    public static StepTemplateDto ToDto(this StepTemplate t)
    {
        JsonElement? parsed = null;
        if (!string.IsNullOrWhiteSpace(t.ParamsJson))
        {
            try { parsed = JsonDocument.Parse(t.ParamsJson).RootElement.Clone(); }
            catch { parsed = null; }
        }

        return new StepTemplateDto(
            t.Id,
            t.Title,
            t.Description,
            t.Type.ToString().ToLowerInvariant(),
            parsed,
            t.Tags,
            t.DefaultPoints,
            t.IsPublic,
            t.CreatedById,
            t.CreatedBy?.DisplayName ?? "?",
            t.CreatedAtUtc);
    }
}
