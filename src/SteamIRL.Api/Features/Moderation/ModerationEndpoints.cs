using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Accounts;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;

namespace SteamIRL.Api.Features.Moderation;

public static class ModerationEndpoints
{
    public static void MapModeration(this IEndpointRouteBuilder endpoints)
    {
        var submissions = endpoints.MapGroup("/api/location-submissions").RequireAuthorization().AddEndpointFilter<CsrfFilter>();
        submissions.MapPost("", async (SubmissionRequest request, HttpContext context, IdempotentCommands idempotency, SubmissionCommands commands) =>
            (await idempotency.Run(UserId(context), Key(context), "submission/create", request,
                () => commands.Submit(UserId(context), request, context.RequestAborted), context.RequestAborted)).ToResult(context));
        submissions.MapGet("/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, ModerationReads reads, CancellationToken ct) =>
        {
            NoCache(context);
            var allowed = await db.LocationSubmissions.AnyAsync(item => item.Id == id && item.AuthorId == UserId(context), ct)
                || await ModeratorAccess.HasRole(db, UserId(context), ct);
            if (!allowed) return Results.NotFound();
            var detail = await reads.Submission(id, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });
        var reports = endpoints.MapGroup("/api/content-reports").RequireAuthorization().AddEndpointFilter<CsrfFilter>();
        reports.MapPost("", async (ReportRequest request, HttpContext context, IdempotentCommands idempotency, SubmissionCommands commands) =>
            (await idempotency.Run(UserId(context), Key(context), "report/create", request,
                () => commands.Report(UserId(context), request, context.RequestAborted), context.RequestAborted)).ToResult(context));
        reports.MapGet("/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, ModerationReads reads, CancellationToken ct) =>
        {
            NoCache(context);
            var allowed = await db.ContentReports.AnyAsync(item => item.Id == id && item.AuthorId == UserId(context), ct)
                || await ModeratorAccess.HasRole(db, UserId(context), ct);
            if (!allowed) return Results.NotFound();
            var detail = await reads.Report(id, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });
        endpoints.MapGet("/api/me/submissions", async ([AsParameters] ModerationPageQuery query, HttpContext context, ModerationReads reads, CancellationToken ct) =>
        {
            NoCache(context); if (!ValidPage(query.Page, query.PageSize)) return InvalidPage();
            var items = await reads.Submissions.Where(item => item.AuthorId == UserId(context)).OrderByDescending(item => item.SubmittedAt).ThenBy(item => item.Id)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize + 1).ToArrayAsync(ct);
            return Results.Ok(new ReviewPage<SubmissionResponse>(items.Take(query.PageSize).Select(ModerationReads.Map).ToArray(), query.Page, query.PageSize, items.Length > query.PageSize));
        }).RequireAuthorization();
        endpoints.MapGet("/api/me/reports", async ([AsParameters] ModerationPageQuery query, HttpContext context, ModerationReads reads, CancellationToken ct) =>
        {
            NoCache(context); if (!ValidPage(query.Page, query.PageSize)) return InvalidPage();
            var items = await reads.Reports.Where(item => item.AuthorId == UserId(context)).OrderByDescending(item => item.SubmittedAt).ThenBy(item => item.Id)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize + 1).ToArrayAsync(ct);
            return Results.Ok(new ReviewPage<ReportResponse>(items.Take(query.PageSize).Select(ModerationReads.Map).ToArray(), query.Page, query.PageSize, items.Length > query.PageSize));
        }).RequireAuthorization();

        var moderation = endpoints.MapGroup("/api/moderation").RequireAuthorization("Moderator").AddEndpointFilter<CsrfFilter>();
        moderation.MapGet("/submissions", async ([AsParameters] SubmissionQueueQuery query, HttpContext context, ModerationReads reads, CancellationToken ct) =>
        {
            NoCache(context); if (!ValidPage(query.Page, query.PageSize) || !Enum.IsDefined(query.State)) return InvalidPage();
            var items = await reads.Submissions.Where(item => item.State == query.State).OrderBy(item => item.SubmittedAt).ThenBy(item => item.Id)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize + 1).ToArrayAsync(ct);
            return Results.Ok(new ReviewPage<SubmissionResponse>(items.Take(query.PageSize).Select(ModerationReads.Map).ToArray(), query.Page, query.PageSize, items.Length > query.PageSize));
        });
        moderation.MapGet("/reports", async ([AsParameters] ReportQueueQuery query, HttpContext context, ModerationReads reads, CancellationToken ct) =>
        {
            NoCache(context); if (!ValidPage(query.Page, query.PageSize) || !Enum.IsDefined(query.State)) return InvalidPage();
            var items = await reads.Reports.Where(item => item.State == query.State).OrderBy(item => item.SubmittedAt).ThenBy(item => item.Id)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize + 1).ToArrayAsync(ct);
            return Results.Ok(new ReviewPage<ReportResponse>(items.Take(query.PageSize).Select(ModerationReads.Map).ToArray(), query.Page, query.PageSize, items.Length > query.PageSize));
        });
        moderation.MapGet("/submissions/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, ModerationReads reads, CancellationToken ct) =>
        {
            NoCache(context);
            var detail = await reads.Submission(id, ct); if (detail is null) return Results.NotFound();
            var nearby = await db.Locations.AsNoTracking().Where(item => item.Id != detail.LocationId && item.ApprovalStatus == ApprovalStatus.Approved
                && item.Latitude >= detail.Proposed.Latitude - 0.001m && item.Latitude <= detail.Proposed.Latitude + 0.001m
                && item.Longitude >= detail.Proposed.Longitude - 0.002m && item.Longitude <= detail.Proposed.Longitude + 0.002m)
                .OrderBy(item => item.Name).ThenBy(item => item.Id).Take(10).Select(item => new NearbyLocation(item.Id, item.Name, item.Address)).ToArrayAsync(ct);
            return Results.Ok(new SubmissionDetail(detail, nearby));
        });
        moderation.MapPost("/submissions/{id:guid}/decision", (Guid id, DecisionRequest request, HttpContext context, AppDbContext db, IdempotentCommands idempotency, ModerationCommands commands) =>
            Decide(id, "submission", request, context, db, idempotency, () => commands.Submission(UserId(context), id, request, context.RequestAborted)));
        moderation.MapPost("/reports/{id:guid}/decision", (Guid id, DecisionRequest request, HttpContext context, AppDbContext db, IdempotentCommands idempotency, ModerationCommands commands) =>
            Decide(id, "report", request, context, db, idempotency, () => commands.Report(UserId(context), id, request, context.RequestAborted)));
    }
    private static async Task<IResult> Decide(Guid id, string type, DecisionRequest request, HttpContext context, AppDbContext db, IdempotentCommands idempotency, Func<Task<MutationResult>> execute)
        => (await idempotency.Run(UserId(context), Key(context), $"decision/{type}/{id}", request, execute, context.RequestAborted,
            () => ModeratorAccess.HasRole(db, UserId(context), context.RequestAborted))).ToResult(context);
    private static Guid UserId(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static string Key(HttpContext context) => context.Request.Headers["Idempotency-Key"].ToString();
    private static void NoCache(HttpContext context) => context.Response.Headers.CacheControl = "no-store";
    private static bool ValidPage(int page, int size) => page is >= 1 and <= 100000 && size is >= 1 and <= 50;
    private static IResult InvalidPage() => Results.Problem(statusCode: 400, title: "Проверь состояние записи и страницу (1–100000, размер 1–50).");
}
