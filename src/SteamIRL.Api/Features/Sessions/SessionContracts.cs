using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace SteamIRL.Api.Features.Sessions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateSessionRequest(Guid LocationId, Guid ActivityId, SessionKind Kind, SkillLevel SkillLevel,
    int Capacity, EquipmentStatus EquipmentStatus = EquipmentStatus.NotSpecified,
    [property: JsonConverter(typeof(ExplicitOffsetDateTimeConverter))] DateTimeOffset? StartsAt = null,
    [property: JsonConverter(typeof(ExplicitOffsetDateTimeConverter))] DateTimeOffset? EndsAt = null, int? DurationMinutes = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CapacityRequest(int Capacity);
public sealed record SessionQuery(Guid? ActivityId = null, Guid? LocationId = null, DateTimeOffset? From = null,
    DateTimeOffset? To = null, SkillLevel? Level = null, bool JoinableOnly = false, int Page = 1, int PageSize = 50);
public sealed record MySessionsQuery(int Page = 1, int PageSize = 50);
public sealed record SessionPage(SessionResponse[] Items, int Page, int PageSize, bool HasMore);
public sealed record SessionResponse(Guid Id, Guid LocationId, string LocationName, Guid ActivityId, string ActivityName,
    string ActivitySlug, Guid OrganiserId, string OrganiserName, SessionKind Kind, SkillLevel SkillLevel,
    EquipmentStatus EquipmentStatus, int Capacity, int ParticipantCount, int RemainingPlaces,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, bool IsJoinable, bool IsOrganiser,
    bool IsParticipating, DateTimeOffset ServerNow, string? CancellationReason = null);

public sealed record MutationResult(int Status, string Json, string ContentType = "application/json", string? Location = null)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public static MutationResult Success(SessionResponse response, bool created = false) => new(created ? 201 : 200,
        JsonSerializer.Serialize(response, JsonOptions), Location: created ? $"/api/sessions/{response.Id}" : null);
    public static MutationResult Value(object response, int status = 200, string? location = null) => new(status,
        JsonSerializer.Serialize(response, JsonOptions), Location: location);
    public static MutationResult Error(int status, string code, string message, Guid? existingSessionId = null) => new(status,
        JsonSerializer.Serialize(new ProblemDetails { Status = status, Title = message,
            Extensions = { ["code"] = code, ["existingSessionId"] = existingSessionId } }, JsonOptions), "application/problem+json");
    public IResult ToResult(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (Location is not null) context.Response.Headers.Location = Location;
        return Results.Content(Json, ContentType, statusCode: Status);
    }
}
