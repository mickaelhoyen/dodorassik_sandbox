namespace Dodorassik.Core.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Player;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginUtc { get; set; }

    public Guid? FamilyId { get; set; }
    public Family? Family { get; set; }

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    /// <summary>
    /// Nombre de générations C3 consommées (toutes périodes confondues).
    /// Utilisé pour le quota d'essai gratuit (ex. 2 générations offertes).
    /// </summary>
    public int AiGenerationsUsed { get; set; } = 0;
}
