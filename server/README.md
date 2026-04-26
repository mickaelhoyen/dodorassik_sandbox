# Serveur — Dodorassik.Api

Solution .NET 8 (ASP.NET Core + EF Core + PostgreSQL).

## Démarrage

```bash
docker compose up -d                                          # PostgreSQL local
dotnet restore
dotnet ef database update \
    --project src/Dodorassik.Infrastructure \
    --startup-project src/Dodorassik.Api
dotnet run --project src/Dodorassik.Api                       # http://localhost:5080
```

> Premier `dotnet ef migrations add Initial` à lancer si le dossier
> `Migrations/` n'existe pas encore — il sera ensuite committé.

Swagger est exposé en dev sur `http://localhost:5080/swagger`.

## Projets

| Projet                    | Rôle                                              |
|---------------------------|---------------------------------------------------|
| `Dodorassik.Core`         | Modèle domaine pur, abstractions (pas d'IO).      |
| `Dodorassik.Infrastructure` | EF Core (`AppDbContext`), hashing PBKDF2.       |
| `Dodorassik.Api`          | Web API, JWT, controllers, DTOs.                  |

## Configuration

`appsettings.json` contient des valeurs de dev. **Toujours** surcharger
`Jwt:Secret` en production via :

- variables d'environnement : `Jwt__Secret=...`
- ou `dotnet user-secrets set Jwt:Secret "..." --project src/Dodorassik.Api`

## Modèle de données

Voir [`../docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md) pour le diagramme
relationnel. Les paramètres de chaque type d'étape sont stockés en `jsonb`
(`HuntStep.ParamsJson`, `StepSubmission.PayloadJson`) pour permettre
d'ajouter de nouveaux types de jeu sans migration.
