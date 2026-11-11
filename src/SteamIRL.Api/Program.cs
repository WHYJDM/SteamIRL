using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Accounts;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;
using SteamIRL.Api.Features.Moderation;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Serialization;
using SteamIRL.Api.Features.Activities;
using SteamIRL.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddScoped<SessionQueries>();
builder.Services.AddScoped<SessionCommands>();
builder.Services.AddScoped<IdempotentCommands>();
builder.Services.AddScoped<ModerationReads>();
builder.Services.AddScoped<SubmissionCommands>();
builder.Services.AddScoped<ModerationCommands>();
builder.Services.AddScoped<IAuthorizationHandler, ModeratorAccess>();

builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Database")
        ?? throw new InvalidOperationException("Set ConnectionStrings__Database before starting the API.");
    options.UseNpgsql(connectionString);
});
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
var insecureCookies = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") || builder.Configuration.GetValue<bool>("Authentication:AllowInsecureCookies");
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SteamIRL.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = insecureCookies ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options => options.AddPolicy("Moderator", policy => policy.RequireAuthenticatedUser().AddRequirements(new ModeratorRequirement())));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "SteamIRL.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = insecureCookies ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddDataProtection().SetApplicationName("SteamIRL").PersistKeysToFileSystem(new DirectoryInfo(
    builder.Configuration["DataProtection:KeyPath"] ?? Path.Combine(builder.Environment.ContentRootPath, ".local", "keys")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Explicit bootstrap command: the normal web process never changes the schema.
if (args.Contains("--bootstrap-database", StringComparer.Ordinal))
{
    await DatabaseAccess.Bootstrap(builder.Configuration.GetConnectionString("Database")!, builder.Configuration);
    return;
}
if (args.Contains("--grant-database-access", StringComparer.Ordinal))
{
    await DatabaseAccess.Grant(builder.Configuration.GetConnectionString("Database")!, builder.Configuration);
    return;
}
if (args.Contains("--seed-demo", StringComparer.Ordinal))
{
    await DemoData.Seed(app.Services, app.Environment, app.Configuration);
    return;
}
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
var grantIndex = Array.IndexOf(args, "--grant-moderator");
var revokeIndex = Array.IndexOf(args, "--revoke-moderator");
if (grantIndex >= 0 || revokeIndex >= 0)
{
    var index = grantIndex >= 0 ? grantIndex : revokeIndex;
    if (grantIndex >= 0 && revokeIndex >= 0 || index + 1 >= args.Length || !Guid.TryParse(args[index + 1], out var userId))
        throw new InvalidOperationException("Use --grant-moderator <account-id> or --revoke-moderator <account-id>.");
    await ModeratorAccess.ChangeRole(app.Services, userId, grantIndex >= 0);
    return;
}

// Test hosts migrate disposable databases with their own fixture login. Real web
// processes, including Development, must never use that administrative identity.
if (!app.Configuration.GetValue<bool>("Testing:SkipDatabaseRoleCheck"))
    await DatabaseAccess.VerifyApplication(builder.Configuration.GetConnectionString("Database")!);

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });
app.MapActivities();
app.MapAccounts();
app.MapLocations();
app.MapSessions();
app.MapModeration();
app.Run();

public partial class Program;
