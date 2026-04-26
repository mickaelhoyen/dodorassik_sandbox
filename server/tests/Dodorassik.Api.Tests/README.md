# Dodorassik.Api.Tests

Tests d'intégration de l'API. Chaque classe instancie une
`TestingWebAppFactory` (xUnit `IClassFixture`) qui :

1. Démarre l'API en process avec `Microsoft.AspNetCore.Mvc.Testing`.
2. Remplace le `DbContext` Npgsql par un provider **EF Core InMemory**
   isolé par GUID (`dodorassik-tests-<guid>`) — donc **pas de Postgres
   requis pour exécuter les tests**.
3. Surcharge `appsettings` avec un secret JWT factice ≥ 32 caractères et
   une whitelist CORS minimale.

```bash
cd server
dotnet test
```

## Couverture actuelle

| Suite                      | Cible                                                   |
|----------------------------|---------------------------------------------------------|
| `AuthApiTests`             | register / login, anti-énumération, signup creator, refus de self-promotion super_admin |
| `HuntsApiTests`            | create / list / PUT (upsert + suppression d'orphelins) / clues (duplicate, cap), `?mine=true` |
| `HuntsModerationApiTests`  | submit-for-review / withdraw / approve / reject (raison obligatoire) / takedown / verrous d'édition |
| `FamiliesApiTests`         | create / join / leave / 404 si pas de famille          |
| `PublicApiTests`           | catalogue Published only, fenêtre événement, filtre catégorie, anti-fuite PII |
| `UsersApiTests`            | export RGPD, suppression avec confirmation             |

## Limites connues d'EF Core InMemory

L'InMemory provider est **rapide** et **sans dépendance**, parfait pour
les tests de logique applicative — mais il n'émule pas Postgres. Plus
précisément :

- **Pas de typage `jsonb`** : les colonnes `ParamsJson`/`PayloadJson`
  sont stockées comme du texte. Les requêtes utilisant les opérateurs
  Postgres (`->`, `->>`, `@>`) ne sont pas couvertes.
- **Pas de contraintes uniques composites strictes** : EF InMemory
  applique les unique indexes en mémoire, mais pas de la même façon
  que Postgres (différences de comportement sur `null`, conflits sous
  transaction concurrente).
- **Pas de cascade `ON DELETE` natif** : EF lève les FK en mémoire,
  mais l'ordre de suppression peut différer du SQL réel.
- **Pas de transactions réelles** ni de niveaux d'isolation.
- **Pas de fonctions Postgres** (`now()`, `gen_random_uuid()`).

## Faire tourner les tests sur un vrai Postgres (optionnel)

Pour valider les comportements ci-dessus, ajouter
[Testcontainers](https://dotnet.testcontainers.org/modules/postgres/) :

```bash
dotnet add tests/Dodorassik.Api.Tests \
    package Testcontainers.PostgreSql --version 4.1.0
```

Et créer une variante de la factory :

```csharp
public class PostgresWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        // Apply server/db/init.sql once via _pg.ExecScriptAsync(...)
    }

    public new async Task DisposeAsync() => await _pg.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.ConfigureAppConfiguration((_, c) =>
        {
            c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _pg.GetConnectionString(),
                // ... idem TestingWebAppFactory ...
            });
        });
        // Do NOT replace the DbContext — keep Npgsql.
    }
}
```

Décorer chaque test concerné d'un `[Trait("Category", "Postgres")]`
pour que la CI puisse les exécuter dans un job séparé qui dispose de
Docker. Tant que Testcontainers n'est pas en place, la CI standard
peut tourner uniquement sur l'InMemory factory.
