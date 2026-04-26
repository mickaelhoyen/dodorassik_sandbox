using Dodorassik.Api.Auth;
using Dodorassik.Api.Dtos;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly ILogger<AuthController> _log;

    public AuthController(AppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt, ILogger<AuthController> log)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _log = log;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "invalid_input" });

        var email = req.Email.Trim().ToLowerInvariant();

        // SuperAdmin cannot self-register — must be promoted by another SuperAdmin.
        var requestedRoleStr = (req.Role ?? "player").Trim().ToLowerInvariant();
        if (requestedRoleStr is not ("player" or "creator"))
            return BadRequest(new { error = "invalid_role" });
        var requestedRole = requestedRoleStr == "creator" ? UserRole.Creator : UserRole.Player;

        // Don't disclose whether the email exists: same delay regardless.
        var exists = await _db.Users.AnyAsync(u => u.Email == email);
        if (exists)
        {
            _log.LogInformation("Register rejected (duplicate) for hashed-email {EmailHash}", HashEmailForLog(email));
            return Conflict(new { error = "email_taken" });
        }

        var user = new User
        {
            Email = email,
            DisplayName = req.DisplayName.Trim(),
            PasswordHash = _hasher.Hash(req.Password),
            Role = requestedRole,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _log.LogInformation("New user registered {UserId}", user.Id);
        return Ok(new AuthResponse(_jwt.Issue(user), ToDto(user)));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid) return Unauthorized(new { error = "invalid_credentials" });

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Always run the password check so failure timing doesn't leak whether
        // the email exists (constant-ish time path).
        var dummyHash = "v1$100000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        var hash = user?.PasswordHash ?? dummyHash;
        var ok = _hasher.Verify(req.Password, hash);

        if (user is null || !ok)
        {
            _log.LogInformation("Login failed for hashed-email {EmailHash}", HashEmailForLog(email));
            return Unauthorized(new { error = "invalid_credentials" });
        }

        user.LastLoginUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _log.LogInformation("Login OK {UserId}", user.Id);
        return Ok(new AuthResponse(_jwt.Issue(user), ToDto(user)));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, JwtTokenService.RoleToSnake(u.Role), u.FamilyId);

    /// <summary>
    /// Hashes the email so it can appear in logs without being PII. The hash
    /// uses no per-user salt: callers must still treat it as low-cardinality
    /// and never publish hash sets externally.
    /// </summary>
    private static string HashEmailForLog(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(email));
        return Convert.ToHexString(bytes, 0, 4);
    }
}
