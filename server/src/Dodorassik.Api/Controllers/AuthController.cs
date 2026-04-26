using Dodorassik.Api.Dtos;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public AuthController(AppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || req.Password.Length < 8)
            return BadRequest(new { error = "invalid_input" });

        var exists = await _db.Users.AnyAsync(u => u.Email == req.Email);
        if (exists) return Conflict(new { error = "email_taken" });

        var user = new User
        {
            Email = req.Email.Trim().ToLowerInvariant(),
            DisplayName = req.DisplayName.Trim(),
            PasswordHash = _hasher.Hash(req.Password),
            Role = UserRole.Player,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse(_jwt.Issue(user), ToDto(user)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !_hasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "invalid_credentials" });

        user.LastLoginUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse(_jwt.Issue(user), ToDto(user)));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Role.ToString().ToLowerInvariant());
}
