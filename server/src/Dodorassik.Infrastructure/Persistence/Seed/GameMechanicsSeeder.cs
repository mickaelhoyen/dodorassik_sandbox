using System.Text.Json;
using System.Text.Json.Serialization;
using Dodorassik.Core.Domain.Assistant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dodorassik.Infrastructure.Persistence.Seed;

/// <summary>
/// Peuple la table GameMechanics depuis game_mechanics.json embarqué dans
/// l'assembly. Idempotent : n'insère rien si la table contient déjà des entrées.
///
/// À appeler en démarrage ou via l'endpoint admin
/// POST /api/admin/seed/game-mechanics.
/// </summary>
public class GameMechanicsSeeder
{
    private readonly AppDbContext _db;
    private readonly ILogger<GameMechanicsSeeder> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GameMechanicsSeeder(AppDbContext db, ILogger<GameMechanicsSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> SeedAsync(CancellationToken ct = default)
    {
        if (await _db.GameMechanics.AnyAsync(ct))
        {
            _logger.LogInformation("GameMechanics table already populated — skipping seed.");
            return 0;
        }

        var json = await ReadEmbeddedJsonAsync(ct);
        var seeds = JsonSerializer.Deserialize<List<GameMechanicSeedDto>>(json, JsonOpts);

        if (seeds is null or { Count: 0 })
        {
            _logger.LogWarning("game_mechanics.json is empty or could not be parsed.");
            return 0;
        }

        var entities = seeds.Select(s => new GameMechanic
        {
            Title = s.Title,
            SourceGame = s.SourceGame,
            Mechanics = s.Mechanics,
            Themes = s.Themes,
            AgeMin = s.AgeMin,
            AgeMax = s.AgeMax,
            DurationMinutes = s.DurationMinutes,
            PlayerCountMin = s.PlayerCountMin,
            PlayerCountMax = s.PlayerCountMax,
            Format = s.Format,
            Embedding = null, // généré séparément par l'embedder
        }).ToList();

        _db.GameMechanics.AddRange(entities);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded {Count} game mechanics.", entities.Count);
        return entities.Count;
    }

    private static async Task<string> ReadEmbeddedJsonAsync(CancellationToken ct)
    {
        var assembly = typeof(GameMechanicsSeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("game_mechanics.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException("Embedded resource game_mechanics.json not found.");

        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    private record GameMechanicSeedDto(
        string Title,
        string SourceGame,
        string[] Mechanics,
        string[] Themes,
        int AgeMin,
        int? AgeMax,
        int? DurationMinutes,
        int? PlayerCountMin,
        int? PlayerCountMax,
        string Format);
}
