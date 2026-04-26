using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dodorassik.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> when no host is available (e.g. when running
/// <c>dotnet ef migrations add</c> from the Infrastructure project itself).
/// The connection string here is purely a placeholder — the Api project's
/// configuration takes precedence at runtime.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DODORASSIK_DESIGNTIME_CONN")
                   ?? "Host=localhost;Port=5432;Database=dodorassik;Username=dodorassik;Password=dodorassik";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new AppDbContext(options);
    }
}
