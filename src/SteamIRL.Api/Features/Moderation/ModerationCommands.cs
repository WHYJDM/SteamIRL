using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;

namespace SteamIRL.Api.Features.Moderation;

public sealed class ModerationCommands(AppDbContext db, ModerationReads reads, SessionQueries clock)
{
    public async Task<MutationResult> Submission(Guid moderatorId, Guid id, DecisionRequest request, CancellationToken ct)
    {
        if (!await ModeratorAccess.HasRole(db, moderatorId, ct)) return Forbidden();
        if (!ReasonValid(request.Reason) || request.Action is not (ModerationAction.Approve or ModerationAction.Reject)) return Invalid();
        var locationId = await db.LocationSubmissions.Where(item => item.Id == id).Select(item => (Guid?)item.LocationId).SingleOrDefaultAsync(ct);
        if (locationId is null) return Missing();
        var location = (await LocationLocks.Update(db, locationId.Value, ct))!;
        var submission = await db.LocationSubmissions.FromSqlInterpolated($"SELECT * FROM location_submissions WHERE \"Id\"={id} FOR UPDATE").SingleAsync(ct);
        if (submission.State != SubmissionState.Pending) return Closed();
        if (request.Action == ModerationAction.Approve && (submission.BaseRevision != location.Revision || location.ApprovalStatus != (submission.Kind == SubmissionKind.New ? ApprovalStatus.Pending : ApprovalStatus.Approved)))
            return MutationResult.Error(409, "stale_submission", "Площадка уже изменилась или снята с публикации. Отклони устаревшую заявку, чтобы автор мог отправить новую.");
        if (request.Action == ModerationAction.Approve && submission.Kind == SubmissionKind.New)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({submission.Fingerprint}, 0))", ct);
            var duplicate = await db.LocationSubmissions.AnyAsync(item => item.Id != id && item.Fingerprint == submission.Fingerprint
                && item.Kind == SubmissionKind.New && item.State == SubmissionState.Approved, ct);
            if (duplicate) return MutationResult.Error(409, "approved_duplicate", "Такая площадка уже одобрена. Проверь дубли и отклони повторную заявку.");
        }
        var before = await reads.Snapshot(location, ct);
        var now = await clock.Now(ct); var affected = 0;
        if (request.Action == ModerationAction.Approve)
        {
            var activityIds = await db.SubmissionActivities.Where(link => link.SubmissionId == id).Select(link => link.ActivityId).ToArrayAsync(ct);
            var links = await db.LocationActivities.Where(link => link.LocationId == location.Id).ToListAsync(ct);
            var disabled = links.Where(link => link.IsEnabled && !activityIds.Contains(link.ActivityId)).Select(link => link.ActivityId).ToArray();
            if (disabled.Length > 0) affected = await CancelSessions(location.Id, disabled, null, "Активность больше не поддерживается этой площадкой.", ct);
            foreach (var link in links) link.IsEnabled = activityIds.Contains(link.ActivityId);
            foreach (var activity in activityIds.Except(links.Select(link => link.ActivityId)))
                db.LocationActivities.Add(new LocationActivity { LocationId = location.Id, ActivityId = activity, IsEnabled = true });
            location.Name = submission.Name; location.Description = submission.Description; location.Address = submission.Address;
            location.Latitude = submission.Latitude; location.Longitude = submission.Longitude; location.SourceUrl = submission.SourceUrl;
            location.CoordinateNote = submission.CoordinateNote; location.AccessNotes = submission.AccessNotes;
            location.ApprovalStatus = ApprovalStatus.Approved; location.UpdatedAt = now; location.SourceCheckedAt = now; location.Revision++;
            submission.State = SubmissionState.Approved;
        }
        else
        {
            submission.State = SubmissionState.Rejected;
            if (submission.Kind == SubmissionKind.New) { location.ApprovalStatus = ApprovalStatus.Rejected; location.Revision++; location.UpdatedAt = now; }
        }
        await db.SaveChangesAsync(ct);
        var after = await reads.Snapshot(location, ct);
        db.ModerationDecisions.Add(new ModerationDecision { Id = Guid.NewGuid(), ModeratorId = moderatorId, SubmissionId = id,
            Action = request.Action, Reason = request.Reason!.Trim(), DecidedAt = now, AffectedSessions = affected,
            BeforeJson = JsonSerializer.Serialize(before, MutationResult.JsonOptions), AfterJson = JsonSerializer.Serialize(after, MutationResult.JsonOptions) });
        await db.SaveChangesAsync(ct);
        return MutationResult.Value((await reads.Submission(id, ct))!);
    }

    public async Task<MutationResult> Report(Guid moderatorId, Guid id, DecisionRequest request, CancellationToken ct)
    {
        if (!await ModeratorAccess.HasRole(db, moderatorId, ct)) return Forbidden();
        if (!ReasonValid(request.Reason) || request.Action is not (ModerationAction.Resolve or ModerationAction.Dismiss or ModerationAction.RemoveContent)) return Invalid();
        var target = await db.ContentReports.Where(item => item.Id == id).Select(item => new { LocationId = item.LocationId ?? item.Session!.LocationId, item.SessionId }).SingleOrDefaultAsync(ct);
        if (target is null) return Missing();
        var location = (await LocationLocks.Update(db, target.LocationId, ct))!;
        var report = await db.ContentReports.FromSqlInterpolated($"SELECT * FROM content_reports WHERE \"Id\"={id} FOR UPDATE").SingleAsync(ct);
        if (report.State != ReportState.Open) return Closed();
        var before = await ReportSnapshot(location, target.SessionId, ct);
        var now = await clock.Now(ct); var affected = 0;
        if (request.Action == ModerationAction.RemoveContent)
        {
            if (target.SessionId.HasValue) affected = await CancelSessions(location.Id, null, target.SessionId, "Игра снята модератором.", ct);
            else
            {
                affected = await CancelSessions(location.Id, null, null, "Площадка снята с публикации модератором.", ct);
                if (location.ApprovalStatus != ApprovalStatus.Removed) { location.ApprovalStatus = ApprovalStatus.Removed; location.Revision++; location.UpdatedAt = now; }
            }
        }
        report.State = request.Action == ModerationAction.Dismiss ? ReportState.Dismissed : ReportState.Resolved;
        await db.SaveChangesAsync(ct);
        var after = await ReportSnapshot(location, target.SessionId, ct);
        db.ModerationDecisions.Add(new ModerationDecision { Id = Guid.NewGuid(), ModeratorId = moderatorId, ReportId = id,
            Action = request.Action, Reason = request.Reason!.Trim(), DecidedAt = now, AffectedSessions = affected,
            BeforeJson = before, AfterJson = after });
        await db.SaveChangesAsync(ct);
        return MutationResult.Value((await reads.Report(id, ct))!);
    }

    private async Task<int> CancelSessions(Guid locationId, Guid[]? activityIds, Guid? sessionId, string reason, CancellationToken ct)
    {
        // Location update lock excludes ALL game commands for this place. Lock
        // affected sessions in deterministic order before changing them.
        var ids = await db.Sessions.Where(item => item.LocationId == locationId && (activityIds == null || activityIds.Contains(item.ActivityId))
            && (sessionId == null || item.Id == sessionId)).OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync(ct);
        var affected = 0;
        foreach (var id in ids)
        {
            var game = await db.Sessions.FromSqlInterpolated($"SELECT * FROM sessions WHERE \"Id\"={id} FOR UPDATE").SingleAsync(ct);
            var now = await clock.Now(ct);
            if (game.CancelledAt is not null || game.EndsAt <= now) continue;
            game.CancelledAt = now; game.UpdatedAt = now; game.CancellationReason = reason; affected++;
        }
        return affected;
    }
    private async Task<string> ReportSnapshot(Location location, Guid? sessionId, CancellationToken ct)
    {
        if (sessionId.HasValue) return JsonSerializer.Serialize(await db.Sessions.AsNoTracking().Where(item => item.Id == sessionId)
            .Select(item => new { item.Id, item.CancelledAt, item.CancellationReason }).SingleAsync(ct), MutationResult.JsonOptions);
        return JsonSerializer.Serialize(new { Location = await reads.Snapshot(location, ct), Sessions = await db.Sessions.AsNoTracking()
            .Where(item => item.LocationId == location.Id).OrderBy(item => item.Id).Select(item => new { item.Id, item.CancelledAt, item.CancellationReason }).ToArrayAsync(ct) }, MutationResult.JsonOptions);
    }
    private static bool ReasonValid(string? reason) => reason?.Trim().Length is >= 10 and <= 1000;
    private static MutationResult Invalid() => MutationResult.Error(400, "decision_fields", "Выбери допустимое решение и укажи причину от 10 до 1000 символов.");
    private static MutationResult Missing() => MutationResult.Error(404, "case_missing", "Заявка или жалоба не найдена.");
    private static MutationResult Closed() => MutationResult.Error(409, "case_closed", "По этой записи уже принято решение. Обнови очередь.");
    private static MutationResult Forbidden() => MutationResult.Error(403, "moderator_required", "Нужны права модератора.");
}
