using Dodorassik.Core.Domain.Assistant;

namespace Dodorassik.Core.Abstractions;

public interface IPhotoAnalyzer
{
    Task<PhotoAnalysisResult> AnalyzeAsync(string fileName, Stream photoStream, CancellationToken ct);
}
