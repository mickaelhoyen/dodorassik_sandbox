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
