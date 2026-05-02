namespace Dodorassik.Core.Domain.Assistant;

/// <summary>
/// Entrée de la base de connaissances de mécaniques de jeux (C2 RAG).
/// Les embeddings sont générés hors ligne et stockés dans la colonne
/// vector(1536) — null tant que le modèle d'embedding n'a pas été exécuté.
/// </summary>
public class GameMechanic
{
    public int Id { get; set; }

    /// <summary>Titre descriptif de la fiche mécanique.</summary>
    public string Title { get; set; } = "";

    /// <summary>Jeu source dont cette fiche est tirée.</summary>
    public string SourceGame { get; set; } = "";

    /// <summary>
    /// Mécaniques de jeu normalisées (snake_case).
    /// Ex: ["deduction", "cooperation", "time_pressure"]
    /// </summary>
    public string[] Mechanics { get; set; } = [];

    /// <summary>
    /// Thèmes / univers normalisés (snake_case).
    /// Ex: ["medieval", "mystery", "nature"]
    /// </summary>
    public string[] Themes { get; set; } = [];

    public int AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public int? DurationMinutes { get; set; }
    public int? PlayerCountMin { get; set; }
    public int? PlayerCountMax { get; set; }

    /// <summary>
    /// Format du jeu source.
    /// Valeurs : boardgame | videogame | escape | geocaching | larp | outdoor
    /// </summary>
    public string Format { get; set; } = "boardgame";

    /// <summary>
    /// Vecteur d'embedding (text-embedding-3-small, 1536 dim).
    /// Null tant que l'embedder n'a pas été exécuté sur cette fiche.
    /// Stocké en PostgreSQL vector(1536) via pgvector.
    /// </summary>
    public float[]? Embedding { get; set; }
}
