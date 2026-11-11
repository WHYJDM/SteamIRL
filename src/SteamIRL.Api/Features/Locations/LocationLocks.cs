using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;

namespace SteamIRL.Api.Features.Locations;

public static class LocationLocks
{
    // Called inside the actor's command transaction. Shared readers cannot race
    // a moderation update. A command locks location BEFORE any session/case row.
    public static Task<Location?> Share(AppDbContext db, Guid id, CancellationToken ct) => db.Locations
        .FromSqlInterpolated($"SELECT * FROM locations WHERE \"Id\"={id} FOR SHARE").SingleOrDefaultAsync(ct);
    public static Task<Location?> Update(AppDbContext db, Guid id, CancellationToken ct) => db.Locations
        .FromSqlInterpolated($"SELECT * FROM locations WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct);
}
