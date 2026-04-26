using System.ComponentModel.DataAnnotations;
using Dodorassik.Api.Validation;

namespace Dodorassik.Api.Dtos;

public record RegisterRequest(
    [property: Required, EmailAddress, StringLength(InputLimits.EmailMaxLength)]
    string Email,
    [property: Required, StringLength(InputLimits.PasswordMaxLength, MinimumLength = InputLimits.PasswordMinLength)]
    string Password,
    [property: Required, StringLength(InputLimits.DisplayNameMaxLength, MinimumLength = InputLimits.DisplayNameMinLength)]
    string DisplayName,
    // Accepted values: "player" (default) or "creator". SuperAdmin cannot self-register.
    [property: StringLength(32)]
    string? Role = null);

public record LoginRequest(
    [property: Required, EmailAddress, StringLength(InputLimits.EmailMaxLength)]
    string Email,
    [property: Required, StringLength(InputLimits.PasswordMaxLength)]
    string Password);

public record AuthResponse(string Token, UserDto User);

public record UserDto(Guid Id, string Email, string DisplayName, string Role, Guid? FamilyId);

public record UpdateProfileRequest(
    [property: StringLength(InputLimits.DisplayNameMaxLength, MinimumLength = InputLimits.DisplayNameMinLength)]
    string? DisplayName,
    [property: EmailAddress, StringLength(InputLimits.EmailMaxLength)]
    string? Email);
