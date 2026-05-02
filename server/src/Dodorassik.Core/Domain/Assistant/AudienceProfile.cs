namespace Dodorassik.Core.Domain.Assistant;

public enum MobilityLevel
{
    Wheelchair = 0,
    Standard = 1,
    Athletic = 2,
}

public record AudienceProfile(
    int AgeMin,
    int AgeMax,
    int GroupSize,
    MobilityLevel Mobility,
    int DurationMinutes,
    string Language);
