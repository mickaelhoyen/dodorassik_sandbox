using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain.Assistant;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;

namespace Dodorassik.Infrastructure.Assistant;

public class GameKnowledgeRepository : IGameKnowledgeRepository
{
    private readonly AppDbContext _db;
    private readonly ITextEmbedder _embedder;
    private readonly ILogger<GameKnowledgeRepository> _logger;

    public GameKnowledgeRepository(
        AppDbContext db,
        ITextEmbedder embedder,
        ILogger<GameKnowledgeRepository> logger)
    {
        _db = db;
        _embedder = embedder;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RagHit>> FindSimilarAsync(
        string queryText,
        AudienceProfile audience,
        int limit,
        CancellationToken ct)
    {
        var embedding = await _embedder.EmbedAsync(queryText, ct);

        return embedding is not null
            ? await VectorSearchAsync(embedding, audience, limit, ct)
            : await TagSearchAsync(queryText, audience, limit, ct);
    }

    public Task<int> CountAsync(CancellationToken ct)
        => _db.GameMechanics.CountAsync(ct);

    // ─── Recherche vectorielle (pgvector cosine) ───────────────────────────

    private async Task<IReadOnlyList<RagHit>> VectorSearchAsync(
        float[] queryEmbedding,
        AudienceProfile audience,
        int limit,
        CancellationToken ct)
    {
        // Cosine distance [0..2], plus petit = plus similaire.
        // On filtre d'abord par tranche d'âge pour éviter des propositions
        // inadaptées, puis on trie par distance vectorielle.
        var results = await _db.GameMechanics
            .Where(g => g.Embedding != null)
            .Where(g => g.AgeMin <= audience.AgeMax && (g.AgeMax == null || g.AgeMax >= audience.AgeMin))
            .OrderBy(g => EF.Functions.CosineDistance(g.Embedding!, queryEmbedding))
            .Take(limit)
            .Select(g => new
            {
                g.Id, g.Title, g.SourceGame, g.Mechanics, g.Themes,
                g.AgeMin, g.AgeMax, g.DurationMinutes, g.Format,
                Distance = EF.Functions.CosineDistance(g.Embedding!, queryEmbedding)
            })
            .ToListAsync(ct);

        return results
            .Select(r => new RagHit(
                r.Id, r.Title, r.SourceGame, r.Mechanics, r.Themes,
                r.AgeMin, r.AgeMax, r.DurationMinutes, r.Format,
                Score: 1f - (float)(r.Distance / 2f)))
            .ToList();
    }

    // ─── Scoring par tags (fallback sans embedder) ─────────────────────────

    private async Task<IReadOnlyList<RagHit>> TagSearchAsync(
        string queryText,
        AudienceProfile audience,
        int limit,
        CancellationToken ct)
    {
        // Extrait des mots-clés simples depuis le texte de requête pour le
        // chevauchement de tags — suffisant pour un prototype fonctionnel.
        var keywords = ExtractKeywords(queryText);

        var candidates = await _db.GameMechanics
            .Where(g => g.AgeMin <= audience.AgeMax && (g.AgeMax == null || g.AgeMax >= audience.AgeMin))
            .ToListAsync(ct);

        return candidates
            .Select(g => new { Game = g, Score = ComputeTagScore(g, keywords, audience) })
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => new RagHit(
                x.Game.Id, x.Game.Title, x.Game.SourceGame,
                x.Game.Mechanics, x.Game.Themes,
                x.Game.AgeMin, x.Game.AgeMax, x.Game.DurationMinutes, x.Game.Format,
                x.Score))
            .ToList();
    }

    private static float ComputeTagScore(GameMechanic g, HashSet<string> keywords, AudienceProfile audience)
    {
        var allTags = g.Mechanics.Concat(g.Themes).Select(t => t.ToLowerInvariant()).ToHashSet();
        var tagOverlap = keywords.Count > 0
            ? (float)keywords.Intersect(allTags).Count() / Math.Max(keywords.Count, 1)
            : 0f;

        var durationScore = g.DurationMinutes.HasValue
            ? Math.Max(0f, 1f - Math.Abs(g.DurationMinutes.Value - audience.DurationMinutes) / 60f)
            : 0.5f;

        // Formule : 60 % chevauchement tags, 30 % mobilité/format, 10 % durée.
        var mobilityPenalty = audience.Mobility == MobilityLevel.Wheelchair
            && g.Format is "outdoor" or "larp" or "geocaching" ? 0.3f : 0f;

        return Math.Max(0f, tagOverlap * 0.6f + durationScore * 0.1f + 0.3f - mobilityPenalty);
    }

    private static HashSet<string> ExtractKeywords(string queryText)
    {
        // Tokenise le texte de requête en mots non vides de longueur ≥ 3.
        return queryText
            .ToLowerInvariant()
            .Split([' ', ',', '.', ':', ';', '\n', '\r', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .ToHashSet();
    }
}
