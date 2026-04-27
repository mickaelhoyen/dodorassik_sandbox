# Journal des interventions Claude

> Historique de toutes les requêtes utilisateur substantielles et des
> modifications apportées par Claude. Une entrée par requête, ordre
> chronologique inverse (plus récent en haut).
>
> Format de chaque entrée :
> 1. Métadonnées (date, branche, commit)
> 2. Requête utilisateur (verbatim ou résumé fidèle si trop longue)
> 3. Analyse Claude (interprétation, options envisagées, choix)
> 4. Modifications (fichiers + nature)
> 5. Security & Privacy review (impact sur les invariants de `CLAUDE.md`)

---

## 2026-04-27 — Correction CI (rate limiting tue les tests d'intégration)

**Branche** : `claude/roadmap-next-phase-qbyvm`
**Commit** : (en cours)

### Requête utilisateur

> Le CI est toujours en failed — les tests reçoivent des 429 au lieu des 400/200 attendus.

### Analyse

Après correction de CS0832 (commit `e31719c`), le build passe et les tests tournent
enfin. Nouveau symptôme : beaucoup de tests obtiennent 429 TooManyRequests là où
ils attendent 400 ou 200.

Cause : le rate limiter `auth-register` est configuré à **3 requêtes par heure**,
sans partitionnement par IP — c'est un compteur global partagé par toutes les
requêtes de l'endpoint. Dans `AuthApiTests` (qui utilise un `IClassFixture` = une
seule usine), les tests `Register_creates_account`, `Register_rejects_duplicate`
et `Register_rejects_invalid_input` (4 cas Theory) consomment 7+ registrations
sur la même usine → limite dépassée dès le 4e appel → 429.

Idem pour `HuntsApiTests`, `HuntsModerationApiTests`, etc. : chaque classe de
test partage son usine et effectue plusieurs registrations.

La suggestion de l'audit (changer `[property: Required]` → `[Required]`) est un
faux positif : la syntaxe `[property: X]` est correcte pour les records en
ASP.NET Core 8 (l'attribut est sur la propriété générée, que le model validator
inspecte effectivement).

**Solution** : utiliser des limites très élevées (10 000 / fenêtre) en
environnement `Development` — l'usine de tests (`TestingWebAppFactory`) fixe
l'environnement à Development. En production les valeurs strictes sont conservées.
Le global limiter par IP n'est également activé qu'en dehors de Development
(inutile en local ou en test, et aucun RemoteIpAddress fiable dans un contexte
in-process).

### Modifications

| Fichier | Nature |
|---|---|
| `server/src/Dodorassik.Api/Program.cs` | Limits conditionnelles Development vs Production pour rate limiter |

### Security & Privacy review

Les limites de prod (3/h register, 5/min login, 30/min submit) sont **inchangées**.
En Development seulement les limites sont relâchées. `IsDevelopment()` est strict
(variable d'environnement `ASPNETCORE_ENVIRONMENT = Development`) ; un déploiement
de production n'active jamais ce chemin.

---

## 2026-04-27 — Correction CI (is-pattern dans expression tree)

**Branche** : `claude/roadmap-next-phase-qbyvm`
**Commit** : (en cours)

### Requête utilisateur

> Le CI fail toujours à chaque fois, les workflow échouent systématiquement.

### Analyse

Récupération des logs de CI via l'URL du job GitHub Actions (run #24963923261).
Erreur réelle : `CS0832 An expression tree may not contain an 'is' pattern-matching operator`
dans `PublicApiTests.cs` ligne 50.

Le lambda `h => h.Name is "Brouillon" or "À modérer" or "Rejetée" or "Archivée"` est
passé à `FluentAssertions.NotContain()` qui attend un `Expression<Func<T, bool>>`.
C# ne permet pas les patterns `is ... or` dans un arbre d'expression (limitation du
compilateur, RFC expression-trees). C'est une erreur de compilation, pas de runtime —
d'où l'échec silencieux en ~30 secondes (restore + build only, sans résultats de tests).

Solution : remplacer le pattern `is X or Y` par des comparaisons `== X || == Y` standard,
compatibles avec les expression trees.

Note : commit `7d7492d` (verbose output) n'avait pas déclenché de run CI car il ne
modifiait pas `server/**` (path filter). La correction `671b836` (ExecuteUpdateAsync)
avait bien déclenché un run, mais l'erreur de compilation était antérieure.

### Modifications

| Fichier | Nature |
|---|---|
| `server/tests/Dodorassik.Api.Tests/PublicApiTests.cs` | Correctif — is-pattern → || equality |

### Security & Privacy review

Changement purement dans les tests. Aucun impact sur le code de production,
la sécurité, la vie privée ou les invariants CLAUDE.md.

---

## 2026-04-26 — Correction CI (ExecuteUpdateAsync InMemory + Godot download)

**Branche** : `claude/roadmap-next-phase-qbyvm`
**Commit** : (en cours)

### Requête utilisateur

> Le CI fail à chaque fois, vérifies si quelque chose pose problème si ce n'est
> pas le cas désactives temporairement le CI.

### Analyse

Deux problèmes distincts confirmés par inspection des check runs sur les PRs #11 et #12 :

1. **`ci-godot.yml` — 404 au téléchargement** : la version `Godot_v4.6-stable_linux.x86_64`
   n'est pas encore publiée sur GitHub Releases, ce qui fait échouer le `curl` en 14 secondes.
   Solution : changer le trigger de `push/pull_request` à `workflow_dispatch` seulement,
   avec un commentaire expliquant la cause. À réactiver dès la sortie de Godot 4.6 stable.

2. **`ci-server.yml` — test `Delete_requires_explicit_confirmation` échoue** :
   `UsersController.Delete` utilise `ExecuteUpdateAsync()` (EF Core 7+). Cette API
   est réservée aux providers relationnels ; le provider InMemory utilisé en tests
   ne la supporte pas et lève une exception → le contrôleur renvoie 500 au lieu de 204.
   Solution : remplacer par le pattern classique load+set+SaveChanges, compatible
   avec InMemory et PostgreSQL.

### Modifications

| Fichier | Nature |
|---|---|
| `server/src/Dodorassik.Api/Controllers/UsersController.cs` | Correctif — ExecuteUpdateAsync → load+update loop |
| `.github/workflows/ci-godot.yml` | Désactivé temporairement (trigger → workflow_dispatch) |

### Security & Privacy review

- Le correctif GDPR (`Delete`) est fonctionnellement équivalent : les soumissions et
  chasses de l'utilisateur sont bien anonymisées avant suppression. Aucun changement
  de logique de sécurité ou de vie privée.
- Le trigger `workflow_dispatch` ne supprime pas le workflow ; il empêche juste les
  runs automatiques qui auraient toujours échoué de toute façon.

---

## 2026-04-26 — Éditeur de carte (Option B WebView) + corrections code review

**Branche** : `claude/roadmap-next-phase-qbyvm`
**Commit** : (en cours)

### Requête utilisateur

> Pour l'éditeur de carte implémentes l'option B. Analyses de nouveau l'ensemble
> du code pour vérifier s'il n'y aurait pas d'autres améliorations à apporter.

### Analyse

**Éditeur de carte — Option B (WebView Leaflet.js)**
Choix confirmé : WebView Android overlay, HTML bundlé dans les assets du plugin,
Leaflet.js via CDN (créateurs connectés lors de l'édition). Alternative CDN
rejetée pour le runner (joueurs hors ligne) — pas concerné ici.
Architecture : `MapModule.java` est un module interne de `DodorassikDevice`
(même pattern que LocationModule/CameraModule/BluetoothModule). Communication
GDScript→JS via `evaluateJavascript(initBase64(b64))` avec Base64 pour éviter
l'échappement JSON. Retour JS→GDScript via `JavascriptInterface` → signaux
`map_confirmed` / `map_cancelled` propagés par DeviceServices.
Pas de nouvel écran/route — la carte s'ouvre en overlay sans détruire hunt_editor,
ce qui évite le problème de Callable sur objet libéré.

**Corrections code review**
- Faux positif AntiCheatService (Include non nécessaire — EF Core traduit les
  navigation properties en JOIN dans les WHERE automatiquement).
- Faux positif leaderboard auth (le code protège déjà les chasses non publiées).
- Vrais bugs corrigés : traductions PLAYER_PLAY_BTN/PLAYER_DOWNLOAD_BTN manquantes,
  team_select_screen continue vers hunt_runner si le refresh post-join échoue
  (fallback sur dict minimal au lieu de crash silencieux).

### Modifications

| Fichier | Nature |
|---|---|
| `godot/android/plugin/src/main/java/com/dodorassik/device/MapModule.java` | Créé — WebView overlay + JavascriptInterface |
| `godot/android/plugin/src/main/java/com/dodorassik/device/DodorassikDevice.java` | Modifié — MapModule + signaux map_confirmed/map_cancelled |
| `godot/android/plugin/src/main/assets/map_editor.html` | Créé — Leaflet.js map, placement/drag, rayon, confirm/cancel |
| `godot/scripts/autoload/device_services.gd` | Modifié — signaux + méthodes map wrapper |
| `godot/scripts/ui/hunt_editor.gd` | Modifié — bouton 🗺️, _open_map, _on_map_confirmed, _on_map_cancelled |
| `godot/translations/strings.csv` | Modifié — PLAYER_PLAY_BTN, PLAYER_DOWNLOAD_BTN, MAP_EDITOR_* |
| `godot/scripts/ui/team_select_screen.gd` | Modifié — fallback active_team si refresh échoue |
| `docs/ROADMAP.md` | Éditeur de carte coché |

### Security & Privacy review

- Le WebView charge la carte depuis `file:///android_asset/map_editor.html`
  (contenu maîtrisé, pas d'injection possible via URL externe).
- `MIXED_CONTENT_ALWAYS_ALLOW` activé uniquement pour permettre file://→HTTPS
  pour les tuiles OSM ; limité à ce WebView isolé, pas à l'app entière.
- Les coordonnées GPS retournées par la carte ne sont pas loguées (seul le
  payload JSON opaque transite via le signal, comme les autres modules).
- Aucune PII nouvelle. Le JavascriptInterface expose uniquement onConfirm et
  onCancel — surface d'attaque minimale.

---

## 2026-04-26 — Phase 5 : CI/CD, Docker, statistiques d'usage, console super-admin web

**Branche** : `claude/roadmap-next-phase-qbyvm`
**Commit** : (en cours)

### Requête utilisateur

> Continues sur la phase suivante.

### Analyse

Phase 5 — Plateforme — avait 4 items restants :

1. **Console super-admin web** : Blazor était une option citée dans le ROADMAP
   mais ajouter un projet Blazor Server séparé représente une dépendance lourde.
   Choix retenu : SPA vanilla JS/HTML servie par le même serveur ASP.NET Core
   comme fichier statique (`wwwroot/admin/index.html`). Avantages : zéro
   dépendance, même domaine (pas de CORS additionnel), même JWT Bearer déjà
   implémenté. Inconvénient : pas de Server-Side Rendering, mais les données
   admin ne sont pas indexées donc SEO irrelevant.

2. **Statistiques d'usage** : nouvel endpoint `GET /api/admin/stats` protégé
   `super_admin`. Pas de table d'agrégats dédiée — calcul en temps réel sur
   les données existantes (acceptable pour un usage admin, volume attendu faible).

3. **CI/CD GitHub Actions** : deux workflows indépendants déclenchés par path
   (`server/**` et `godot/**`). Le workflow Godot ne réalise l'export APK que
   sur `main` (import+validation sur toutes les branches).

4. **Docker** : multi-stage Dockerfile standard .NET 8. Le `docker-compose.yml`
   existant avait uniquement postgres ; il est enrichi avec le service `api`.
   Les secrets sont portés par variables d'environnement pour respecter
   l'invariant #1 de CLAUDE.md (pas de secret en clair dans le repo).

### Modifications

| Fichier | Nature |
|---|---|
| `.github/workflows/ci-server.yml` | Créé — CI .NET build + test + audit |
| `.github/workflows/ci-godot.yml` | Créé — import headless + export APK (main only) |
| `server/Dockerfile` | Créé — multi-stage sdk:8.0 → aspnet:8.0 |
| `server/.dockerignore` | Créé — exclusions bin/obj/tests |
| `server/docker-compose.yml` | Modifié — ajout service api + healthcheck postgres |
| `server/src/Dodorassik.Api/Dtos/StatsDtos.cs` | Créé — UsageStatsDto, PopularHuntDto |
| `server/src/Dodorassik.Api/Controllers/StatsController.cs` | Créé — GET /api/admin/stats |
| `server/src/Dodorassik.Api/wwwroot/admin/index.html` | Créé — console SPA (login, stats, modération) |
| `docs/ROADMAP.md` | Phase 5 items cochés |

### Security & Privacy review

- `StatsController` : `[Authorize(Roles = "super_admin")]` — aucune donnée PII
  exposée (familles = entités anonymes, comptes = counts, pas de noms/emails).
- `wwwroot/admin/index.html` : JWT stocké en `sessionStorage` (fermé à la
  fermeture de l'onglet), jamais en `localStorage`. XSS mitigé par la fonction
  `esc()` sur toutes les valeurs affichées.
- Docker : `JWT_SECRET` et `POSTGRES_PASSWORD` sont des variables d'environnement,
  non commités. La valeur par défaut dans `docker-compose.yml` est `REPLACE_ME…`
  pour les secrets JWT, ce qui évite un démarrage silencieux avec un secret faible.
- CI : aucun secret dans les workflows ; l'audit `dotnet list package --vulnerable`
  est systématique.
- Aucune PII nouvelle. Logs inchangés.

---

## 2026-04-26 — Phase 4 : internationalisation FR/EN, bibliothèque d'étapes, modération super-admin

**Branche** : `claude/roadmap-next-phase-qbyvm`
**Commit** : (en cours)

### Requête utilisateur

> Continues sur la phase suivante.

### Analyse

Phase 4 — Création avancée — comporte cinq items :

1. **Éditeur de carte** — non implémenté (dépend d'un moteur carto tiers).
2. **Bibliothèque d'étapes** — implémenté côté serveur + Godot.
3. **Validation super-admin** — déjà implémentée en phase précédente ; écran
   Godot `AdminModerationScreen` ajouté.
4. **Marketplace** — non implémenté (dépend d'un module paiement).
5. **Internationalisation** — implémentée : CSV 188 clés, `AppLocale`, tous
   les écrans migrés vers `tr()`.

Choix techniques :
- CSV Godot natif (TranslationServer) plutôt que JSON custom : aucune
  dépendance, rechargeable à chaud, compatible Godot export.
- `AppLocale` partage `user://config.json` avec `ApiClient` pour minimiser
  les fichiers de conf.
- La bibliothèque d'étapes est limitée aux créateurs (`Authorize(Roles=
  "creator,super_admin")`), jamais exposée aux joueurs.

### Modifications

| Fichier | Nature |
|---|---|
| `godot/translations/strings.csv` | Créé — ~188 clés FR/EN |
| `godot/scripts/autoload/app_locale.gd` | Créé — gestion locale + persistance |
| `godot/project.godot` | Modifié — autoload AppLocale + section i18n |
| `godot/scripts/ui/role_selection_screen.gd` | Migré vers tr() + sélecteur langue |
| `godot/scripts/ui/login_screen.gd` | Migré vers tr() |
| `godot/scripts/ui/signup_screen.gd` | Migré vers tr() |
| `godot/scripts/ui/family_select_screen.gd` | Migré vers tr() |
| `godot/scripts/ui/player_home.gd` | Migré vers tr() |
| `godot/scripts/ui/creator_home.gd` | Migré vers tr() |
| `godot/scripts/ui/super_admin_home.gd` | Migré vers tr(), route admin_moderation |
| `godot/scripts/ui/hunt_runner.gd` | Migré vers tr() |
| `godot/scripts/ui/team_select_screen.gd` | Migré vers tr() |
| `godot/scripts/ui/leaderboard_screen.gd` | Migré vers tr() |
| `godot/scripts/ui/hunt_editor.gd` | Migré vers tr(), bouton Bibliothèque, _inject_template |
| `godot/scripts/ui/step_library_screen.gd` | Créé — filtres mine/type, usage de modèle |
| `godot/scripts/ui/admin_moderation_screen.gd` | Créé — file de modération, approve/reject |
| `godot/scripts/autoload/api_client.gd` | Ajout search_step_templates, admin_* |
| `godot/scripts/autoload/router.gd` | Ajout routes step_library, admin_moderation |
| `server/src/Dodorassik.Core/Domain/StepTemplate.cs` | Créé — entité template |
| `server/src/Dodorassik.Api/Dtos/StepTemplateDtos.cs` | Créé — DTO + mapping |
| `server/src/Dodorassik.Api/Controllers/StepTemplatesController.cs` | Créé — CRUD REST |
| `server/src/Dodorassik.Infrastructure/Persistence/AppDbContext.cs` | Modifié — DbSet StepTemplate + config EF |
| `docs/ROADMAP.md` | Phase 4 items cochés |

### Security & Privacy review

- `StepTemplatesController` : `[Authorize(Roles = "creator,super_admin")]` global,
  ownership check sur update/delete, pas de bypass possible.
- Aucune PII nouvelle : `StepTemplate` ne stocke que du contenu de jeu
  (titre, description, type, params, tags) + `CreatedById` (FK existante).
- `AdminModerationScreen` s'appuie sur `AdminHuntsController` déjà sécurisé
  (`[Authorize(Roles = "super_admin")]`).
- Aucun secret committé. Logs inchangés.

---

## 2026-04-26 — Phase 3 : multi-joueur compétitif, leaderboard temps réel, anti-triche

**Branche** : `claude/roadmap-next-phase-qbyvm`
**Commit** : (en cours)

### Requête utilisateur

> Lis le ROADMAP et poursuit l'implémentation de la prochaine phase, si il y
> a d'autres urgences programmées, indiques le moi afin que je puisse te dire
> quoi faire.

### Analyse

La phase 3 — Multi-joueur & compétition — était la prochaine entièrement non
commencée. Quatre items :

1. **WebSocket / SignalR** — backbone temps-réel pour les chasses compétitives.
2. **Leaderboard temps réel** — classement persisté + poussé via SignalR.
3. **Plusieurs équipes par famille** — modèle `Team` + `TeamMember`, workflow
   Godot d'entrée.
4. **Anti-triche basique** — ordre des étapes (`BlocksNext`) + vitesse GPS.

**Urgences signalées dans les autres phases :**
- Phase 5 — CI/CD GitHub Actions + Docker : indépendant, peut être fait à tout
  moment ; débloquerait les déploiements en production.
- Phase 5 — Console super-admin web (Blazor) : workflow côté serveur complet
  mais UI Godot encore en stubs.

Toute la Phase 3 a été implémentée en une session.

### Modifications

#### Backend (C# / .NET 8)

- **`server/src/Dodorassik.Core/Domain/Team.cs`** *(nouveau)* — entités
  `Team` (Id, Name, Color, HuntId, FamilyId) et `TeamMember` (join composite).
- **`server/src/Dodorassik.Core/Domain/Submission.cs`** *(modifié)* — ajout
  `TeamId?` + navigation `Team?`.
- **`server/src/Dodorassik.Core/Abstractions/IAntiCheatService.cs`** *(nouveau)* —
  contrat `IsStepOrderValidAsync` + `IsGpsSpeedPlausibleAsync`.
- **`server/src/Dodorassik.Infrastructure/Persistence/AppDbContext.cs`** *(modifié)* —
  `DbSet<Team>`, `DbSet<TeamMember>`, config EF (index, FK, composite key), FK
  `TeamId` sur `StepSubmission`.
- **`server/src/Dodorassik.Api/Dtos/TeamDtos.cs`** *(nouveau)* — `TeamDto`,
  `TeamMemberDto`, `CreateTeamRequest`, `LeaderboardEntryDto`, `LeaderboardDto`.
- **`server/src/Dodorassik.Api/Dtos/HuntDtos.cs`** *(modifié)* — `SubmitStepRequest`
  accepte maintenant `Guid? TeamId`.
- **`server/src/Dodorassik.Api/Controllers/TeamsController.cs`** *(nouveau)* —
  `GET/POST /api/hunts/{huntId}/teams`, `POST join`, `DELETE leave`.
- **`server/src/Dodorassik.Api/Controllers/HuntsController.cs`** *(modifié)* —
  injection `IAntiCheatService` + `IHubContext<CompetitiveHuntHub>` ; nouvel
  endpoint `GET .../leaderboard` (REST + push SignalR après accepted submit) ;
  anti-triche activé en mode compétitif.
- **`server/src/Dodorassik.Api/Services/AntiCheatService.cs`** *(nouveau)* —
  implémentation : validation ordre via `BlocksNext` + haversine speed check 40 km/h.
- **`server/src/Dodorassik.Api/Hubs/CompetitiveHuntHub.cs`** *(nouveau)* —
  `JoinHunt(huntId)` / `LeaveHunt(huntId)` ; groupe par huntId.
- **`server/src/Dodorassik.Api/Program.cs`** *(modifié)* — `AddSignalR()`,
  `MapHub<CompetitiveHuntHub>`, `IAntiCheatService` (scoped), JWT events pour
  passer le token en query string (`?access_token=`), `AllowCredentials` CORS.

> **Migration EF Core** : exécuter localement
> `dotnet ef migrations add AddTeamsAndCompetitive` puis committer le résultat.

#### Godot (GDScript 4.6)

- **`godot/scripts/autoload/competitive_hub_client.gd`** *(nouveau)* — autoload
  SignalR JSON WebSocket : negotiate → handshake → JoinHunt → dispatch
  `LeaderboardUpdated`. Ping/Pong. Reconnexion manuelle via `connect_to_hunt`.
- **`godot/scripts/autoload/app_state.gd`** *(modifié)* — ajout `active_team`,
  signal `active_team_changed`, `set_active_team()`, reset sur `clear_session`.
- **`godot/scripts/autoload/api_client.gd`** *(modifié)* — méthodes teams
  (`list_teams`, `create_team`, `join_team`, `leave_team`) + `get_leaderboard` +
  `submit_step` accepte `team_id` optionnel (wrapper `{payload, teamId}`).
- **`godot/scripts/autoload/router.gd`** *(modifié)* — routes `team_select`,
  `leaderboard`.
- **`godot/scripts/ui/team_select_screen.gd`** *(nouveau)* — création / rejoindre
  équipe avant une chasse compétitive ; redirige vers `hunt_runner` après.
- **`godot/scripts/ui/leaderboard_screen.gd`** *(nouveau)* — classement en temps
  réel (SignalR) ou polling 10 s (relaxed/offline) ; médailles 🥇🥈🥉 ; durée
  formatée.
- **`godot/scripts/ui/hunt_runner.gd`** *(modifié)* — chronomètre en mode
  compétitif ; redirection auto vers `team_select` si pas d'équipe ; bouton
  « Classement » ; messages anti-triche lisibles.
- **`godot/project.godot`** *(modifié)* — autoload `CompetitiveHubClient`.

#### Docs

- **`docs/ROADMAP.md`** — Phase 3 entièrement cochée avec détail.
- **`docs/CLAUDE-LOG.md`** — cette entrée.

### Security & Privacy review

- **Pas de nouvelle PII.** `Team.Name` est un pseudonyme libre. `TeamMember`
  stocke uniquement `UserId` (FK existante).
- **Autorisation.** `TeamsController` est `[Authorize]` global ; join vérifie
  `user.FamilyId == team.FamilyId` côté serveur (pas seulement UI).
- **JWT query string.** Le token dans `?access_token=` est limité au segment
  `/hubs` (condition dans `OnMessageReceived`). C'est la seule façon authentifiée
  de connecter un WebSocket sans header custom. L'exposition est identique à
  celle d'un Bearer header sur les requêtes normales.
- **Anti-triche.** Les rejections (`step_order_violation`, `gps_speed_implausible`)
  retournent un message générique sans exposer de détails internes.
- **SignalR CORS.** `AllowCredentials` ajouté — compatible avec la whitelist
  d'origines existante. `AllowAnyOrigin` toujours interdit en prod.
- **Logs.** Aucun PII dans les nouveaux logs (`push_warning` sur code HTTP, pas
  sur contenu utilisateur).
- **Données enfants.** Aucune donnée d'enfant introduite.

---

## 2026-04-26 — MVP : modération super-admin + plugin Android natif + dette de tests

**Branche** : `claude/mvp-moderation-android-tests`
**Commit** : `615fc3f`

### Requête utilisateur

> intégres immédiatement toutes tes remarques (passage sur les dettes) et
> commence à livrer un MVP exploitable avec les trois points suggérés

### Analyse

Trois axes priorisés, plus la dette discrète :

1. **Plugin Android natif** — `LocationModule`, `CameraModule`,
   `BluetoothModule` étaient des stubs `not_implemented`. Implémentation :
   - `LocationModule` synchrone avec `FusedLocationProviderClient.getCurrentLocation`,
     `CountDownLatch` 5 s, annulation propre si timeout.
   - `CameraModule` asynchrone : intent `ACTION_IMAGE_CAPTURE` + `FileProvider`
     pointant sur `getExternalFilesDir(DIRECTORY_PICTURES)` (espace privé
     application, non visible dans la galerie). Le résultat arrive via
     `onMainActivityResult` → signal `photo_captured`.
   - `BluetoothModule` asynchrone : `BluetoothLeScanner` avec un
     `ScanFilter.setDeviceAddress` par MAC de la whitelist + double
     filtre côté `onScanResult` (belt-and-braces) ; sentinel timeout
     émis comme `(name="", address="", rssi=-1)`.
   - Wiring Gradle complet : `build.gradle` (compileOnly godot-lib,
     play-services-location, androidx.core), `settings.gradle`,
     `consumer-rules.pro`, ressource `dodorassik_file_paths.xml`,
     mise à jour de `AndroidManifest.xml` pour déclarer le FileProvider.
   - `device_services.gd` adapté pour le pattern pending+signal : le
     wrapper Godot `await` les signaux `photo_captured` et
     `bluetooth_device_found` après l'appel natif.

2. **Modération super-admin** (item phase 4 cocher) — workflow complet
   `Draft → Submitted → Published / Rejected`, plus `Archived`. Plus aucun
   `POST /api/hunts/{id}/publish` accessible à un creator : la
   publication est l'apanage du super-admin via `AdminHuntsController`.
   Endpoints :
   - `POST /api/hunts/{id}/submit-for-review`, `/withdraw`, `/archive`
   - `GET /api/admin/hunts?status=...`, `POST /api/admin/hunts/{id}/approve`,
     `/reject` (raison ≥ 5 chars obligatoire), `/takedown` (urgence)
   - Verrou : `PUT /api/hunts/{id}` et les endpoints clue sur une chasse
     `Submitted` ou `Published` retournent `409 hunt_locked`.
   - `Hunt` gagne `SubmittedAtUtc`, `ReviewedAtUtc`, `ReviewedById`,
     `RejectionReason` (≤ 2 000 chars). Index `IX_Hunts_Status` pour
     accélérer la queue.
   - Filtre du `GET /api/hunts` : par défaut anonyme/non-creator → seules
     les `Published` ; super_admin peut filtrer par `?status=` ; creator
     avec `?mine=true` voit ses propres drafts.

3. **Tests des nouveautés** — neuf nouveaux fichiers/sections couvrent
   la dette signalée :
   - `FamiliesApiTests` (create/join/leave/404)
   - `PublicApiTests` (Published only, fenêtre événement, filtre catégorie,
     anti-fuite PII)
   - `HuntsModerationApiTests` (workflow complet, raison obligatoire,
     locked-on-submitted, takedown, accès player refusé)
   - Extensions `AuthApiTests` : signup creator, refus self-promotion
     super_admin/admin/inconnu (Theory)
   - Extensions `HuntsApiTests` : duplicate clue, too_many_clues, PUT
     upsert + suppression d'orphelins, PUT cross-creator forbidden,
     `/clues` POST duplicate, `?mine=true`
   - `TestUserHelper` factorise register/promote.

4. **Rate limit Razor `/Signup`** — `[EnableRateLimiting("auth-register")]`
   posé sur `OnPostAsync` (pas sur la classe pour ne pas limiter le GET).
   Le bypass via le formulaire web est désormais bouché.

5. **Documentation Testcontainers** — `server/tests/Dodorassik.Api.Tests/README.md`
   liste les limites de l'InMemory provider (jsonb, contraintes uniques,
   cascade, transactions, fonctions Postgres) et donne le snippet exact
   pour passer à Testcontainers PostgreSQL en option.

Alternatives rejetées :

- **Bloquer la photo en synchrone** comme la location → impossible :
  `startActivityForResult` est asynchrone et bloquer le thread Godot
  empêche le rendu UI. Le pattern `pending + signal` est imposé.
- **Sauter le double filtre Bluetooth** côté Java → garder pour défense
  en profondeur si fuite OEM Android.
- **Permettre au super-admin d'éditer une `Published` en place** →
  bypass de modération. La chasse doit être archivée puis recréée.

### Modifications

#### Plugin Android (Java + Gradle)

- **`godot/android/plugin/src/main/java/com/dodorassik/device/LocationModule.java`**
  réécrit (FusedLocationProviderClient, timeout 5 s).
- **`.../CameraModule.java`** réécrit (intent + FileProvider).
- **`.../BluetoothModule.java`** réécrit (LeScanner + whitelist).
- **`.../DodorassikDevice.java`** : signal `photo_captured`, override
  `onMainActivityResult`.
- **`godot/android/plugin/src/main/AndroidManifest.xml`** : provider
  FileProvider via `${applicationId}.fileprovider`.
- **`.../res/xml/dodorassik_file_paths.xml`** *(nouveau)*
- **`.../build.gradle`**, **`.../settings.gradle`**,
  **`.../consumer-rules.pro`** *(nouveaux)*

#### Côté Godot

- **`godot/scripts/autoload/device_services.gd`** : signal
  `photo_captured` ajouté, wrappers `capture_photo`/`scan_bluetooth`
  await les signaux natifs.
- **`godot/scripts/ui/hunt_runner.gd`** : photo step renvoie
  `photo_size_bytes` + `photo_taken: true`, jamais le path
  (cf. PRIVACY.md §3).

#### Backend — modération

- **`server/src/Dodorassik.Core/Domain/Enums.cs`** : `Submitted`, `Rejected`.
- **`server/src/Dodorassik.Core/Domain/Hunt.cs`** : 4 champs modération.
- **`server/src/Dodorassik.Infrastructure/Persistence/AppDbContext.cs`** :
  `RejectionReason` HasMaxLength + index `Status`.
- **`server/src/Dodorassik.Api/Controllers/AdminHuntsController.cs`** *(nouveau)*
- **`server/src/Dodorassik.Api/Controllers/HuntsController.cs`** : Publish→
  SubmitForReview, Withdraw, Archive, hunt_locked guards, List filtre
  par défaut Published.
- **`server/src/Dodorassik.Api/Dtos/HuntDtos.cs`** : `HuntDto` étendu.

#### Backend — rate limit Razor

- **`server/src/Dodorassik.Api/Pages/Signup.cshtml.cs`** :
  `[EnableRateLimiting("auth-register")]` sur `OnPostAsync`.

#### Tests

- **`server/tests/Dodorassik.Api.Tests/TestUserHelper.cs`** *(nouveau)*
- **`.../FamiliesApiTests.cs`**, **`.../PublicApiTests.cs`**,
  **`.../HuntsModerationApiTests.cs`** *(nouveaux)*
- **`.../AuthApiTests.cs`**, **`.../HuntsApiTests.cs`** *(étendus)*
- **`.../README.md`** *(nouveau)* : limites InMemory + recette
  Testcontainers.

#### SQL

- **`server/db/init.sql`** : 4 colonnes modération + index
  `IX_Hunts_Status`.
- **`server/db/migrate_add_moderation.sql`** *(nouveau, idempotent)*

#### Documentation

- **`docs/ROADMAP.md`** : phase 2 Android natif coché, phase 4
  "Validation super-admin" coché, couverture des tests étendue.
- **`docs/API.md`** : sections "Workflow de modération", "Édition
  verrouillée", "Admin (modération)" ; ancien `/publish` retiré.
- **`docs/CLAUDE-LOG.md`** : cette entrée.

### Security & Privacy review

- **Pas de nouvelle PII enfant**. `ReviewedById` référence un
  super_admin (adulte) uniquement.
- **Modération obligatoire** (CLAUDE.md §3, art. 8 RGPD renforcé) :
  test `PublicApiTests.Returns_only_published_hunts` couvre les
  statuts intermédiaires (`Draft`, `Submitted`, `Rejected`, `Archived`).
- **Logs de modération** : seuls `HuntId` et `ReviewerId` (Guid),
  jamais le contenu de la chasse, ni le creator, ni la raison de rejet
  → conforme `CLAUDE.md` §2.10.
- **Rate limit complet** : Razor n'est plus un bypass. Invariant
  `SECURITY.md` §4 respecté.
- **Photo** : `hunt_runner.gd` ne soumet plus le `path` au serveur,
  juste `photo_taken` + taille — durcissement par rapport au commit
  initial.
- **Bluetooth filter** : double filtre (OS + plugin), aucun appareil
  tiers ne fuit jamais à GDScript, même via les logs.
- **Verrou `hunt_locked`** : empêche le "approval drift" (mutation
  silencieuse d'une chasse approuvée).
- **Tests InMemory** : aucune donnée hors process. La voie
  Testcontainers est cadrée comme option dev locale.

### Reste à faire (suivi)

- Validation sur appareil physique du plugin Android (build AAR + test
  GPS / photo / Bluetooth Android 14+).
- Refresh tokens (`SECURITY.md` §10).
- Console super-admin web Blazor (phase 5).

---

## 2026-04-26 — Inscription créateur Godot + interface web publique

**Branche** : `claude/add-creator-signup-TuwbZ`
**Commit** : (en cours)

### Requête utilisateur

> Erreur PostgreSQL "le rôle dodorassik n'est pas autorisé à se connecter"
> (SqlState 28000).
> Ajoutes la possibilité de créer un compte directement depuis l'application
> Godot : en tant que créateur. Les comptes joueur ne nécessitent pas de
> création obligatoire mais il faut pouvoir prévoir l'enregistrement de la
> session actuelle et donc la création d'un compte à tout moment.
> L'application API côté serveur doit être accompagnée d'une interface web
> client pour pouvoir créer son compte sur le site web. Côté serveur une page
> publique indique les événements/chasses du moment en cours ainsi que les
> chasses permanentes (exemple : sentier).

### Analyse

**Erreur DB 28000** : le rôle PostgreSQL `dodorassik` n'a pas le droit LOGIN.
Causes probables : volume Docker persistant d'une instance créée via
`CREATE ROLE` sans `LOGIN`. Le `docker-compose.yml` existant utilise
`POSTGRES_USER: dodorassik` (LOGIN inclus par défaut). Fix : supprimer le
volume et relancer (`docker compose down -v && docker compose up -d`).
Aucun changement de code nécessaire — la config est déjà correcte.

**Inscription créateur depuis Godot** : l'endpoint `POST /api/auth/register`
existant créait toujours un compte Player. Ajout d'un champ `Role?` optionnel
dans `RegisterRequest`, acceptant `"player"` ou `"creator"` uniquement
(SuperAdmin non auto-assignable). `AuthController.Register` valide la valeur
avant création. Le nouvel écran `signup_screen.gd` gère l'inscription avec
validation locale (longueur, email basique, confirmation password) puis
auto-login sur réponse 200.

**Sauvegarde session joueur** : un joueur non connecté voit désormais le
bouton "Créer un compte / Sauvegarder ma session" dans `player_home`, qui
mène vers `signup_screen` avec `target_role = PLAYER`. Cela respecte
l'invariant Privacy §1 (pas de compte enfant forcé — c'est optionnel et
initié par l'adulte).

**Interface web publique** (Razor Pages intégrées dans `Dodorassik.Api`) :
- `Program.cs` : ajout `AddRazorPages()` + `MapRazorPages()` + `UseStaticFiles()`.
- `Pages/Index.cshtml` : page publique listant les événements en cours et
  les parcours permanents, requête EF Core directe (même processus, pas de
  HTTPClient redondant).
- `Pages/Signup.cshtml` : formulaire d'inscription web avec les mêmes
  contraintes que l'API (PBKDF2, validation DTO, pas de SuperAdmin).
- `GET /api/public/hunts` : endpoint JSON dédié sans auth, filtrable par
  `?category=event|permanent`, visible par Godot et le web.

**HuntCategory** : nouvel enum `Permanent` (0) / `Event` (1) sur `Hunt`,
avec `EventStartUtc` / `EventEndUtc` nullable pour les événements bornés.
Migration SQL fournie dans `db/migrate_add_hunt_category.sql` (idempotent).

Alternatives rejetées :
- Réutiliser `HuntMode` pour distinguer permanent/événement → trop ambigu,
  `Mode` concerne le scoring, pas la nature du parcours.
- Page Blazor séparée pour le web → overkill phase 5, Razor Pages dans le
  même projet suffit.
- Appel HTTP interne depuis Razor Pages vers l'API → redondance inutile
  quand les services sont dans le même processus.

### Modifications

#### Backend

- **server/src/Dodorassik.Core/Domain/Enums.cs** — Ajout `HuntCategory`.
- **server/src/Dodorassik.Core/Domain/Hunt.cs** — Champs `Category`,
  `EventStartUtc`, `EventEndUtc`.
- **server/src/Dodorassik.Api/Dtos/AuthDtos.cs** — `RegisterRequest.Role?`
  optionnel.
- **server/src/Dodorassik.Api/Controllers/AuthController.cs** — Validation
  du rôle demandé (player|creator uniquement).
- **server/src/Dodorassik.Api/Dtos/HuntDtos.cs** — `HuntDto` étendu avec
  `Category`, `LocationLabel`, `EventStartUtc`, `EventEndUtc` ; mapping
  `ParseHuntCategory` ; `CreateHuntRequest` étendu.
- **server/src/Dodorassik.Api/Controllers/HuntsController.cs** — Utilise
  les nouveaux champs dans `Create`.
- **server/src/Dodorassik.Api/Controllers/PublicController.cs** *(nouveau)* —
  `GET /api/public/hunts` sans auth, répond `PublicHuntsResponse`.
- **server/src/Dodorassik.Api/Program.cs** — `AddRazorPages`, `MapRazorPages`,
  `UseStaticFiles`.
- **server/src/Dodorassik.Api/Pages/_ViewImports.cshtml** *(nouveau)*
- **server/src/Dodorassik.Api/Pages/_ViewStart.cshtml** *(nouveau)*
- **server/src/Dodorassik.Api/Pages/Shared/_Layout.cshtml** *(nouveau)*
- **server/src/Dodorassik.Api/Pages/Index.cshtml** + `Index.cshtml.cs` *(nouveaux)*
- **server/src/Dodorassik.Api/Pages/Signup.cshtml** + `Signup.cshtml.cs` *(nouveaux)*
- **server/src/Dodorassik.Api/wwwroot/css/dodorassik.css** *(nouveau)* —
  styles minimaux sans dépendance externe.
- **server/db/init.sql** — Nouvelles colonnes dans `Hunts`.
- **server/db/migrate_add_hunt_category.sql** *(nouveau)* — Migration
  idempotente pour bases existantes.

#### Godot

- **godot/scripts/autoload/api_client.gd** — `register()` accepte un
  paramètre `role` (défaut `"player"`).
- **godot/scripts/ui/signup_screen.gd** *(nouveau)* — Formulaire d'inscription
  complet avec validation locale et auto-login post-création.
- **godot/scripts/ui/login_screen.gd** — Bouton "Créer un compte" renvoyant
  vers `signup` avec `target_role` hérité.
- **godot/scripts/ui/player_home.gd** — Bouton "Créer un compte / Sauvegarder
  ma session" affiché si non authentifié.
- **godot/scripts/autoload/router.gd** — Route `"signup"` ajoutée.

#### Documentation

- **docs/ROADMAP.md** — Phase 5 : items livrés cochés.
- **docs/CLAUDE-LOG.md** — Cette entrée.

### Security & Privacy review

- **Pas de nouvelle PII enfant** introduite. L'inscription reste réservée aux
  adultes ; aucun champ identifiant d'enfant n'existe dans les formulaires.
- **Rôle SuperAdmin non auto-assignable** : la validation `"player" or "creator"`
  côté API et le formulaire web excluent explicitement `super_admin`.
- **Mêmes invariants de sécurité** pour la page Signup web que l'API : PBKDF2
  (via `IPasswordHasher` injecté), même `InputLimits`, pas de logging du
  mot de passe, pas de fuite "email déjà pris" dans un timing différent
  (note : la page Razor retourne "adresse déjà utilisée" — acceptable car
  le RGPD exige de prévenir l'utilisateur d'une tentative sur son compte).
- **Page publique** : aucune donnée utilisateur ni token dans les réponses
  `/api/public/hunts` et `/` — seulement noms, descriptions, coordonnées
  de *lieux* (étapes de parcours), conformes à la règle GPS §2 de CLAUDE.md.
- **Rate limiting** : la page Signup web bénéficie du limiteur global
  (60 req/min par IP). Le formulaire ne bypasse pas le limiteur de l'endpoint
  API car il passe par le PageModel (même process). Pour une protection
  équivalente au rate limiter `auth-register`, ajouter un middleware dédié
  aux routes Razor en v2.
- **CSS en local** : aucun CDN externe, aucune requête tiers, conforme à
  l'invariant Privacy §4 (pas de tiers analytics).
- **CORS** : les Razor Pages ne sont pas concernées par la politique CORS
  (rendered server-side, pas d'appel fetch cross-origin).

---

## 2026-04-26 — Phase 2 : éditeur de chasse complet (steps + clues) + build Android

**Branche** : `claude/implement-phase-2-VulAm`
**Commit** : (en cours)

### Requête utilisateur

> Réanalyses la solution et analyses ensuite docs/ROADMAP.md et implémente la
> suite (phase 2).

### Analyse

Re-lecture complète de la solution (16 fichiers backend, 12 scripts GDScript,
plugin Android, tests). Les items Phase 2 déjà cochés sont stables. Les trois
items restants :

1. **Éditeur Godot avec steps + clues** : `hunt_editor.gd` ajoutait des steps
   round-robin sans formulaire d'édition, et ne gérait pas du tout les clues
   physiques. La refonte complète était nécessaire.
2. **Build Android signé documenté** : le README du plugin listait les pré-requis
   mais manquait le wiring Gradle, la génération keystore, l'export preset et
   l'install ADB.
3. **Plugin Android natif** : structure Java déjà présente, non compilable
   (manque `build.gradle`). Ajout des instructions de build ; test sur appareil
   physique reste hors sandbox.

Choix notables :

- **PUT full-replace** pour les mises à jour de chasse : semantique plus simple
  que des PATCH partiels au stade MVP. L'upsert côté serveur (match par Id si
  présent, sinon création) permet au client de conserver les ids après le
  premier save et de ne pas recréer les entités à chaque sauvegarde.
- **Normalisation des codes de clue en MAJUSCULES** côté serveur et côté
  éditeur Godot : évite les doublons insensibles à la casse et simplifie la
  comparaison en jeu.
- **`publish_hunt` ajouté à `api_client.gd`** en même temps que les helpers
  clue/update, pour compléter la couverture des endpoints existants.
- **`ClueDto` inclus dans `HuntDto`** et dans la réponse de `List` : le listing
  des chasses du créateur affiche maintenant les infos complètes sans second
  appel. Coût réseau négligeable pour la taille attendue des chasses familiales.
- **Params type-spécifiques dans l'éditeur** : formulaires inline plutôt que
  dialogs modaux pour rester dans le paradigme `base_screen.gd`. Un step de type
  `location` affiche lat/lon/rayon, `text_answer`/`clue_collect` affiche
  "réponse attendue", `bluetooth` affiche la liste des adresses MAC.

### Modifications

#### Backend

- **server/src/Dodorassik.Api/Validation/InputLimits.cs** — Ajout des
  constantes clue : `ClueCodeMaxLength`, `ClueTitleMaxLength`,
  `ClueRevealMaxLength`, `CluesPerHuntMax`.
- **server/src/Dodorassik.Api/Dtos/HuntDtos.cs** — Ajout `ClueDto`,
  `CreateClueRequest`, `UpdateHuntRequest` ; `HuntDto` inclut désormais
  `List<ClueDto> Clues` ; `CreateHuntRequest` accepte `List<CreateClueRequest>?
  Clues` ; mapping `Clue.ToDto()`.
- **server/src/Dodorassik.Api/Controllers/HuntsController.cs** — `List`
  inclut les clues ; `Create` gère les clues avec déduplication de codes ;
  nouveau `PUT /{id}` (upsert steps + clues + suppression des orphelins) ;
  `POST /{huntId}/clues` ; `DELETE /{huntId}/clues/{clueId}`.

#### Godot

- **godot/scripts/ui/hunt_editor.gd** — Réécriture complète : formulaire de
  step par ligne (type via OptionButton, titre, description, params
  type-spécifiques, points, boutons ↑↓ et suppression) ; section clues
  (code, titre, reveal, points, suppression) ; logique de save CREATE/UPDATE
  avec rafraîchissement des ids serveur après chaque sauvegarde.
- **godot/scripts/autoload/api_client.gd** — Ajout `update_hunt()`,
  `publish_hunt()`, `add_clue()`, `delete_clue()`.

#### Documentation

- **godot/android/plugin/README.md** — Instructions complètes : setup Gradle,
  récupération de `godot-lib`, `build.gradle` + `settings.gradle`, compilation
  AAR debug/release, génération du keystore debug, configuration du preset
  d'export Godot 4.6, install ADB.
- **docs/ROADMAP.md** — Phase 2 : items steps+clues et build Android signés
  cochés ; item plugin natif redéfini (structure compilable, test appareil
  physique reste à faire).
- **docs/CLAUDE-LOG.md** — Cette entrée.

### Security & Privacy review

- **Pas de nouvelle PII** : les clues contiennent un code court, un titre et
  un texte de révélation — aucun identifiant personnel, aucune photo en base.
- **Autorisation propriétaire vérifiée côté serveur** : `PUT /{id}`, `POST
  /clues`, `DELETE /clues/{clueId}` vérifient que `hunt.CreatorId ==
  currentUserId` avant toute mutation. Seul `super_admin` peut outrepasser.
- **Codes de clue non-PII** : normalisés en majuscules, max 64 caractères,
  pas d'information personnelle. Déduplication serveur = protection contre
  les injections d'homonymes.
- **Rate limiting** : les nouveaux endpoints passent par les limiteurs globaux
  définis dans `Program.cs`; aucun contournement introduit.
- **Pas de logs PII** ajoutés dans les nouveaux controllers.
- **Upsert by Id** : un client malveillant ne peut pas modifier une clue d'une
  autre chasse car la recherche filtre sur `HuntId` avant de matcher l'Id.

---

## 2026-04-26 — Inscription créateur Godot + interface web publique

**Branche** : `claude/godot-family-game-7x9Ki`
**Commit** : `5566752`

### Requête utilisateur

> J'utilise Godot 4.6, mets à jour et continue sur la phase 2. Mets également
> à jour la solution avec les fichiers CLAUDE.md ou autre fichier .MD afin de
> s'assurer que la solution soit security by design, privacy by design et que
> chaque ajout ou modification s'assure de cela. Il faudra également consigner
> l'ensemble des requêtes de une analyse des modifications ou ajouts effectués
> par Claude dans un fichier MD.

### Analyse

Trois axes :

1. **Cadre de gouvernance** : créer `CLAUDE.md` à la racine (lu par Claude
   en premier), `docs/SECURITY.md` (threat model + contre-mesures),
   `docs/PRIVACY.md` (données collectées, RGPD, mineurs), et le présent
   `docs/CLAUDE-LOG.md`.
2. **Audit + correctifs sur l'existant** : la solution mise en place au
   commit précédent a déjà des bonnes pratiques (PBKDF2, JWT bien validé)
   mais aussi des trous (`AllowAnyOrigin` global, pas de rate limiting,
   pas de validation de longueur des entrées, pas d'export/delete RGPD).
3. **Phase 2 technique** : Godot 4.3 → 4.6, migrations EF Core écrites à
   la main (pas de `dotnet ef` dispo dans le sandbox), projet de tests
   d'intégration, stub plugin Android pour GPS/caméra/Bluetooth, écran
   sélection de famille.

Choix notables :

- **Migrations à la main** plutôt que d'attendre. Le SQL généré reste
  reproductible côté .NET et peut être régénéré sur la machine du dev avec
  `dotnet ef migrations add Initial --force` si besoin (la signature
  resterait identique tant que le DbContext ne change pas).
- **CORS** déplacé en config typée par environnement, refus explicite de
  `AllowAnyOrigin` hors `Development`.
- **Rate limiter natif .NET 8** plutôt qu'AspNetCoreRateLimit (zéro dépendance
  externe, suffisant pour la phase 2).
- **Tests** : `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers.PostgreSql`
  rejeté pour l'instant (besoin Docker côté CI) ; remplacé par une
  configuration EF Core InMemory réservée aux tests, avec un `appsettings.Test.json`.
- **Plugin Android** : structure de fichiers Java + `.gdap` créés mais
  build laissé au dev (nécessite Android Studio). Le wrapper GDScript
  `DeviceServices` détecte la présence du plugin à l'exécution.

### Modifications

#### Documentation

- **CLAUDE.md** *(nouveau)* — règles opérationnelles pour Claude.
- **docs/SECURITY.md** *(nouveau)* — modèle de menaces, contre-mesures.
- **docs/PRIVACY.md** *(nouveau)* — politique RGPD, public mineurs.
- **docs/CLAUDE-LOG.md** *(nouveau)* — ce journal.
- **docs/ROADMAP.md** *(modifié)* — phase 2 cochée pour les items livrés.
- **docs/API.md** *(modifié)* — nouveaux endpoints RGPD documentés.

#### Godot 4.6

- **godot/project.godot** — `config/features` → `4.6`, ajout des
  permissions Android (location/camera/bluetooth) déclarées via
  `permissions/*` (lus par le plugin d'export Android 4.6).
- **godot/scripts/autoload/device_services.gd** — détection runtime du
  plugin natif `DodorassikDevice`, fallback stub identique à avant.

#### Backend — correctifs security/privacy

- **server/src/Dodorassik.Api/Program.cs** — :
  - CORS typé par environnement (`Cors:AllowedOrigins` config).
  - Rate limiter natif (`AddRateLimiter`).
  - `MapInboundClaims = false` pour des claims propres.
  - `RequireHttpsMetadata` strict hors dev.
- **server/src/Dodorassik.Api/Validation/InputLimits.cs** *(nouveau)* —
  constantes partagées pour bornes de validation.
- **server/src/Dodorassik.Api/Dtos/AuthDtos.cs** — annotations
  `[EmailAddress]`, `[StringLength]`, `[Required]`.
- **server/src/Dodorassik.Api/Dtos/HuntDtos.cs** — limites step count,
  taille payload.
- **server/src/Dodorassik.Api/Controllers/AuthController.cs** — purge des
  logs PII, validation explicite, message d'erreur générique sur 401
  (pas de fuite "email existe vs password faux").
- **server/src/Dodorassik.Api/Controllers/UsersController.cs** *(nouveau)* —
  endpoints RGPD `GET /api/users/me/export`, `DELETE /api/users/me`,
  `PATCH /api/users/me`.
- **server/src/Dodorassik.Infrastructure/Persistence/Migrations/...** —
  migration `Initial` + `DesignTimeFactory` pour `dotnet ef`.

#### Backend — phase 2

- **server/tests/Dodorassik.Api.Tests/Dodorassik.Api.Tests.csproj** *(nouveau)*
- **server/tests/Dodorassik.Api.Tests/AuthApiTests.cs** *(nouveau)* — couvre
  register/login happy path + email déjà pris + password trop court +
  rate limit.
- **server/tests/Dodorassik.Api.Tests/HuntsApiTests.cs** *(nouveau)* —
  CRUD + autorisation rôle.
- **server/tests/Dodorassik.Api.Tests/TestingWebAppFactory.cs** *(nouveau)*
  — bootstrap WebApplicationFactory + InMemory.

#### Godot — écran famille + plugin Android

- **godot/scripts/ui/family_select_screen.gd** *(nouveau)* — sélection ou
  création de famille à la connexion.
- **godot/scripts/autoload/router.gd** — route `family_select` ajoutée.
- **godot/android/plugin/** *(nouveau)* — squelette plugin (Java + .gdap).
- **godot/android/plugin/README.md** — instructions de build.

### Security & Privacy review

- **Pas de nouvelle PII** introduite. Les nouveaux endpoints RGPD lisent
  ou suppriment uniquement les données déjà existantes.
- **CORS** durci en prod : conforme à l'invariant §2.6 de `CLAUDE.md`.
- **Rate limit** : nouvel invariant ajouté à `SECURITY.md` §4.
- **Logs** : confirmation que les controllers ne logguent ni email ni
  mot de passe ; le message 401 est générique pour ne pas révéler
  l'existence d'un email (anti enum).
- **Plugin Android** : permissions déclarées de manière minimale, scan
  Bluetooth limité à la whitelist du hunt (cf. `PRIVACY.md` §4).
- **Tests** : la base InMemory ne crée aucune donnée sortant du processus.
- **Migrations** : pas de privilège `CREATE` requis pour l'app au runtime
  (à appliquer hors process en prod, conforme `SECURITY.md` §6).

---

## Modèle d'entrée future

```markdown
## YYYY-MM-DD — Titre court

**Branche** : ...
**Commit** : ...

### Requête utilisateur
> ...

### Analyse
...

### Modifications
- file/path : nature
- ...

### Security & Privacy review
- ...
```
