using System.Net.Http.Json;
using Dodorassik.Core.Abstractions;
using Dodorassik.Core.Domain;
using Dodorassik.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Dodorassik.Api.Tests;

/// <summary>
/// Shared test fixture helpers. Each test class instantiates its own
/// <see cref="TestingWebAppFactory"/> so the InMemory DB is isolated;
/// these helpers deal with the boilerplate of getting an authenticated
/// client at the requested role.
/// </summary>
internal static class TestUserHelper
{
    public const string DefaultPassword = "correcthorse42";

    public static async Task<(Guid Id, string Token)> RegisterAsync(
        HttpClient client,
        string email,
        string? role = null,
        string? displayName = null)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = DefaultPassword,
            displayName = displayName ?? email.Split('@')[0],
            role,
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuthResponseBody>();
        return (body!.User.Id, body.Token);
    }

    /// <summary>
    /// Registers (always as player via the API), then directly promotes
    /// to <paramref name="targetRole"/> through the DI container — the
    /// public surface forbids self-promotion to SuperAdmin.
    /// </summary>
    public static async Task<(Guid Id, string Token)> RegisterAndPromoteAsync(
        TestingWebAppFactory factory,
        string email,
        UserRole targetRole)
    {
        var client = factory.CreateClient();
        var (id, token) = await RegisterAsync(client, email);
        if (targetRole == UserRole.Player) return (id, token);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = db.Users.Single(u => u.Id == id);
        user.Role = targetRole;
        db.SaveChanges();
        return (user.Id, jwt.Issue(user));
    }

    public static HttpClient AuthClient(this TestingWebAppFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

internal record AuthResponseBody(string Token, AuthUserBody User);
internal record AuthUserBody(Guid Id, string Email, string DisplayName, string Role, Guid? FamilyId);
