using SteamIRL.Api.Features.Activities;

namespace SteamIRL.Api.Features.Locations;

public enum ApprovalStatus { Pending, Approved, Rejected, Removed }

public sealed class Location
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Address { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Revision { get; set; } = 1;
    public required string SourceUrl { get; set; }
    public DateTimeOffset SourceCheckedAt { get; set; }
    public required string CoordinateNote { get; set; }
    public required string AccessNotes { get; set; }
    public List<LocationActivity> Activities { get; set; } = [];
}

public sealed class LocationActivity
{
    public Guid LocationId { get; set; }
    public Guid ActivityId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Location Location { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
}
