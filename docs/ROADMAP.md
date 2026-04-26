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
      `db/init.sql`, README de génération). Génération à lancer en local
      (`dotnet ef migrations add Initial`) puis commit.
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

- [ ] WebSocket / SignalR pour la liveness des chasses compétitives
- [ ] Leaderboard temps réel
- [ ] Plusieurs équipes par famille (ex: garçons vs filles)
- [ ] Anti-triche basique : cohérence vitesse de déplacement, ordre des étapes

## Phase 4 — Création avancée

- [ ] Éditeur de carte intégré (placer des points, dessiner un parcours)
- [ ] Bibliothèque d'assets/énigmes partagée
- [x] **Validation par le super-admin avant publication publique** :
      workflow `Draft → Submitted → Published / Rejected`, `AdminHuntsController`
      (queue, approve, reject avec raison, takedown), endpoints
      `/api/hunts/{id}/submit-for-review`, `/withdraw`, `/archive`,
      verrou d'édition sur Submitted/Published.
- [ ] Marketplace de parcours (gratuit / payant)
- [ ] Internationalisation (FR/EN au minimum)

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
- [ ] Console super-admin web (séparée du jeu) — peut être un projet Blazor
- [ ] Statistiques d'usage (familles actives, parcours populaires)
- [ ] CI/CD GitHub Actions (build serveur + export Godot Android)
- [ ] Déploiement Docker / Fly.io / Azure App Service
