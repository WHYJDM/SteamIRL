using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Accounts;

namespace SteamIRL.Api.Features.Moderation;

public sealed class ModeratorRequirement : IAuthorizationRequirement;
public sealed class ModeratorAccess(AppDbContext db) : AuthorizationHandler<ModeratorRequirement>
{
    public const string RoleName = "Moderator";
    public static readonly Guid RoleId = Guid.Parse("dddd1111-1111-4111-8111-111111111111");
    public static Task<bool> HasRole(AppDbContext db, Guid id, CancellationToken ct = default) => db.UserRoles
        .AnyAsync(link => link.UserId == id && db.Roles.Any(role => role.Id == link.RoleId && role.NormalizedName == "MODERATOR"), ct);
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ModeratorRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            && await HasRole(db, id, (context.Resource as HttpContext)?.RequestAborted ?? default)) context.Succeed(requirement);
    }

    public static async Task ChangeRole(IServiceProvider services, Guid userId, bool grant)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={userId} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Account not found. Use an existing account ID.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var hasRole = await users.IsInRoleAsync(user, RoleName);
        if (hasRole != grant)
        {
            var result = grant ? await users.AddToRoleAsync(user, RoleName) : await users.RemoveFromRoleAsync(user, RoleName);
            if (!result.Succeeded) throw new InvalidOperationException("Role update failed.");
        }
        await transaction.CommitAsync();
    }
}
