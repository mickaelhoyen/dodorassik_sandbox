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
            // Replace the Npgsql DbContext with a SQLite one bound to our
            // shared in-memory connection.
            var npgsqlDescriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(npgsqlDescriptor);
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
