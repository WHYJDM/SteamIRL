using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;

namespace SteamIRL.Api.Features.Activities;

public static class ActivityEndpoints
{
    public static void MapActivities(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/activities", async (AppDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await db.Activities.AsNoTracking()
                .OrderBy(activity => activity.Slug)
                .Select(activity => new ActivityResponse(activity.Id, activity.Slug, activity.Name, activity.Description))
                .ToListAsync(cancellationToken)))
            .WithName("GetActivities");
    }
}

public sealed record ActivityResponse(Guid Id, string Slug, string Name, string Description);
