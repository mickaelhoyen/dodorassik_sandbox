# Roadmap

Liste vivante. À découper en issues GitHub au fil de l'eau.

## Phase 1 — Fondations (✅ ce commit)

- [x] Squelette Godot 4 + 5 autoloads
- [x] Trois interfaces (joueur, créateur, super-admin)
- [x] Solution .NET 8 (Api / Core / Infrastructure) avec EF Core PostgreSQL
- [x] Modèle domaine de base (User, Family, Hunt, Step, Clue, Submission, Score)
- [x] Auth JWT + hashing PBKDF2
- [x] File de soumissions hors ligne

## Phase 2 — Boucle de jeu jouable

- [x] **Cadre security/privacy by design** : `CLAUDE.md`, `docs/SECURITY.md`,
      `docs/PRIVACY.md`, journal `docs/CLAUDE-LOG.md`.
- [x] Audit + correctifs sur l'existant : CORS allowlist par environnement,
      rate limiting natif .NET 8, validation DTO complète, messages d'erreur
      anti-énumération, `MapInboundClaims = false`, claims rôle en snake_case.
- [x] Endpoints RGPD : `GET/PATCH/DELETE /api/users/me`, export portabilité.
- [x] Tests d'intégration `WebApplicationFactory` (Auth, Hunts, Users,
      **Families, Public, Hunts.Update PUT, Hunts.Create avec clues
      dupliquées, signup creator, modération complète** — InMemory ;
      voir `server/tests/Dodorassik.Api.Tests/README.md` pour la voie
      Testcontainers Postgres).
- [x] Migrations EF Core : scaffolding (`DesignTimeDbContextFactory`,
      README de génération). Génération à lancer en local
      (`dotnet ef migrations add InitialCreate`) puis commit. SQL prod
      généré à la volée par `dotnet ef migrations script --idempotent`.
- [x] Plugin Android scaffold (`godot/android/plugin/`) : permissions,
      modules GPS / Camera / Bluetooth, intégration `DeviceServices` runtime.
- [x] Sélection / création de famille à la connexion (`FamiliesController`
      + écran `family_select` côté Godot).
- [x] Migration projet vers Godot 4.6.
- [x] Création réelle d'une chasse depuis l'éditeur Godot avec steps + clues :
      éditeur réécrit avec formulaire par step (type, titre, description,
      params type-spécifiques), gestion des clues physiques (code, titre,
      reveal, points), PUT pour mise à jour des chasses existantes, upsert
      server-side des steps/clues avec conservation des ids.
- [x] Build Android signé (debug) documenté avec presets d'export Godot 4.6,
      génération du keystore debug, install ADB, filtrage logcat.
- [x] Implémentation native du plugin Android : `LocationModule`
      (FusedLocationProviderClient, one-shot, timeout 5 s),
      `CameraModule` (ACTION_IMAGE_CAPTURE + FileProvider, signal
      `photo_captured`), `BluetoothModule` (BluetoothLeScanner avec
      ScanFilter par MAC, signal `bluetooth_device_found`). Build Gradle
      complet (`build.gradle`, `settings.gradle`, `consumer-rules.pro`,
      ressource `dodorassik_file_paths.xml`). Reste à valider sur
      appareil physique.

## Phase 3 — Multi-joueur & compétition

- [x] WebSocket / SignalR pour la liveness des chasses compétitives :
      `CompetitiveHuntHub` (SignalR, `/hubs/competitive`), client GDScript
      minimal avec negotiate + handshake + `JoinHunt` + push `LeaderboardUpdated`.
- [x] Leaderboard temps réel : `GET /api/hunts/{id}/leaderboard` (REST),
      push SignalR après chaque étape acceptée, `LeaderboardScreen` Godot
      avec polling 10 s + réception SignalR en compétitif.
- [x] Plusieurs équipes par famille (ex: garçons vs filles) : entité `Team` +
      `TeamMember`, `TeamsController` (list / create / join / leave),
      `TeamSelectScreen` Godot (affiché automatiquement avant le runner en mode
      compétitif), `active_team` dans `AppState`.
- [x] Anti-triche basique : `IAntiCheatService` / `AntiCheatService` — vérification
      de l'ordre des étapes (`BlocksNext`) et cohérence de la vitesse GPS
      (max 40 km/h). Appliqué côté serveur dans `POST .../steps/{id}/submit`
      uniquement pour les chasses en mode `competitive`.

## Phase 4 — Création avancée

- [x] **Éditeur de carte intégré** : WebView Android (Option B) avec Leaflet.js —
      `MapModule.java` overlay plein-écran, `map_editor.html` bundlé dans les
      assets du plugin, protocole Base64 GDScript↔JS, signaux `map_confirmed` /
      `map_cancelled`. Bouton « 🗺️ » dans l'éditeur de parcours, fallback
      automatique sur les champs lat/lon si Android non disponible.
- [x] **Bibliothèque d'étapes/énigmes partagée** : entité `StepTemplate` (titre,
      description, type, params jsonb, tags, visibilité publique), `StepTemplatesController`
      (search `?mine=&type=&tag=`, get, create, update, delete),
      `StepLibraryScreen` Godot avec filtres mine/type, bouton « 📚 Bibliothèque »
      dans l'éditeur injectant le modèle comme nouvelle étape.
- [x] **Validation par le super-admin avant publication publique** :
      workflow `Draft → Submitted → Published / Rejected`, `AdminHuntsController`
      (queue, approve, reject avec raison, takedown), endpoints
      `/api/hunts/{id}/submit-for-review`, `/withdraw`, `/archive`,
      verrou d'édition sur Submitted/Published. `AdminModerationScreen` Godot
      (file de modération, carte par parcours, approbation / rejet en ligne).
- [ ] Marketplace de parcours (gratuit / payant)
- [x] **Internationalisation FR/EN** : `translations/strings.csv` (~188 clés,
      colonnes fr/en), autoload `AppLocale` (lecture/écriture `user://config.json`,
      `TranslationServer.set_locale()`), sélecteur de langue sur l'écran d'accueil,
      tous les écrans Godot migrés vers `tr()`, `project.godot` configuré avec
      fallback fr.

## Phase 5 — Plateforme

- [x] **Inscription créateur depuis Godot** : écran `signup_screen.gd` avec
      sélection de rôle Player/Creator, auto-login post-inscription.
- [x] **Sauvegarde session joueur** : bouton "Créer un compte" accessible
      depuis `player_home` sans authentification préalable.
- [x] **Interface web publique** (Razor Pages intégrées à l'API) :
      - `/` → page publique listant événements en cours et parcours permanents.
      - `/Signup` → formulaire d'inscription créateur/joueur côté web.
      - `GET /api/public/hunts` → endpoint JSON pour le catalogue public.
- [x] **HuntCategory** : distinction `Permanent` / `Event` sur les chasses,
      avec `EventStartUtc` / `EventEndUtc` pour les événements temporaires.
- [x] **Console super-admin web** : SPA vanilla JS/HTML servie à `/admin/index.html`
      par le même serveur ASP.NET. Login JWT → onglets Statistiques / Modération /
      Tous les parcours. Approve, reject (raison obligatoire), takedown inline.
      Aucun framework tiers, aucun cookie supplémentaire — utilise le JWT Bearer
      existant stocké en `sessionStorage`.
- [x] **Statistiques d'usage** : `StatsController` (`GET /api/admin/stats`,
      `[Authorize(Roles="super_admin")]`) — familles totales / actives 30j,
      utilisateurs, parcours par statut, soumissions acceptées total / 7j,
      modèles de bibliothèque, top-10 parcours par soumissions.
- [x] **CI/CD GitHub Actions** :
      - `ci-server.yml` : build + test .NET 8 sur push/PR paths `server/**` ;
        audit `dotnet list package --vulnerable` en fin de job.
      - `ci-godot.yml` : import headless Godot 4.6 (détection d'erreurs GDScript)
        sur push/PR paths `godot/**` ; export APK debug uniquement sur `main`.
- [x] **Déploiement Docker** : `server/Dockerfile` multi-stage (sdk:8.0 → aspnet:8.0),
      `docker-compose.yml` enrichi avec service `api` + healthcheck postgres,
      secrets passés via variables d'environnement (`JWT_SECRET`, `POSTGRES_PASSWORD`,
      `CORS_ORIGIN`), `server/.dockerignore`.

## Phase 6 — Assistant de game design en réalité terrain

> Architecture complète documentée dans `docs/GAME-DESIGN-ASSISTANT.md`.

### Phase 6a — C1 ContextBuilder (🚧 en cours)

- [x] **Documentation C1→C3** : `docs/GAME-DESIGN-ASSISTANT.md` — couches,
      interfaces, endpoints, privacy/security review.
- [x] **Domaine Assistant** : records `AudienceProfile`, `GpsPoint`,
      `SponsorConstraint`, `LocationContext`, `PhotoAnalysisResult`, `HuntContext`
      dans `Dodorassik.Core/Domain/Assistant/`.
- [x] **Abstractions** : `IContextBuilderService`, `ILocationEnricher`,
      `IPhotoAnalyzer` dans `Dodorassik.Core/Abstractions/`.
- [x] **LocationEnricher** (Infrastructure) : appels OpenStreetMap Overpass API
      + Wikidata SPARQL, timeouts courts, résultat vide si échec réseau.
- [x] **StubPhotoAnalyzer** (Infrastructure) : placeholder jusqu'à C3 ; photos
      lues en mémoire et immédiatement jetées, rien persisté.
- [x] **ContextBuilderService** (Api) : orchestre LocationEnricher + PhotoAnalyzer
      en parallèle.
- [x] **HuntGenerationController** : `POST /api/hunts/generate/context`
      (`[Authorize(Roles="creator,super_admin")]`), multipart/form-data,
      rate limit 10 req/h.

### Phase 6b — C2 Knowledge RAG

- [ ] Peuplement base de mécaniques (~1 000 jeux : BoardGameGeek + curation manuelle)
- [ ] `pgvector` sur PostgreSQL + migration EF Core
- [ ] `IGameKnowledgeRepository` + implémentation EF
- [ ] Embedding des requêtes (text-embedding-3-small ou équivalent)
- [ ] Enrichissement de `HuntContextDto` avec `RagHit[]`

### Phase 6c — C3 DesignGenerator (Claude API)

- [ ] `Anthropic.SDK` + `SixLabors.ImageSharp`
- [ ] `ClaudePhotoAnalyzer` remplaçant `StubPhotoAnalyzer` (vision multimodale)
- [ ] `IDesignGeneratorService` + implémentation Claude — prompt chain 3 passes
- [ ] `POST /api/hunts/generate/full` avec streaming SSE
- [ ] Rate limit 5 req/h par créateur
- [ ] Intégration Godot : écran `hunt_generator` déclenché depuis `creator_home`
