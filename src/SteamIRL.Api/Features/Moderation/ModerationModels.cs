using SteamIRL.Api.Features.Accounts;
using SteamIRL.Api.Features.Activities;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;

namespace SteamIRL.Api.Features.Moderation;

public enum SubmissionKind { New, Correction }
public enum SubmissionState { Pending, Approved, Rejected }
public enum ReportReason { Inaccurate = 1, Spam, Abuse, AccessIssue, Safety, Other }
public enum ReportState { Open, Resolved, Dismissed }
public enum ModerationAction { Approve = 1, Reject, Resolve, Dismiss, RemoveContent }

public sealed class LocationSubmission
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;
    public SubmissionKind Kind { get; set; }
    public SubmissionState State { get; set; }
    public long BaseRevision { get; set; }
    public required string Fingerprint { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Address { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public required string SourceUrl { get; set; }
    public required string CoordinateNote { get; set; }
    public required string AccessNotes { get; set; }
    public required string Evidence { get; set; }
    public required string OriginalDataJson { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public List<SubmissionActivity> Activities { get; set; } = [];
    public List<ModerationDecision> Decisions { get; set; } = [];
}

public sealed class SubmissionActivity
{
    public Guid SubmissionId { get; set; }
    public Guid ActivityId { get; set; }
    public LocationSubmission Submission { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
}

public sealed class ContentReport
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public Guid? SessionId { get; set; }
    public GameSession? Session { get; set; }
    public ReportReason Reason { get; set; }
    public required string Details { get; set; }
    public ReportState State { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public List<ModerationDecision> Decisions { get; set; } = [];
}

public sealed class ModerationDecision
{
    public Guid Id { get; set; }
    public Guid ModeratorId { get; set; }
    public ApplicationUser Moderator { get; set; } = null!;
    public Guid? SubmissionId { get; set; }
    public LocationSubmission? Submission { get; set; }
    public Guid? ReportId { get; set; }
    public ContentReport? Report { get; set; }
    public ModerationAction Action { get; set; }
    public required string Reason { get; set; }
    public required string BeforeJson { get; set; }
    public required string AfterJson { get; set; }
    public int AffectedSessions { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}
