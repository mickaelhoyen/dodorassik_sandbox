namespace Dodorassik.Api.Validation;

/// <summary>
/// Centralised input bounds shared by DTO validation. Tweaking limits in one
/// place reduces the chance of inconsistent rules between endpoints (which
/// historically helps both DoS resistance and clearer error messages).
/// </summary>
public static class InputLimits
{
    public const int EmailMaxLength = 256;
    public const int DisplayNameMinLength = 1;
    public const int DisplayNameMaxLength = 64;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public const int HuntNameMaxLength = 256;
    public const int HuntDescriptionMaxLength = 4_000;
    public const int StepTitleMaxLength = 256;
    public const int StepDescriptionMaxLength = 4_000;
    public const int StepsPerHuntMax = 100;

    public const int JsonParamsMaxBytes = 8_192;
    public const int JsonPayloadMaxBytes = 8_192;

    public const int ClueCodeMaxLength = 64;
    public const int ClueTitleMaxLength = 256;
    public const int ClueRevealMaxLength = 4_000;
    public const int CluesPerHuntMax = 200;

    // Game Design Assistant — C1 ContextBuilder
    public const int PhotosPerContextMax = 5;
    public const int PhotoMaxBytes = 2 * 1024 * 1024;       // 2 Mo par photo
    public const int SponsorsPerHuntMax = 10;
    public const long GenerateContextMaxBytes = 12 * 1024 * 1024; // 5 photos + overhead
}
