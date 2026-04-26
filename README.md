# Dodorassik

Chasse au trésor familiale en environnement réel, jouée sur téléphone par
les adultes pour le compte des enfants.

> Les enfants courent, cherchent, ramassent. Les adultes valident sur le
> téléphone. La chasse peut se dérouler en mode détendu (tout le monde gagne)
> ou compétitif (timing et classement).

## Structure du dépôt

| Dossier   | Rôle                                                   |
|-----------|--------------------------------------------------------|
| `godot/`  | Projet Godot 4 (GDScript). Trois interfaces : joueur, créateur de parcours, super-administrateur. |
| `server/` | Solution .NET 8 (ASP.NET Core + EF Core + PostgreSQL). API REST + JWT. |
| `docs/`   | Architecture, API, roadmap. |

## Démarrage rapide

### Serveur

```bash
cd server
docker compose up -d                 # PostgreSQL local
dotnet restore
dotnet ef database update --project src/Dodorassik.Infrastructure --startup-project src/Dodorassik.Api
dotnet run --project src/Dodorassik.Api
# API disponible sur http://localhost:5080  (Swagger en dev)
```

Avant de lancer en prod, remplacer la valeur `Jwt:Secret` dans
`appsettings.json` (ou via variables d'environnement / user-secrets).

### Client Godot

1. Installer Godot 4.3+
2. Ouvrir le dossier `godot/` dans Godot
3. Lancer `scenes/common/main.tscn`

L'URL du serveur peut être changée à chaud via `ApiClient.set_base_url(...)`
ou éditée dans `user://config.json`.

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — vue d'ensemble technique
- [`docs/API.md`](docs/API.md) — endpoints REST
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — étapes prévues
