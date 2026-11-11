using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Features.Accounts;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;

namespace SteamIRL.Api.Data;

public static class DemoData
{
    public static async Task Seed(IServiceProvider services, IHostEnvironment environment, IConfiguration config)
    {
        if (!environment.IsDevelopment() || !config.GetValue<bool>("Demo:AllowSeed"))
            throw new InvalidOperationException("Demo seeding requires Development and Demo__AllowSeed=true on a local demonstration database.");
        var password = config["Demo:Password"] ?? throw new InvalidOperationException("Set a generated Demo__Password outside Git.");
        if (password.Length < 20) throw new InvalidOperationException("Demo password must contain at least 20 characters.");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var database = db.Database.GetDbConnection().Database;
        if (!database.EndsWith("_demo", StringComparison.Ordinal) && !database.StartsWith("steamirl_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Demo records may only be written to a database ending in _demo or starting with steamirl_test.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(817031145)");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = new[] { Guid.Parse("eeee1111-1111-4111-8111-111111111111"), Guid.Parse("eeee2222-2222-4222-8222-222222222222") };
        for (var i = 0; i < ids.Length; i++)
        {
            var email = $"demo-{(i == 0 ? "organiser" : "player")}@example.invalid";
            var user = await users.FindByIdAsync(ids[i].ToString());
            if (user is null)
            {
                var created = await users.CreateAsync(new ApplicationUser { Id = ids[i], UserName = email, Email = email,
                    Profile = new PublicProfile { UserId = ids[i], DisplayName = i == 0 ? "[ДЕМО] Организатор" : "[ДЕМО] Игрок" } }, password);
                if (!created.Succeeded) throw new InvalidOperationException("Demo user creation failed. Check password policy and existing demo identifiers.");
            }
        }
        var gameId = Guid.Parse("eeee3333-3333-4333-8333-333333333333");
        if (!await db.Sessions.AnyAsync(game => game.Id == gameId))
        {
            var now = await db.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
            db.Sessions.Add(new GameSession { Id = gameId, OrganiserId = ids[0], LocationId = LocationSeed.WhiteBridgeId,
                ActivityId = Guid.Parse("11111111-1111-4111-8111-111111111111"), Kind = SessionKind.Planned, SkillLevel = SkillLevel.Intermediate,
                EquipmentStatus = EquipmentStatus.BallAvailable, Capacity = 2, StartsAt = now.AddDays(1), EndsAt = now.AddDays(1).AddHours(2), CreatedAt = now, UpdatedAt = now,
                Participations = [new Participation { UserId = ids[1], JoinedAt = now }] });
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }
}
