using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dodorassik.Api.Tests;

/// <summary>
/// Wires the API into the test process with an in-memory EF Core provider
/// (per-class isolation via a unique database name) and a dummy JWT secret.
/// </summary>
public class TestingWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"dodorassik-tests-{Guid.NewGuid()}";

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
            // Replace the Npgsql DbContext with an InMemory one for tests.
            var npgsqlDescriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(npgsqlDescriptor);
            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(_databaseName));
        });
    }
}
