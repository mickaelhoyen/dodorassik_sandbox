using Dodorassik.Core.Domain.Assistant;

namespace Dodorassik.Core.Abstractions;

/// <summary>
/// Accès en lecture à la base de mécaniques de jeux.
/// Utilisé par C2 pour retrouver les fiches les plus pertinentes
/// par rapport au contexte d'une chasse.
/// </summary>
public interface IGameKnowledgeRepository
{
    /// <summary>
    /// Retourne les <paramref name="limit"/> fiches les plus similaires
    /// au contexte décrit par <paramref name="queryText"/> et
    /// au profil <paramref name="audience"/>.
    ///
    /// Stratégie : cosine similarity pgvector si l'embedding est disponible,
    /// sinon scoring par chevauchement de tags + adéquation d'audience.
    /// </summary>
    Task<IReadOnlyList<RagHit>> FindSimilarAsync(
        string queryText,
        AudienceProfile audience,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Nombre total d'entrées dans la base de mécaniques.
    /// Utile pour diagnostiquer si le peuplement a été effectué.
    /// </summary>
    Task<int> CountAsync(CancellationToken ct);
}
