using Dodorassik.Core.Domain.Assistant;

namespace Dodorassik.Core.Abstractions;

public interface ILocationEnricher
{
    Task<LocationContext> EnrichAsync(GpsPoint center, string language, CancellationToken ct);
}
