# Game Design Assistant — Architecture C1 → C3

> Ce document décrit la roadmap technique complète de l'assistant de game
> design en réalité terrain intégré à Dodorassik. L'assistant permet à un
> créateur de soumettre photos, points GPS et profil de public pour obtenir
> un jeu généré ou assisté contextuellement.

---

## Vue d'ensemble des couches

```
Entrées créateur
  Photos · Points GPS · Profil public · Sponsors
          │
          ▼
┌─────────────────────────────────────────────────┐
│  C1 — ContextBuilder                            │
│  Enrichissement multimodal des données brutes   │
│  · Vision (photos) → éléments détectés          │
│  · GPS → POIs OSM, faits Wikidata               │
│  · Profil → objet AudienceProfile typé          │
│  · Sponsors → liste SponsorConstraint           │
└──────────────────────┬──────────────────────────┘
                       │ HuntContext
                       ▼
┌─────────────────────────────────────────────────┐
│  C2 — Knowledge RAG (Phase 6b)                  │
│  Recherche vectorielle dans la base de          │
│  mécaniques de jeux (pgvector)                  │
│  · ~1 000 jeux classifiés                       │
│  · Retourne 5-10 mécaniques similaires          │
│  · Propose templates d'étapes                   │
└──────────────────────┬──────────────────────────┘
                       │ HuntContext + RagHits
                       ▼
┌─────────────────────────────────────────────────┐
│  C3 — DesignGenerator (Phase 6c)                │
│  Claude API (Sonnet 4.x) en 3 passes            │
│  1. Brief créatif synthétisé                    │
│  2. Thème + arc narratif + 4-8 étapes           │
│  3. Détail par étape + intégration sponsors     │
│  → GeneratedHunt complet, prêt à éditer         │
└─────────────────────────────────────────────────┘
```

---

## C1 — ContextBuilder (implémenté en Phase 6a)

### Responsabilité

Transformer les entrées brutes du créateur en un objet `HuntContext` structuré,
exploitable par C2 et C3. C1 ne génère rien : il enrichit.

### Sous-composants

#### 1. LocationEnricher

Appelé avec le point GPS central du parcours.

**OpenStreetMap Overpass API** (libre, sans PII)
- Endpoint : `https://overpass-api.de/api/interpreter`
- Rayon : 500 m par défaut (configurable)
- Catégories : `historic`, `tourism`, `leisure`, `natural`
- Retourne : liste de `NearbyPoi { Name, Type, DistanceMeters }`

**Wikidata SPARQL** (libre, sans PII)
- Endpoint : `https://query.wikidata.org/sparql`
- Requête de proximité géographique (rayon 1 km)
- Retourne : liste de `WikidataFact { Label, Description, WikidataId }`

Timeouts courts (8 s / 6 s). En cas d'échec, résultat vide — l'enrichissement
est best-effort, l'endpoint reste fonctionnel.

#### 2. PhotoAnalyzer

- **C1 (actuel)** : `StubPhotoAnalyzer` — retourne un résultat vide horodaté.
  Les photos sont lues en mémoire et immédiatement jetées. Rien n'est stocké.
- **C3 (futur)** : `ClaudePhotoAnalyzer` — envoie l'image redimensionnée
  (max 1024px, JPEG 80 %) à Claude claude-sonnet-4-6 vision. Extrait scène,
  éléments, style architectural.

#### 3. AudienceProfile (parsing)

Validation du DTO entrant → record typé `AudienceProfile`.
Champs : `AgeMin`, `AgeMax`, `GroupSize`, `Mobility`, `DurationMinutes`,
`Language`.

#### 4. SponsorConstraints (parsing)

Liste optionnelle `SponsorConstraint { Brand, Category, Constraints[] }`.
Aucune donnée de sponsor n'est persistée en dehors du draft de chasse.

### Endpoint C1

```
POST /api/hunts/generate/context
Content-Type: multipart/form-data
Authorization: Bearer <jwt>   (rôles : creator, super_admin)

Champs :
  audienceProfileJson  : JSON sérialisé d'AudienceProfileDto
  centerLatitude       : double
  centerLongitude      : double
  gpsPointsJson        : JSON array de GpsPointDto (optionnel)
  sponsorsJson         : JSON array de SponsorConstraintDto (optionnel)
  photos               : IFormFile[] (0-5 fichiers, max 2 Mo chacun)

Réponse 200 : HuntContextDto {
  location: { placeName, pois[], historicalFacts[] },
  audience: { ageMin, ageMax, groupSize, mobility, durationMinutes, language },
  sponsors: [ { brand, category, constraints[] } ],
  photoAnalyses: [ { fileName, sceneDescription, detectedElements[], architectureStyle } ],
  gpsPoints: [ { latitude, longitude, label } ]
}
```

Rate limit dédié : `generate-context` — 10 req/h par utilisateur (évite les
abus sur les APIs externes).

---

## C2 — Knowledge RAG (Phase 6b, non implémenté)

### Base de données de mécaniques

Table `GameMechanic` + colonne `EmbeddingVector` (pgvector) :

```sql
CREATE TABLE game_mechanics (
  id          SERIAL PRIMARY KEY,
  title       TEXT NOT NULL,
  source_game TEXT NOT NULL,            -- "Pandemic", "7th Guest", ...
  mechanics   TEXT[] NOT NULL,          -- ["cooperation", "time_pressure"]
  themes      TEXT[] NOT NULL,          -- ["nature", "medieval", "mystery"]
  age_min     INT NOT NULL,
  age_max     INT,
  duration_minutes INT,
  player_count_min INT,
  player_count_max INT,
  format      TEXT NOT NULL,            -- "boardgame" | "videogame" | "escape" | "geocaching"
  embedding   vector(1536)              -- pgvector, text-embedding-3-small
);
```

### Processus de recherche

1. Composer un texte de requête depuis `HuntContext`
   (lieu + public + durée + mobilité + thèmes détectés)
2. Embedding de la requête (OpenAI text-embedding-3-small ou équivalent)
3. `SELECT ... ORDER BY embedding <=> $query_vec LIMIT 10`
4. Retourner les `RagHit[]` au service appelant

### Interface

```csharp
public interface IGameKnowledgeRepository
{
    Task<IReadOnlyList<RagHit>> FindSimilarAsync(
        string queryText, int limit, CancellationToken ct);
}

public record RagHit(
    string Title, string SourceGame, string[] Mechanics,
    string[] Themes, int AgeMin, int? AgeMax, string Format,
    float Score);
```

### Peuplement initial

- BoardGameGeek API (XML 2.0) : top 500 jeux, extraction mécaniques/thèmes
- Curation manuelle (~50 jeux escape/geocaching/LARP)
- Script `tools/seed-game-mechanics.py` (hors scope Phase 6a)

---

## C3 — DesignGenerator (Phase 6c, non implémenté)

### Modèle recommandé

Claude Sonnet 4.6 (`claude-sonnet-4-6`) pour l'équilibre coût/créativité.
Claude Opus 4.7 pour les cas contraints (sponsors multiples + lieu complexe).

### Prompt chain en 3 passes

**Passe 1 — Brief créatif**
- Entrée : `HuntContext` + `RagHit[]` sérialisés en JSON
- Sortie : brief structuré (thème pressentis, contraintes, ton)
- Cache : `cache_control: ephemeral` sur le contexte système

**Passe 2 — Concept global**
- Entrée : brief P1 + contexte réduit
- Sortie : `{ title, narrative, theme, steps_outline[4-8] }`

**Passe 3 — Détail des étapes**
- Entrée : concept P2 + contraintes sponsors
- Sortie : `GeneratedHunt` complet avec chaque étape rédigée

### Interface

```csharp
public interface IDesignGeneratorService
{
    IAsyncEnumerable<string> GenerateStreamAsync(
        HuntContext context, IReadOnlyList<RagHit> ragHits,
        CancellationToken ct);
}

public record GeneratedHunt(
    string Title,
    string Narrative,
    string Theme,
    TimeSpan EstimatedDuration,
    IReadOnlyList<GeneratedStep> Steps,
    IReadOnlyList<SponsorIntegration> SponsorTouchpoints);
```

### Endpoint C3

```
POST /api/hunts/generate/full
Authorization: Bearer <jwt>   (creator, super_admin)

Body : { context: HuntContextDto, ragHits: RagHitDto[] }

Réponse : Server-Sent Events (text/event-stream)
  → stream des tokens Claude
  → event "done" avec GeneratedHunt JSON final
```

Rate limit : 5 req/h par créateur (coût API Claude).

---

## Privacy & Security

- Les **photos** ne sont jamais persistées : lues en mémoire, analysées,
  jetées. Le `StubPhotoAnalyzer` ne lit même pas le contenu.
- Le **GPS** envoyé est celui des *lieux du parcours*, jamais une trace
  joueur. Conforme CLAUDE.md §3 invariant 2.
- Les appels OSM/Wikidata n'envoient que des coordonnées de lieu — aucune
  donnée utilisateur ne quitte le serveur vers ces APIs.
- L'endpoint est `[Authorize(Roles="creator,super_admin")]` — jamais accessible
  en mode joueur anonyme.
- Les résultats bruts des APIs externes ne sont pas loggés (peuvent contenir
  des données de lieux sensibles).
- En C3, les prompts envoyés à Claude ne contiendront **jamais** d'email,
  d'identifiant utilisateur, ni de contenu des photos brutes.

---

## Packages requis par phase

| Phase | Package | Usage |
|-------|---------|-------|
| 6a (C1) | aucun ajout | OSM/Wikidata via `HttpClient` natif |
| 6b (C2) | `Pgvector.EntityFrameworkCore` | similarité vectorielle |
| 6b (C2) | `OpenAI` (embeddings) | vectorisation des requêtes |
| 6c (C3) | `Anthropic.SDK` | Claude API vision + génération |
| 6c (C3) | `SixLabors.ImageSharp` | redimensionnement avant envoi vision |
