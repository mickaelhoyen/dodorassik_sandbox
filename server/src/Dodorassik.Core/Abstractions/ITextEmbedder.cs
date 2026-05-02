namespace Dodorassik.Core.Abstractions;

/// <summary>
/// Convertit un texte en vecteur d'embedding pour la recherche sémantique C2.
/// Retourne null si aucun embedder réel n'est configuré — le repository
/// bascule alors sur le scoring par tags.
/// </summary>
public interface ITextEmbedder
{
    Task<float[]?> EmbedAsync(string text, CancellationToken ct);
}
