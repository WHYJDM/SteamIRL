using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;

namespace SteamIRL.Api.Features.Moderation;

public sealed class ModerationReads(AppDbContext db)
{
    public IQueryable<LocationSubmission> Submissions => db.LocationSubmissions.AsNoTracking().Include(item => item.Author).ThenInclude(user => user.Profile)
        .Include(item => item.Location).Include(item => item.Activities).ThenInclude(link => link.Activity)
        .Include(item => item.Decisions).ThenInclude(decision => decision.Moderator).ThenInclude(user => user.Profile).AsSplitQuery();
    public IQueryable<ContentReport> Reports => db.ContentReports.AsNoTracking().Include(item => item.Author).ThenInclude(user => user.Profile)
        .Include(item => item.Location).Include(item => item.Session).ThenInclude(session => session!.PlaceActivity).ThenInclude(link => link.Location)
        .Include(item => item.Decisions).ThenInclude(decision => decision.Moderator).ThenInclude(user => user.Profile).AsSplitQuery();
    public async Task<SubmissionResponse?> Submission(Guid id, CancellationToken ct) => (await Submissions.SingleOrDefaultAsync(item => item.Id == id, ct)) is { } entity ? Map(entity) : null;
    public async Task<ReportResponse?> Report(Guid id, CancellationToken ct) => (await Reports.SingleOrDefaultAsync(item => item.Id == id, ct)) is { } entity ? Map(entity) : null;
    public static SubmissionResponse Map(LocationSubmission item) => new(item.Id, item.LocationId, item.Kind, item.State, item.Author.Profile.DisplayName,
        item.BaseRevision, item.Location.Revision, item.Location.ApprovalStatus,
        new LocationSnapshot(item.Name, item.Description, item.Address, item.Latitude, item.Longitude, item.SourceUrl, item.CoordinateNote, item.AccessNotes,
            item.Activities.Select(link => link.ActivityId).Order().ToArray(), item.BaseRevision, item.Kind == SubmissionKind.New ? ApprovalStatus.Pending : ApprovalStatus.Approved),
        item.OriginalDataJson == "null" ? null : JsonSerializer.Deserialize<LocationSnapshot>(item.OriginalDataJson, MutationResult.JsonOptions),
        item.Activities.OrderBy(link => link.Activity.Slug).Select(link => new LocationActivityResponse(link.ActivityId, link.Activity.Slug, link.Activity.Name)).ToArray(),
        item.Evidence, item.SubmittedAt, item.Decisions.OrderBy(decision => decision.DecidedAt).Select(Map).ToArray());
    public static ReportResponse Map(ContentReport item) => new(item.Id, item.LocationId, item.SessionId,
        item.Location?.Name ?? $"Игра на площадке {item.Session!.PlaceActivity.Location.Name}", item.Author.Profile.DisplayName,
        item.Reason, item.Details, item.State, item.SubmittedAt, item.Decisions.OrderBy(decision => decision.DecidedAt).Select(Map).ToArray());
    private static DecisionResponse Map(ModerationDecision item) => new(item.Id, item.Action, item.Reason, item.DecidedAt, item.Moderator.Profile.DisplayName, item.AffectedSessions);
    public async Task<LocationSnapshot> Snapshot(Location item, CancellationToken ct) => new(item.Name, item.Description, item.Address, item.Latitude, item.Longitude,
        item.SourceUrl, item.CoordinateNote, item.AccessNotes, await db.LocationActivities.Where(link => link.LocationId == item.Id && link.IsEnabled)
            .OrderBy(link => link.ActivityId).Select(link => link.ActivityId).ToArrayAsync(ct), item.Revision, item.ApprovalStatus);
}
