using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain.Assistant;

namespace Dodorassik.Api.Services;

public class ContextBuilderService : IContextBuilderService
{
    private readonly ILocationEnricher _locationEnricher;
    private readonly IPhotoAnalyzer _photoAnalyzer;

    public ContextBuilderService(ILocationEnricher locationEnricher, IPhotoAnalyzer photoAnalyzer)
    {
        _locationEnricher = locationEnricher;
        _photoAnalyzer = photoAnalyzer;
    }

    public async Task<HuntContext> BuildAsync(BuildContextRequest request, CancellationToken ct)
    {
        var locationTask = _locationEnricher.EnrichAsync(request.Center, request.Audience.Language, ct);
        var photoTasks = request.Photos
            .Select(p => _photoAnalyzer.AnalyzeAsync(p.FileName, p.Data, ct))
            .ToList();

        var allTasks = new List<Task>(photoTasks.Count + 1) { locationTask };
        allTasks.AddRange(photoTasks);
        await Task.WhenAll(allTasks);

        var photoResults = new List<PhotoAnalysisResult>(photoTasks.Count);
        foreach (var t in photoTasks)
            photoResults.Add(await t);

        return new HuntContext(
            await locationTask,
            request.Audience,
            request.Sponsors,
            photoResults,
            request.GpsPoints);
    }
}
