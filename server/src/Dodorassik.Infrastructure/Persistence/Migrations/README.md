# EF Core migrations

Les fichiers de migration EF Core ne sont **pas committés depuis ce sandbox**.
Leur snapshot doit refléter, octet près, le `DbContext` au moment où ils sont
générés ; les générer sur la machine du dev garantit qu'on n'introduit pas un
delta entre la version d'EF Core utilisée localement et celle qui produit le
snapshot.

## Workflow dev (machine locale)

`DesignTimeDbContextFactory` (cf. `../DesignTimeDbContextFactory.cs`) permet
à `dotnet ef` de fonctionner sans démarrer l'host ASP.NET — la chaîne de
connexion par défaut suffit.

```bash
cd server

# Postgres up
docker compose up -d

# Première génération (ou après un reset)
dotnet ef migrations add InitialCreate \
    --project src/Dodorassik.Infrastructure \
    --startup-project src/Dodorassik.Api \
    --output-dir Persistence/Migrations

# Appliquer
dotnet ef database update \
    --project src/Dodorassik.Infrastructure \
    --startup-project src/Dodorassik.Api
```

### Reset complet

Quand le `DbContext` change suffisamment pour vouloir repartir de zéro :

```bash
dotnet ef database drop -f \
    --project src/Dodorassik.Infrastructure \
    --startup-project src/Dodorassik.Api

find src/Dodorassik.Infrastructure/Persistence/Migrations \
    -maxdepth 1 -type f ! -name 'README.md' -delete

# puis re-faire migrations add + database update
```

## Déploiement prod

Conformément à `CLAUDE.md` §2, le user PostgreSQL applicatif n'a **pas** le
privilège `CREATE` au runtime. Les migrations s'appliquent hors process avec
un user à privilèges élevés. On génère le SQL à la volée à partir du même
modèle EF — pas de script SQL maintenu à la main, donc pas de risque de
dérive vis-à-vis du `DbContext` :

```bash
dotnet ef migrations script --idempotent \
    --project src/Dodorassik.Infrastructure \
    --startup-project src/Dodorassik.Api \
    | psql "$DODORASSIK_ADMIN_CONN"
```

## Sécurité (rappel `CLAUDE.md`)

- L'user applicatif tourne en `SELECT/INSERT/UPDATE/DELETE` uniquement.
- Aucune migration ne doit insérer de données personnelles (`User`,
  `Family`, `StepSubmission`). Si du seed est nécessaire, l'introduire dans
  un script séparé et ne **jamais** l'exécuter en prod.
