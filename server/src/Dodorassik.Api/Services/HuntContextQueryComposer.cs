using System.Text;
using Dodorassik.Api.Dtos;
using Dodorassik.Core.Domain.Assistant;

namespace Dodorassik.Api.Services;

/// <summary>
/// Compose le texte de requête RAG depuis un HuntContextDto.
/// Ce texte est soit vectorisé (C3) soit tokenisé (fallback tag scoring C2).
/// </summary>
public static class HuntContextQueryComposer
{
    public static (string QueryText, AudienceProfile Audience) Compose(HuntContextDto ctx)
    {
        var sb = new StringBuilder();

        if (ctx.Location.Pois.Count > 0)
        {
            var types = ctx.Location.Pois
                .Select(p => p.Type).Distinct()
                .Take(5);
            sb.Append("Location POI types: ").AppendJoin(", ", types).Append(". ");
        }

        if (ctx.Location.HistoricalFacts.Count > 0)
        {
            sb.Append("Historical context: ");
            foreach (var f in ctx.Location.HistoricalFacts.Take(4))
            {
                sb.Append(f.Label);
                if (f.Description is not null)
                    sb.Append(" (").Append(f.Description).Append(')');
                sb.Append(". ");
            }
        }

        var a = ctx.Audience;
        sb.Append($"Audience: ages {a.AgeMin}-{a.AgeMax}, group {a.GroupSize}, ");
        sb.Append($"mobility {a.Mobility}, duration {a.DurationMinutes} min. ");

        var detectedElements = ctx.PhotoAnalyses
            .SelectMany(p => p.DetectedElements)
            .Distinct().Take(10);
        foreach (var el in detectedElements)
            sb.Append(el).Append(' ');

        var architectureStyles = ctx.PhotoAnalyses
            .Select(p => p.ArchitectureStyle)
            .Where(s => s is not null)
            .Distinct().Take(3);
        foreach (var s in architectureStyles)
            sb.Append(s).Append(' ');

        var audience = new AudienceProfile(
            a.AgeMin, a.AgeMax, a.GroupSize,
            a.Mobility, a.DurationMinutes, a.Language);

        return (sb.ToString().Trim(), audience);
    }
}
