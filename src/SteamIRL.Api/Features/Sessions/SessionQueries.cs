using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Locations;

namespace SteamIRL.Api.Features.Sessions;

public sealed class SessionQueries(AppDbContext db)
{
    public Task<DateTimeOffset> Now(CancellationToken ct) => db.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);

    public Task<SessionResponse?> Get(Guid id, Guid? viewer, DateTimeOffset now, CancellationToken ct) =>
        Project(db.Sessions.AsNoTracking().Where(session => session.Id == id), viewer, now).SingleOrDefaultAsync(ct);

    public IQueryable<SessionResponse> Project(IQueryable<GameSession> query, Guid? viewer, DateTimeOffset now) => query.Select(session => new SessionResponse(
        session.Id, session.LocationId, session.PlaceActivity.Location.Name, session.ActivityId, session.PlaceActivity.Activity.Name,
        session.PlaceActivity.Activity.Slug, session.OrganiserId, session.Organiser.Profile.DisplayName, session.Kind,
        session.SkillLevel, session.EquipmentStatus, session.Capacity, session.Participations.Count(p => p.LeftAt == null),
        session.Capacity - session.Participations.Count(p => p.LeftAt == null), session.StartsAt, session.EndsAt,
        session.CancelledAt != null ? "Cancelled" : session.EndsAt <= now ? "Expired"
            : session.Participations.Count(p => p.LeftAt == null) >= session.Capacity ? "Full" : "Open",
        session.CancelledAt == null && session.EndsAt > now && session.PlaceActivity.Location.ApprovalStatus == ApprovalStatus.Approved && session.PlaceActivity.IsEnabled
            && session.Participations.Count(p => p.LeftAt == null) < session.Capacity && session.OrganiserId != viewer,
        session.OrganiserId == viewer, viewer != null && session.Participations.Any(p => p.UserId == viewer && p.LeftAt == null), now, session.CancellationReason));
}
