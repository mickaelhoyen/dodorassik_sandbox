using System.ComponentModel.DataAnnotations;
using Dodorassik.Api.Validation;

namespace Dodorassik.Api.Dtos;

public record RegisterRequest(
    [property: Required, EmailAddress, StringLength(InputLimits.EmailMaxLength)]
    string Email,
    [property: Required, StringLength(InputLimits.PasswordMaxLength, MinimumLength = InputLimits.PasswordMinLength)]
    string Password,
    [property: Required, StringLength(InputLimits.DisplayNameMaxLength, MinimumLength = InputLimits.DisplayNameMinLength)]
    string DisplayName);

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
