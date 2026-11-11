using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SteamIRL.Api.Data;

namespace SteamIRL.Api.Features.Accounts;

public static class AccountEndpoints
{
    public static void MapAccounts(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").AddEndpointFilter<CsrfFilter>();
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
        });
        auth.MapGet("/session", async (HttpContext context, AppDbContext db) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = Guid.TryParse(id, out var userId) ? await Me(db, userId, context.RequestAborted) : null;
            return Results.Ok(new { user });
        });
        auth.MapPost("/register", Register).RequireRateLimiting("auth");
        auth.MapPost("/login", Login).RequireRateLimiting("auth");
        auth.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        var me = endpoints.MapGroup("/api/me").RequireAuthorization().AddEndpointFilter<CsrfFilter>();
        me.MapGet("", async (HttpContext context, AppDbContext db) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var profile = await Me(db, UserId(context.User), context.RequestAborted);
            return profile is null ? Results.Unauthorized() : Results.Ok(profile);
        });
        me.MapPatch("/profile", UpdateProfile);
        endpoints.MapGet("/api/profiles/{userId:guid}", async (Guid userId, AppDbContext db, CancellationToken ct) =>
        {
            var profile = await db.PublicProfiles.AsNoTracking().Where(p => p.UserId == userId)
                .Select(p => new PublicProfileResponse(p.UserId, p.DisplayName)).SingleOrDefaultAsync(ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });
    }

    private static async Task<IResult> Register(RegisterRequest request, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager, AppDbContext db, HttpContext context)
    {
        var email = request.Email?.Trim() ?? "";
        var displayName = request.DisplayName?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)) errors["email"] = ["Укажи корректный email."];
        if (!ValidDisplayName(displayName)) errors["displayName"] = ["Псевдоним должен содержать от 2 до 60 символов без управляющих знаков."];
        if (!ValidPassword(request.Password)) errors["password"] = ["Пароль должен содержать от 10 до 128 символов, заглавную и строчную латинские буквы, цифру."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (await users.FindByEmailAsync(email) is not null) return DuplicateEmail();

        var id = Guid.NewGuid();
        var user = new ApplicationUser { Id = id, UserName = id.ToString("N"), Email = email,
            Profile = new PublicProfile { UserId = id, DisplayName = displayName } };
        IdentityResult result;
        try { result = await users.CreateAsync(user, request.Password!); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return DuplicateEmail();
        }
        if (!result.Succeeded && result.Errors.Any(error => error.Code == "DuplicateEmail")) return DuplicateEmail();
        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Не удалось создать аккаунт. Проверь данные."] });
        await signInManager.SignInAsync(user, isPersistent: true);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Created($"/api/profiles/{id}", await Me(db, id, context.RequestAborted));
    }

    private static async Task<IResult> Login(LoginRequest request, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager, AppDbContext db, HttpContext context,
        IPasswordHasher<ApplicationUser> passwordHasher)
    {
        const string failure = "Не удалось войти. Проверь email и пароль или попробуй позже.";
        var email = request.Email?.Trim() ?? "";
        if (email.Length > 254 || request.Password is null || request.Password.Length > 128)
            return Results.Problem(statusCode: 401, title: failure);
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            // Spend a password verification even for unknown accounts.
            passwordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, request.Password);
            return Results.Problem(statusCode: 401, title: failure);
        }
        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded) return Results.Problem(statusCode: 401, title: failure);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await Me(db, user.Id, context.RequestAborted));
    }

    private static async Task<IResult> UpdateProfile(UpdateProfileRequest request, HttpContext context, AppDbContext db)
    {
        var displayName = request.DisplayName?.Trim() ?? "";
        if (!ValidDisplayName(displayName)) return Results.ValidationProblem(new Dictionary<string, string[]>
            { ["displayName"] = ["Псевдоним должен содержать от 2 до 60 символов без управляющих знаков."] });
        var profile = await db.PublicProfiles.SingleOrDefaultAsync(p => p.UserId == UserId(context.User), context.RequestAborted);
        if (profile is null) return Results.Unauthorized();
        profile.DisplayName = displayName;
        await db.SaveChangesAsync(context.RequestAborted);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await Me(db, profile.UserId, context.RequestAborted));
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static bool ValidDisplayName(string name) => name.Length is >= 2 and <= 60 && !name.Any(char.IsControl);
    private static bool ValidPassword(string? password) => password is { Length: >= 10 and <= 128 }
        && password.Any(c => c is >= 'A' and <= 'Z') && password.Any(c => c is >= 'a' and <= 'z') && password.Any(c => c is >= '0' and <= '9');
    private static IResult DuplicateEmail() => Results.Problem(statusCode: 409, title: "Этот email уже зарегистрирован. Войди в аккаунт.");
    private static Task<MeResponse?> Me(AppDbContext db, Guid id, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(user => user.Id == id).Select(user => new MeResponse(user.Id, user.Email!, user.Profile.DisplayName,
            db.UserRoles.Any(link => link.UserId == user.Id && db.Roles.Any(role => role.Id == link.RoleId && role.NormalizedName == "MODERATOR")))).SingleOrDefaultAsync(ct);
    private static readonly Lazy<string> DummyPasswordHash = new(() => new PasswordHasher<ApplicationUser>()
        .HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));
}

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);
public sealed record LoginRequest(string? Email, string? Password);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateProfileRequest(string? DisplayName);
public sealed record MeResponse(Guid Id, string Email, string DisplayName, bool IsModerator = false);
public sealed record PublicProfileResponse(Guid Id, string DisplayName);
