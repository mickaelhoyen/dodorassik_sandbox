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

## 2026-04-26 — Phase 2 + cadre security/privacy

**Branche** : `claude/godot-family-game-7x9Ki`
**Commit** : (à remplir)

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
