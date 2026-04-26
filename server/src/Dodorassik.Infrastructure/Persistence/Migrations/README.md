# EF Core migrations

Les fichiers de migration EF Core ne sont **pas committés depuis le sandbox**
parce que leur snapshot doit absolument refléter, octet près, le `DbContext`
au moment où ils sont générés. Générer les migrations sur la machine du dev
garantit que le snapshot est cohérent avec la version exacte d'EF Core
utilisée localement.

## Première génération

```bash
cd server

# 1. Le DesignTimeDbContextFactory permet à dotnet ef de fonctionner sans
#    bootstrap du host ASP.NET. Une chaîne de connexion factice convient.
dotnet ef migrations add Initial \
    --project src/Dodorassik.Infrastructure \
    --startup-project src/Dodorassik.Api \
    --output-dir Persistence/Migrations

# 2. Vérifier le diff, puis commiter:
git add src/Dodorassik.Infrastructure/Persistence/Migrations
git commit -m "Add initial EF Core migration"
```

## Appliquer les migrations

```bash
docker compose up -d                          # Postgres local
dotnet ef database update \
    --project src/Dodorassik.Infrastructure \
    --startup-project src/Dodorassik.Api
```

## Bootstrap alternatif sans EF Core

Pour un environnement où `dotnet-ef` n'est pas disponible (CI, container
minimal), `db/init.sql` contient le schéma équivalent à `Initial`. Il **doit
être régénéré** chaque fois que le `DbContext` change :

```bash
dotnet ef migrations script --idempotent \
    --project src/Dodorassik.Infrastructure \
    --startup-project src/Dodorassik.Api \
    --output ../db/init.sql
```

## Sécurité (rappel `CLAUDE.md`)

- Le user PostgreSQL applicatif n'a **pas** le privilège `CREATE` au runtime.
  Les migrations sont appliquées hors process avec un user à privilèges
  élevés, puis l'app tourne avec un user en `SELECT/INSERT/UPDATE/DELETE`
  uniquement.
- Aucune migration ne doit insérer de données personnelles (`User`,
  `Family`, `StepSubmission`). Si du seed est nécessaire, l'introduire dans
  un script séparé `db/seed.sql` et ne jamais l'exécuter en prod.
