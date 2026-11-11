using SteamIRL.Api.Features.Accounts;
using SteamIRL.Api.Features.Locations;

namespace SteamIRL.Api.Features.Sessions;

public enum SessionKind { Planned = 1, Immediate = 2 }
public enum SkillLevel { Beginner = 1, Intermediate = 2, Advanced = 3 }
public enum EquipmentStatus { NotSpecified, BallAvailable, BallNeeded }

public sealed class GameSession
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid OrganiserId { get; set; }
    public LocationActivity PlaceActivity { get; set; } = null!;
    public ApplicationUser Organiser { get; set; } = null!;
    public SessionKind Kind { get; set; }
    public SkillLevel SkillLevel { get; set; }
    public EquipmentStatus EquipmentStatus { get; set; }
    public int Capacity { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<Participation> Participations { get; set; } = [];
}

public sealed class Participation
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public GameSession Session { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
}

public sealed class IdempotencyRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Key { get; set; }
    public required string Operation { get; set; }
    public required string RequestHash { get; set; }
    public required string ResponseJson { get; set; }
    public required string ContentType { get; set; }
    public string? ResourceLocation { get; set; }
    public int ResponseStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
