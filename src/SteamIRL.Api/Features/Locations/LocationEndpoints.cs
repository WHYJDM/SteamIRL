using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;

namespace SteamIRL.Api.Features.Locations;

public static class LocationEndpoints
{
    public static void MapLocations(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/locations", List).WithName("GetLocations");
        endpoints.MapGet("/api/locations/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var item = await Project(db.Locations.AsNoTracking().Where(location => location.Id == id
                && location.ApprovalStatus == ApprovalStatus.Approved)).SingleOrDefaultAsync(ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).WithName("GetLocation");
    }

    private static async Task<IResult> List([AsParameters] LocationQuery query, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Page < 1 || query.Page > 100000) errors["page"] = ["Страница должна быть от 1 до 100000."];
        if (query.PageSize < 1 || query.PageSize > 100) errors["pageSize"] = ["Размер страницы должен быть от 1 до 100."];
        var bounds = new[] { query.MinLat, query.MinLon, query.MaxLat, query.MaxLon };
        if (bounds.Any(value => value.HasValue) && bounds.Any(value => !value.HasValue))
            errors["bounds"] = ["Передай все четыре границы области."];
        if (query.MinLat is < -90 or > 90 || query.MaxLat is < -90 or > 90 || query.MinLon is < -180 or > 180 || query.MaxLon is < -180 or > 180
            || query.MinLat > query.MaxLat || query.MinLon > query.MaxLon) errors["bounds"] = ["Некорректные границы области."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (query.ActivityId.HasValue && !await db.Activities.AnyAsync(activity => activity.Id == query.ActivityId.Value, ct))
            return Results.NotFound();

        var locations = db.Locations.AsNoTracking().Where(location => location.ApprovalStatus == ApprovalStatus.Approved);
        if (query.ActivityId.HasValue) locations = locations.Where(location => location.Activities.Any(link => link.ActivityId == query.ActivityId.Value && link.IsEnabled));
        if (query.MinLat.HasValue) locations = locations.Where(location => location.Latitude >= query.MinLat && location.Latitude <= query.MaxLat
            && location.Longitude >= query.MinLon && location.Longitude <= query.MaxLon);
        var page = await Project(locations.OrderBy(location => location.Name).ThenBy(location => location.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize + 1)).ToListAsync(ct);
        return Results.Ok(new LocationPage(page.Take(query.PageSize).ToArray(), query.Page, query.PageSize, page.Count > query.PageSize));
    }

    private static IQueryable<LocationResponse> Project(IQueryable<Location> query) => query.Select(location => new LocationResponse(
        location.Id, location.Name, location.Description, location.Address, location.Latitude, location.Longitude, location.UpdatedAt,
        location.SourceUrl, location.SourceCheckedAt, location.CoordinateNote, location.AccessNotes,
        location.Activities.Where(link => link.IsEnabled).OrderBy(link => link.Activity.Slug)
            .Select(link => new LocationActivityResponse(link.ActivityId, link.Activity.Slug, link.Activity.Name)).ToArray(), location.Revision));
}

public sealed record LocationQuery(Guid? ActivityId = null, decimal? MinLat = null, decimal? MaxLat = null,
    decimal? MinLon = null, decimal? MaxLon = null, int Page = 1, int PageSize = 50);
public sealed record LocationPage(LocationResponse[] Items, int Page, int PageSize, bool HasMore);
public sealed record LocationActivityResponse(Guid Id, string Slug, string Name);
public sealed record LocationResponse(Guid Id, string Name, string Description, string Address, decimal Latitude, decimal Longitude,
    DateTimeOffset UpdatedAt, string SourceUrl, DateTimeOffset SourceCheckedAt, string CoordinateNote, string AccessNotes, LocationActivityResponse[] Activities, long Revision = 1);
