namespace Dodorassik.Core.Domain.Assistant;

public record PhotoAnalysisResult(
    string FileName,
    string? SceneDescription,
    IReadOnlyList<string> DetectedElements,
    string? ArchitectureStyle);
