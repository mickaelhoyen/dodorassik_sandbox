namespace Dodorassik.Core.Domain.Assistant;

/// <summary>Résultat d'une recherche C2 dans la base de mécaniques.</summary>
public record RagHit(
    int Id,
    string Title,
    string SourceGame,
    string[] Mechanics,
    string[] Themes,
    int AgeMin,
    int? AgeMax,
    int? DurationMinutes,
    string Format,
    /// <summary>Score de similarité [0..1] — plus élevé = plus pertinent.</summary>
    float Score);
