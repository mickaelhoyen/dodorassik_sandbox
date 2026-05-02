using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Dodorassik.Api.Auth;
using Dodorassik.Api.Hubs;
using Dodorassik.Api.Services;
using Dodorassik.Core.Abstractions;
using Dodorassik.Infrastructure.Assistant;
using Dodorassik.Infrastructure.Persistence.Seed;
using Dodorassik.Infrastructure.Persistence;
using Dodorassik.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// Database
// -----------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres")));

// -----------------------------------------------------------------------
// Auth — JWT bearer + PBKDF2 password hashing
// -----------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IAntiCheatService, AntiCheatService>();

// -----------------------------------------------------------------------
// Game Design Assistant — C1 ContextBuilder
// -----------------------------------------------------------------------
var externalApis = builder.Configuration.GetSection("ExternalApis");

builder.Services.AddHttpClient("overpass", c =>
{
    c.BaseAddress = new Uri(externalApis["OverpassUrl"] ?? "https://overpass-api.de/api/interpreter");
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Dodorassik/1.0 (game-design-assistant)");
});

builder.Services.AddHttpClient("wikidata", c =>
{
    c.BaseAddress = new Uri(externalApis["WikidataSparqlUrl"] ?? "https://query.wikidata.org/sparql");
    c.Timeout = TimeSpan.FromSeconds(8);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Dodorassik/1.0 (game-design-assistant)");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/sparql-results+json");
});

builder.Services.AddScoped<ILocationEnricher, LocationEnricher>();
builder.Services.AddScoped<IPhotoAnalyzer, StubPhotoAnalyzer>();
builder.Services.AddScoped<IContextBuilderService, ContextBuilderService>();

// -----------------------------------------------------------------------
// Game Design Assistant — C2 Knowledge RAG
// -----------------------------------------------------------------------
builder.Services.AddScoped<ITextEmbedder, StubTextEmbedder>();
builder.Services.AddScoped<IGameKnowledgeRepository, GameKnowledgeRepository>();
builder.Services.AddScoped<GameMechanicsSeeder>();

// Don't remap "sub" / "role" — we want the raw JWT claim names.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
        // SignalR WebSocket connections pass the JWT in the query string because
        // browsers (and Godot's WebSocketPeer) cannot set custom headers during
        // the HTTP upgrade handshake.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    // Pro et Enterprise ont accès à C3 (génération Claude).
    // super_admin bypasse le filtre — il gère la plateforme.
    options.AddPolicy("RequiresPro", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("tier", "pro") ||
            ctx.User.HasClaim("tier", "enterprise") ||
            ctx.User.HasClaim("role", "super_admin")));
});

// -----------------------------------------------------------------------
// CORS — explicit allowlist per environment, never AllowAnyOrigin in prod
// AllowCredentials is required for SignalR WebSocket transport.
// -----------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
    {
        // Dev convenience only — refused if Environment != Development.
        p.WithOrigins("http://localhost:5173", "http://localhost:8080", "http://127.0.0.1:5500")
         .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
    else
    {
        if (allowedOrigins.Length == 0)
            throw new InvalidOperationException("Cors:AllowedOrigins must be configured in non-Development environments.");
        p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

// -----------------------------------------------------------------------
// Rate limiting (anti-brute force, anti-DoS) — see docs/SECURITY.md §4
// -----------------------------------------------------------------------
// In Development (which includes the test factory environment), use very high
// limits so integration tests never hit the rate limiter. Production retains
// the strict values.
var isDev = builder.Environment.IsDevelopment();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("auth-login", o =>
    {
        o.PermitLimit = isDev ? 10_000 : 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("auth-register", o =>
    {
        o.PermitLimit = isDev ? 10_000 : 3;
        o.Window = TimeSpan.FromHours(1);
        o.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("submit", o =>
    {
        o.PermitLimit = isDev ? 10_000 : 30;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("generate-context", o =>
    {
        o.PermitLimit = isDev ? 10_000 : 10;
        o.Window = TimeSpan.FromHours(1);
        o.QueueLimit = 0;
    });

    if (!isDev)
    {
        // Global fallback — keyed on remote IP. Real deployments should sit
        // behind a trusted reverse proxy and use forwarded headers.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    }
});

// -----------------------------------------------------------------------
// MVC + Razor Pages + SignalR + Swagger
// -----------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.MapHub<CompetitiveHuntHub>("/hubs/competitive");

app.Run();

// Exposed so WebApplicationFactory<Program> can be used in tests.
public partial class Program;
