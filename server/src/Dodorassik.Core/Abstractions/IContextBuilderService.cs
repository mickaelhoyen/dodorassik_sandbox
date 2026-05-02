using Dodorassik.Core.Domain.Assistant;

namespace Dodorassik.Core.Abstractions;

public record PhotoInput(string FileName, Stream Data);

public record BuildContextRequest(
    GpsPoint Center,
    IReadOnlyList<GpsPoint> GpsPoints,
    AudienceProfile Audience,
    IReadOnlyList<SponsorConstraint> Sponsors,
    IReadOnlyList<PhotoInput> Photos);

public interface IContextBuilderService
{
    Task<HuntContext> BuildAsync(BuildContextRequest request, CancellationToken ct);
}
