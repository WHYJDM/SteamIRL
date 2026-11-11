using System.Text.Json.Serialization;
using SteamIRL.Api.Features.Locations;

namespace SteamIRL.Api.Features.Moderation;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmissionRequest(string? Name, string? Address, decimal? Latitude, decimal? Longitude, Guid[]? ActivityIds,
    string? Description = null, string? SourceUrl = null, string? CoordinateNote = null, string? AccessNotes = null,
    string? Evidence = null, Guid? LocationId = null, long? BaseRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportRequest(ReportReason Reason, string? Details = null, Guid? LocationId = null, Guid? SessionId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecisionRequest(ModerationAction Action, string? Reason);
public sealed record ModerationPageQuery(int Page = 1, int PageSize = 20);
public sealed record SubmissionQueueQuery(SubmissionState State = SubmissionState.Pending, int Page = 1, int PageSize = 20);
public sealed record ReportQueueQuery(ReportState State = ReportState.Open, int Page = 1, int PageSize = 20);
public sealed record ReviewPage<T>(T[] Items, int Page, int PageSize, bool HasMore);
public sealed record DecisionResponse(Guid Id, ModerationAction Action, string Reason, DateTimeOffset DecidedAt, string ModeratorName, int AffectedSessions);
public sealed record LocationSnapshot(string Name, string Description, string Address, decimal Latitude, decimal Longitude,
    string SourceUrl, string CoordinateNote, string AccessNotes, Guid[] ActivityIds, long Revision, ApprovalStatus Status);
public sealed record SubmissionResponse(Guid Id, Guid LocationId, SubmissionKind Kind, SubmissionState State, string AuthorName,
    long BaseRevision, long CurrentRevision, ApprovalStatus LocationStatus, LocationSnapshot Proposed,
    LocationSnapshot? Original, LocationActivityResponse[] Activities, string Evidence, DateTimeOffset SubmittedAt, DecisionResponse[] Decisions);
public sealed record ReportResponse(Guid Id, Guid? LocationId, Guid? SessionId, string TargetName, string AuthorName,
    ReportReason Reason, string Details, ReportState State, DateTimeOffset SubmittedAt, DecisionResponse[] Decisions);
public sealed record NearbyLocation(Guid Id, string Name, string Address);
public sealed record SubmissionDetail(SubmissionResponse Submission, NearbyLocation[] NearbyLocations);
