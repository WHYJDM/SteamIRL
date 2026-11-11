using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Accounts;
using SteamIRL.Api.Features.Locations;

namespace SteamIRL.Api.Features.Sessions;

public static class SessionEndpoints
{
    public static void MapSessions(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/sessions", List);
        endpoints.MapGet("/api/sessions/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, SessionQueries queries, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var viewer = Viewer(context);
            var visible = await db.Sessions.AnyAsync(session => session.Id == id && (session.PlaceActivity.Location.ApprovalStatus == ApprovalStatus.Approved && session.PlaceActivity.IsEnabled
                || session.OrganiserId == viewer || session.Participations.Any(p => p.UserId == viewer)), ct);
            return visible ? Results.Ok(await queries.Get(id, viewer, await queries.Now(ct), ct)) : Results.NotFound();
        });
        endpoints.MapGet("/api/me/sessions", Mine).RequireAuthorization();
        var mutations = endpoints.MapGroup("/api/sessions").RequireAuthorization().AddEndpointFilter<CsrfFilter>();
        mutations.MapPost("", async (CreateSessionRequest request, HttpContext context, IdempotentCommands idempotency, SessionCommands commands) =>
            (await idempotency.Run(Viewer(context)!.Value, Key(context), "create", request,
                () => commands.Create(Viewer(context)!.Value, request, context.RequestAborted), context.RequestAborted)).ToResult(context));
        mutations.MapPost("/{id:guid}/join", (Guid id, HttpContext context, IdempotentCommands idempotency, SessionCommands commands) =>
            Execute(id, "join", new { }, context, idempotency, () => commands.Join(Viewer(context)!.Value, id, context.RequestAborted)));
        mutations.MapPost("/{id:guid}/leave", (Guid id, HttpContext context, IdempotentCommands idempotency, SessionCommands commands) =>
            Execute(id, "leave", new { }, context, idempotency, () => commands.Leave(Viewer(context)!.Value, id, context.RequestAborted)));
        mutations.MapPost("/{id:guid}/cancel", (Guid id, HttpContext context, IdempotentCommands idempotency, SessionCommands commands) =>
            Execute(id, "cancel", new { }, context, idempotency, () => commands.Cancel(Viewer(context)!.Value, id, context.RequestAborted)));
        mutations.MapPatch("/{id:guid}/capacity", (Guid id, CapacityRequest request, HttpContext context, IdempotentCommands idempotency, SessionCommands commands) =>
            Execute(id, "capacity", request, context, idempotency, () => commands.Capacity(Viewer(context)!.Value, id, request.Capacity, context.RequestAborted)));
    }

    private static async Task<IResult> Execute(Guid id, string action, object payload, HttpContext context, IdempotentCommands idempotency, Func<Task<MutationResult>> command)
        => (await idempotency.Run(Viewer(context)!.Value, Key(context), $"{action}/{id}", payload, command, context.RequestAborted)).ToResult(context);
    private static string Key(HttpContext context) => context.Request.Headers["Idempotency-Key"].ToString();
    private static Guid? Viewer(HttpContext context) => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static async Task<IResult> List([AsParameters] SessionQuery query, HttpContext context, AppDbContext db, SessionQueries queries, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!ValidPage(query.Page, query.PageSize) || query.From >= query.To || (query.Level.HasValue && !Enum.IsDefined(query.Level.Value)))
            return Results.Problem(statusCode: 400, title: "Проверь страницу, временной интервал и уровень игры.");
        if ((query.From.HasValue && !ExplicitOffsetDateTimeConverter.HasOffset(context.Request.Query["from"].ToString()))
            || (query.To.HasValue && !ExplicitOffsetDateTimeConverter.HasOffset(context.Request.Query["to"].ToString())))
            return Results.Problem(statusCode: 400, title: "Для времени нужен ISO-формат с Z или явным смещением UTC.");
        if (query.ActivityId.HasValue && !await db.Activities.AnyAsync(activity => activity.Id == query.ActivityId, ct)) return Results.NotFound();
        if (query.LocationId.HasValue && !await db.Locations.AnyAsync(location => location.Id == query.LocationId && location.ApprovalStatus == ApprovalStatus.Approved, ct)) return Results.NotFound();
        var now = await queries.Now(ct);
        var sessions = db.Sessions.AsNoTracking().Where(session => session.CancelledAt == null && session.EndsAt > now && session.PlaceActivity.Location.ApprovalStatus == ApprovalStatus.Approved && session.PlaceActivity.IsEnabled);
        if (query.ActivityId.HasValue) sessions = sessions.Where(session => session.ActivityId == query.ActivityId);
        if (query.LocationId.HasValue) sessions = sessions.Where(session => session.LocationId == query.LocationId);
        if (query.Level.HasValue) sessions = sessions.Where(session => session.SkillLevel == query.Level);
        if (query.From.HasValue) sessions = sessions.Where(session => session.EndsAt > query.From);
        if (query.To.HasValue) sessions = sessions.Where(session => session.StartsAt < query.To);
        if (query.JoinableOnly) sessions = sessions.Where(session => session.Participations.Count(p => p.LeftAt == null) < session.Capacity);
        return await Page(sessions, query.Page, query.PageSize, Viewer(context), now, queries, ct);
    }

    private static async Task<IResult> Mine([AsParameters] MySessionsQuery query, HttpContext context, AppDbContext db, SessionQueries queries, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!ValidPage(query.Page, query.PageSize)) return Results.Problem(statusCode: 400, title: "Некорректный размер или номер страницы.");
        var userId = Viewer(context)!.Value;
        return await Page(db.Sessions.AsNoTracking().Where(session => session.OrganiserId == userId || session.Participations.Any(p => p.UserId == userId)),
            query.Page, query.PageSize, userId, await queries.Now(ct), queries, ct, history: true);
    }

    private static async Task<IResult> Page(IQueryable<GameSession> sessions, int page, int size, Guid? viewer, DateTimeOffset now,
        SessionQueries queries, CancellationToken ct, bool history = false)
    {
        var ordered = history ? sessions.OrderByDescending(session => session.StartsAt).ThenBy(session => session.Id)
            : sessions.OrderBy(session => session.StartsAt).ThenBy(session => session.Id);
        var results = await queries.Project(ordered.Skip((page - 1) * size).Take(size + 1), viewer, now).ToArrayAsync(ct);
        return Results.Ok(new SessionPage(results.Take(size).ToArray(), page, size, results.Length > size));
    }
    private static bool ValidPage(int page, int size) => page is >= 1 and <= 100000 && size is >= 1 and <= 100;
}
