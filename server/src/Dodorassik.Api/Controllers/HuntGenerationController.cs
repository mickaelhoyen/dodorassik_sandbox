using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Dodorassik.Api.Dtos;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dodorassik.Api.Controllers;

[ApiController]
[Route("api/hunts/generate")]
[Authorize(Roles = "creator,super_admin")]
public class HuntGenerationController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// C1 — Enrichit les données brutes (GPS + photos + profil public + sponsors)
    /// en un HuntContext structuré exploitable par C2 (RAG) et C3 (Claude).
    /// </summary>
    [HttpPost("context")]
    [EnableRateLimiting("generate-context")]
    [RequestSizeLimit(InputLimits.GenerateContextMaxBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<HuntContextDto>> BuildContext(
        [FromForm] BuildContextFormDto form,
        [FromServices] IContextBuilderService contextBuilder,
        CancellationToken ct)
    {
        // Désérialiser les champs JSON embarqués dans le formulaire multipart.
        AudienceProfileDto? audience;
        try
        {
            audience = JsonSerializer.Deserialize<AudienceProfileDto>(
                form.AudienceProfileJson, JsonOpts);
        }
        catch
        {
            return BadRequest("audienceProfileJson invalide.");
        }

        if (audience is null)
            return BadRequest("audienceProfileJson manquant.");

        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(audience, new ValidationContext(audience), validationResults, true))
            return BadRequest(validationResults.Select(r => r.ErrorMessage));

        if (audience.AgeMin > audience.AgeMax)
            return BadRequest("ageMin doit être ≤ ageMax.");

        IReadOnlyList<GpsPointDto> gpsPoints = [];
        if (!string.IsNullOrWhiteSpace(form.GpsPointsJson))
        {
            try
            {
                gpsPoints = JsonSerializer.Deserialize<List<GpsPointDto>>(form.GpsPointsJson, JsonOpts) ?? [];
            }
            catch
            {
                return BadRequest("gpsPointsJson invalide.");
            }
        }

        IReadOnlyList<SponsorConstraintDto> sponsors = [];
        if (!string.IsNullOrWhiteSpace(form.SponsorsJson))
        {
            try
            {
                sponsors = JsonSerializer.Deserialize<List<SponsorConstraintDto>>(form.SponsorsJson, JsonOpts) ?? [];
            }
            catch
            {
                return BadRequest("sponsorsJson invalide.");
            }
        }

        if (sponsors.Count > InputLimits.SponsorsPerHuntMax)
            return BadRequest($"Maximum {InputLimits.SponsorsPerHuntMax} sponsors.");

        var photos = form.Photos ?? [];
        if (photos.Count > InputLimits.PhotosPerContextMax)
            return BadRequest($"Maximum {InputLimits.PhotosPerContextMax} photos.");

        // Construire la requête domaine.
        var photoInputs = photos
            .Select(f => new PhotoInput(f.FileName, f.OpenReadStream()))
            .ToList();

        var request = new BuildContextRequest(
            Center: new GpsPoint(form.CenterLatitude, form.CenterLongitude),
            GpsPoints: gpsPoints.Select(p => new GpsPoint(p.Latitude, p.Longitude, p.Label)).ToList(),
            Audience: new AudienceProfile(
                audience.AgeMin, audience.AgeMax, audience.GroupSize,
                audience.Mobility, audience.DurationMinutes, audience.Language),
            Sponsors: sponsors.Select(s => new SponsorConstraint(
                s.Brand, s.Category, s.Constraints)).ToList(),
            Photos: photoInputs);

        HuntContext? context = null;
        try
        {
            context = await contextBuilder.BuildAsync(request, ct);
        }
        finally
        {
            // Les streams des photos sont libérés immédiatement — rien n'est persisté.
            foreach (var pi in photoInputs)
                await pi.Data.DisposeAsync();
        }

        return Ok(MapToDto(context!));
    }

    private static HuntContextDto MapToDto(HuntContext ctx) => new(
        Location: new LocationContextDto(
            ctx.Location.PlaceName,
            ctx.Location.Pois.Select(p => new NearbyPoiDto(p.Name, p.Type, p.DistanceMeters)).ToList(),
            ctx.Location.HistoricalFacts.Select(f => new WikidataFactDto(f.Label, f.Description, f.WikidataId)).ToList()),
        Audience: new AudienceProfileDto(
            ctx.Audience.AgeMin, ctx.Audience.AgeMax, ctx.Audience.GroupSize,
            ctx.Audience.Mobility, ctx.Audience.DurationMinutes, ctx.Audience.Language),
        Sponsors: ctx.Sponsors.Select(s => new SponsorConstraintDto(s.Brand, s.Category, s.Constraints)).ToList(),
        PhotoAnalyses: ctx.PhotoAnalyses.Select(p => new PhotoAnalysisResultDto(
            p.FileName, p.SceneDescription, p.DetectedElements, p.ArchitectureStyle)).ToList(),
        GpsPoints: ctx.GpsPoints.Select(g => new GpsPointDto(g.Latitude, g.Longitude, g.Label)).ToList());
}

/// <summary>Modèle de formulaire multipart pour l'endpoint C1.</summary>
public class BuildContextFormDto
{
    [Required]
    public string AudienceProfileJson { get; set; } = "";

    [Range(-90, 90)]
    public double CenterLatitude { get; set; }

    [Range(-180, 180)]
    public double CenterLongitude { get; set; }

    public string? GpsPointsJson { get; set; }
    public string? SponsorsJson { get; set; }

    public List<IFormFile>? Photos { get; set; }
}
