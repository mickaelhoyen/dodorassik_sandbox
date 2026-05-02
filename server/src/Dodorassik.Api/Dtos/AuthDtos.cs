using System.ComponentModel.DataAnnotations;
using Dodorassik.Api.Validation;

namespace Dodorassik.Api.Dtos;

public record RegisterRequest(
    [Required, EmailAddress, StringLength(InputLimits.EmailMaxLength)]
    string Email,
    [Required, StringLength(InputLimits.PasswordMaxLength, MinimumLength = InputLimits.PasswordMinLength)]
    string Password,
    [Required, StringLength(InputLimits.DisplayNameMaxLength, MinimumLength = InputLimits.DisplayNameMinLength)]
    string DisplayName,
    // Accepted values: "player" (default) or "creator". SuperAdmin cannot self-register.
    [StringLength(32)]
    string? Role = null);

public record LoginRequest(
    [Required, EmailAddress, StringLength(InputLimits.EmailMaxLength)]
    string Email,
    [Required, StringLength(InputLimits.PasswordMaxLength)]
    string Password);

public record AuthResponse(string Token, UserDto User);

public record UserDto(Guid Id, string Email, string DisplayName, string Role, Guid? FamilyId, string Tier);

public record UpdateProfileRequest(
    [StringLength(InputLimits.DisplayNameMaxLength, MinimumLength = InputLimits.DisplayNameMinLength)]
    string? DisplayName,
    [EmailAddress, StringLength(InputLimits.EmailMaxLength)]
    string? Email);
