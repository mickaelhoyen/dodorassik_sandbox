using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Dodorassik.Api.Auth;

public class JwtSettings
{
    public string Issuer { get; set; } = "dodorassik";
    public string Audience { get; set; } = "dodorassik-clients";
    public string Secret { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 60 * 24 * 7; // 1 week
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings;
        if (string.IsNullOrWhiteSpace(_settings.Secret) || _settings.Secret.Length < 32)
            throw new InvalidOperationException("Jwt secret must be at least 32 characters.");
    }

    public string Issue(User user)
    {
        var roleSnake = RoleToSnake(user.Role);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("display_name", user.DisplayName),
            new Claim("role", roleSnake),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.LifetimeMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string RoleToSnake(UserRole role) => role switch
    {
        UserRole.Player => "player",
        UserRole.Creator => "creator",
        UserRole.SuperAdmin => "super_admin",
        _ => "player",
    };
}
