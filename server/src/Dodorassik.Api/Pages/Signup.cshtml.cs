using System.ComponentModel.DataAnnotations;
using Dodorassik.Api.Validation;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Pages;

public class SignupModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;

    public SignupModel(AppDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty]
    public SignupInput Input { get; set; } = new();

    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet() => Page();

    // The Razor signup must obey the same throttling as POST /api/auth/register
    // (3 / hour / IP). Without this, an attacker could bypass the API limiter
    // just by hitting /Signup. See docs/SECURITY.md §4.
    [EnableRateLimiting("auth-register")]
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Input.Password != Input.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(Input.ConfirmPassword), "Les mots de passe ne correspondent pas.");
            return Page();
        }

        var allowedRoles = new[] { "player", "creator" };
        var role = (Input.Role ?? "player").Trim().ToLowerInvariant();
        if (!allowedRoles.Contains(role))
        {
            ModelState.AddModelError(nameof(Input.Role), "Rôle invalide.");
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == email);
        if (exists)
        {
            ErrorMessage = "Cette adresse e-mail est déjà utilisée.";
            return Page();
        }

        var user = new User
        {
            Email = email,
            DisplayName = Input.DisplayName.Trim(),
            PasswordHash = _hasher.Hash(Input.Password),
            Role = role == "creator" ? UserRole.Creator : UserRole.Player,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        SuccessMessage = "Compte créé avec succès ! Vous pouvez maintenant vous connecter depuis l'application Dodorassik.";
        return Page();
    }
}

public class SignupInput
{
    [Required(ErrorMessage = "Le nom d'affichage est requis.")]
    [StringLength(InputLimits.DisplayNameMaxLength, MinimumLength = InputLimits.DisplayNameMinLength,
        ErrorMessage = "Entre 1 et 64 caractères.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'e-mail est requis.")]
    [EmailAddress(ErrorMessage = "Adresse e-mail invalide.")]
    [StringLength(InputLimits.EmailMaxLength, ErrorMessage = "E-mail trop long.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis.")]
    [StringLength(InputLimits.PasswordMaxLength, MinimumLength = InputLimits.PasswordMinLength,
        ErrorMessage = "Entre 8 et 128 caractères.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation est requise.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? Role { get; set; } = "creator";
}
