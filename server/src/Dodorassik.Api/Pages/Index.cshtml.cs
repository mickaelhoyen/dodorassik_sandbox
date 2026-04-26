using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public List<PublicHuntView> Events { get; private set; } = [];
    public List<PublicHuntView> Permanent { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var all = await _db.Hunts
                .Where(h => h.Status == HuntStatus.Published)
                .Select(h => new PublicHuntView(
                    h.Name,
                    h.Description,
                    h.LocationLabel,
                    h.Steps.Count,
                    h.Category,
                    h.EventStartUtc,
                    h.EventEndUtc))
                .ToListAsync();

            Permanent = all.Where(h => h.Category == HuntCategory.Permanent).ToList();
            Events = all
                .Where(h => h.Category == HuntCategory.Event)
                .Where(h => (h.EventStartUtc == null || h.EventStartUtc <= now)
                         && (h.EventEndUtc == null || h.EventEndUtc >= now))
                .OrderBy(h => h.EventEndUtc)
                .ToList();
        }
        catch
        {
            ErrorMessage = "Impossible de charger les chasses pour l'instant. Réessayez plus tard.";
        }
    }
}

public record PublicHuntView(
    string Name,
    string Description,
    string? LocationLabel,
    int StepCount,
    HuntCategory Category,
    DateTime? EventStartUtc,
    DateTime? EventEndUtc);
