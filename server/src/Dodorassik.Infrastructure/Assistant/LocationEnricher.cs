using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain.Assistant;
using Microsoft.Extensions.Logging;

namespace Dodorassik.Infrastructure.Assistant;

public class LocationEnricher : ILocationEnricher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LocationEnricher> _logger;

    private const int OsmRadiusMeters = 500;
    private const int OsmMaxResults = 15;
    private const int WikidataRadiusKm = 1;
    private const int WikidataMaxResults = 8;

    public LocationEnricher(IHttpClientFactory httpClientFactory, ILogger<LocationEnricher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<LocationContext> EnrichAsync(GpsPoint center, string language, CancellationToken ct)
    {
        var poisTask = FetchOsmPoisAsync(center, ct);
        var factsTask = FetchWikidataFactsAsync(center, language, ct);

        await Task.WhenAll(poisTask, factsTask);

        return new LocationContext(
            PlaceName: null,
            Pois: await poisTask,
            HistoricalFacts: await factsTask);
    }

    private async Task<IReadOnlyList<NearbyPoi>> FetchOsmPoisAsync(GpsPoint center, CancellationToken ct)
    {
        // Overpass QL: POIs culturels/historiques/touristiques dans le rayon.
        // On évite l'interpolation dans la requête SQL côté EF Core — ici c'est
        // une requête Overpass avec paramètres numériques (pas d'injection possible).
        var query = $"""
            [out:json][timeout:8];
            (
              node(around:{OsmRadiusMeters},{center.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{center.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})[name][historic];
              node(around:{OsmRadiusMeters},{center.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{center.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})[name][tourism];
              node(around:{OsmRadiusMeters},{center.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{center.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})[name][leisure];
              node(around:{OsmRadiusMeters},{center.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{center.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})[name][natural];
              way(around:{OsmRadiusMeters},{center.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{center.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})[name][historic];
              way(around:{OsmRadiusMeters},{center.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{center.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})[name][tourism];
            );
            out center {OsmMaxResults};
            """;

        try
        {
            var client = _httpClientFactory.CreateClient("overpass");
            using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
            using var response = await client.PostAsync("", content, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var results = new List<NearbyPoi>();
            if (!doc.RootElement.TryGetProperty("elements", out var elements))
                return results;

            foreach (var el in elements.EnumerateArray())
            {
                var tags = el.TryGetProperty("tags", out var t) ? t : default;
                var name = tags.ValueKind != JsonValueKind.Undefined && tags.TryGetProperty("name", out var n)
                    ? n.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var type = DetermineOsmType(tags);
                double lat = center.Latitude;
                double lon = center.Longitude;
                if (el.TryGetProperty("lat", out var latProp))
                    lat = latProp.GetDouble();
                else if (el.TryGetProperty("center", out var centerEl) && centerEl.TryGetProperty("lat", out var cLat))
                    lat = cLat.GetDouble();

                if (el.TryGetProperty("lon", out var lonProp))
                    lon = lonProp.GetDouble();
                else if (el.TryGetProperty("center", out var centerEl2) && centerEl2.TryGetProperty("lon", out var cLon))
                    lon = cLon.GetDouble();

                results.Add(new NearbyPoi(name, type, HaversineMeters(center, lat, lon)));
            }

            return results.OrderBy(p => p.DistanceMeters).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("OSM Overpass query failed: {Message}", ex.Message);
            return [];
        }
    }

    private async Task<IReadOnlyList<WikidataFact>> FetchWikidataFactsAsync(
        GpsPoint center, string language, CancellationToken ct)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "fr" : language.ToLowerInvariant();
        var lat = center.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = center.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // SPARQL : éléments Wikidata dans le rayon géographique.
        var sparql = $"""
            SELECT ?item ?itemLabel ?itemDescription WHERE {{
              SERVICE wikibase:around {{
                ?item wdt:P625 ?location .
                bd:serviceParam wikibase:center "Point({lon} {lat})"^^geo:wktLiteral .
                bd:serviceParam wikibase:radius "{WikidataRadiusKm}" .
              }}
              SERVICE wikibase:label {{ bd:serviceParam wikibase:language "{lang},en" . }}
            }}
            LIMIT {WikidataMaxResults}
            """;

        try
        {
            var client = _httpClientFactory.CreateClient("wikidata");
            var url = "?query=" + HttpUtility.UrlEncode(sparql) + "&format=json";
            using var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var results = new List<WikidataFact>();
            if (!doc.RootElement.TryGetProperty("results", out var resultsEl) ||
                !resultsEl.TryGetProperty("bindings", out var bindings))
                return results;

            foreach (var binding in bindings.EnumerateArray())
            {
                var id = binding.TryGetProperty("item", out var item)
                    ? item.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "" : "";
                var label = binding.TryGetProperty("itemLabel", out var lbl)
                    ? lbl.TryGetProperty("value", out var lv) ? lv.GetString() ?? "" : "" : "";
                var desc = binding.TryGetProperty("itemDescription", out var d)
                    ? d.TryGetProperty("value", out var dv) ? dv.GetString() : null : null;

                if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(id))
                    continue;

                // Extraire l'identifiant court (ex: Q12345) depuis l'URI
                var wikidataId = id.Contains('/') ? id[(id.LastIndexOf('/') + 1)..] : id;

                results.Add(new WikidataFact(label, desc, wikidataId));
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Wikidata SPARQL query failed: {Message}", ex.Message);
            return [];
        }
    }

    private static string DetermineOsmType(JsonElement tags)
    {
        if (tags.ValueKind == JsonValueKind.Undefined) return "poi";
        if (tags.TryGetProperty("historic", out _)) return "historic";
        if (tags.TryGetProperty("tourism", out _)) return "tourism";
        if (tags.TryGetProperty("leisure", out _)) return "leisure";
        if (tags.TryGetProperty("natural", out _)) return "natural";
        return "poi";
    }

    private static double HaversineMeters(GpsPoint origin, double lat2, double lon2)
    {
        const double R = 6_371_000;
        var dLat = (lat2 - origin.Latitude) * Math.PI / 180;
        var dLon = (lon2 - origin.Longitude) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(origin.Latitude * Math.PI / 180)
              * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
