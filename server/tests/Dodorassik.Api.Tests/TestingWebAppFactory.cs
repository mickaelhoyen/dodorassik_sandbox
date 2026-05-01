using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dodorassik.Api.Tests;

/// <summary>
/// Wires the API into the test process backed by a per-factory SQLite
/// in-memory database. SQLite is used (rather than EF Core's InMemory
/// provider) because InMemory does not honour relational semantics
/// (cascade delete, orphan removal, transactions), which silently
/// breaks tests like <c>Update_replaces_steps_and_removes_orphans</c>.
/// </summary>
public class TestingWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public TestingWebAppFactory()
    {
        // ":memory:" with a kept-open connection means the DB lives as long
        // as this factory does, and is dropped on Dispose. Each test class
        // (IClassFixture) gets its own factory → its own isolated DB.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // EF Core SQLite sends PRAGMA foreign_keys = ON only when *it* opens
        // the connection. Since we pre-open and hand the connection in, we must
        // send the PRAGMA ourselves so cascade/restrict semantics are enforced.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=unused",
                ["Jwt:Secret"] = "test-secret-test-secret-test-secret-test-secret-32+chars",
                ["Jwt:Issuer"] = "dodorassik-test",
                ["Jwt:Audience"] = "dodorassik-test-clients",
                ["Cors:AllowedOrigins:0"] = "http://localhost",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContextOptions<AppDbContext> descriptors (Npgsql registers
            // exactly one via TryAdd, but we use RemoveAll to be future-proof).
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Replace with a SQLite DbContext bound to our shared in-memory connection.
            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }
}
