using Dodorassik.Core.Abstractions;

namespace Dodorassik.Infrastructure.Assistant;

/// <summary>
/// Retourne null — active le scoring par tags dans le repository.
/// Remplacé en C3 par un embedder OpenAI ou équivalent.
/// </summary>
public class StubTextEmbedder : ITextEmbedder
{
    public Task<float[]?> EmbedAsync(string text, CancellationToken ct)
        => Task.FromResult<float[]?>(null);
}
