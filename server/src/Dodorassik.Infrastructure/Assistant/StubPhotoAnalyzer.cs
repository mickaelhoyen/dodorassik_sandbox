using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain.Assistant;

namespace Dodorassik.Infrastructure.Assistant;

/// <summary>
/// Placeholder jusqu'à C3 (Claude Vision). Les photos ne sont pas lues
/// ni stockées — seul le nom de fichier est retourné.
/// </summary>
public class StubPhotoAnalyzer : IPhotoAnalyzer
{
    public Task<PhotoAnalysisResult> AnalyzeAsync(string fileName, Stream photoStream, CancellationToken ct)
        => Task.FromResult(new PhotoAnalysisResult(
            FileName: fileName,
            SceneDescription: null,
            DetectedElements: [],
            ArchitectureStyle: null));
}
