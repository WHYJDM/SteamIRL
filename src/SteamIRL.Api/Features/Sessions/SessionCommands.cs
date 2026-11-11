using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Locations;

namespace SteamIRL.Api.Features.Sessions;

public sealed class SessionCommands(AppDbContext db, SessionQueries queries)
{
    public async Task<MutationResult> Create(Guid userId, CreateSessionRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.SkillLevel) || !Enum.IsDefined(request.EquipmentStatus)
            || request.Capacity is < 1 or > 30)
            return MutationResult.Error(400, "session_fields", "Проверь тип игры, уровень, инвентарь и количество игроков от 1 до 30.");
        await LocationLocks.Share(db, request.LocationId, ct);
        var place = await db.LocationActivities.AsNoTracking().Where(link => link.LocationId == request.LocationId && link.ActivityId == request.ActivityId && link.IsEnabled)
            .Select(link => new { link.Location.ApprovalStatus, link.Activity.Slug }).SingleOrDefaultAsync(ct);
        if (place is null || place.ApprovalStatus != ApprovalStatus.Approved)
            return MutationResult.Error(400, "location_activity", "Выбери одобренную площадку и поддерживаемую ею активность.");
        if (request.EquipmentStatus != EquipmentStatus.NotSpecified && place.Slug is not ("volleyball" or "basketball" or "football"))
            return MutationResult.Error(400, "equipment", "Для этой активности выбери «Не указан» в поле инвентаря.");

        var now = await queries.Now(ct);
        DateTimeOffset start, end;
        if (request.Kind == SessionKind.Planned)
        {
            if (request.StartsAt is null || request.EndsAt is null || request.StartsAt <= now || request.EndsAt <= request.StartsAt || request.DurationMinutes is not null)
                return MutationResult.Error(400, "session_time", "Для запланированной игры укажи будущее начало и более позднее окончание.");
            start = request.StartsAt.Value.ToUniversalTime(); end = request.EndsAt.Value.ToUniversalTime();
        }
        else
        {
            if (request.StartsAt is not null || request.EndsAt is not null || (request.DurationMinutes ?? 60) is < 15 or > 240)
                return MutationResult.Error(400, "session_time", "Немедленная игра начинается сейчас. Выбери срок действия от 15 до 240 минут.");
            var existing = await db.Sessions.Where(session => session.OrganiserId == userId && session.LocationId == request.LocationId
                && session.ActivityId == request.ActivityId && session.Kind == SessionKind.Immediate && session.CancelledAt == null && session.EndsAt > now)
                .Select(session => (Guid?)session.Id).FirstOrDefaultAsync(ct);
            if (existing.HasValue) return MutationResult.Error(409, "immediate_exists", "У тебя уже есть действующее приглашение на этой площадке. Открой его для изменения.", existing);
            if (await db.Sessions.CountAsync(session => session.OrganiserId == userId && session.Kind == SessionKind.Immediate && session.CreatedAt > now.AddHours(-1), ct) >= 3)
                return MutationResult.Error(429, "immediate_limit", "Можно создать не более трёх немедленных приглашений за час.");
            start = now; end = now.AddMinutes(request.DurationMinutes ?? 60);
        }
        var created = new GameSession { Id = Guid.NewGuid(), LocationId = request.LocationId, ActivityId = request.ActivityId,
            OrganiserId = userId, Kind = request.Kind, SkillLevel = request.SkillLevel, EquipmentStatus = request.EquipmentStatus,
            Capacity = request.Capacity, StartsAt = start, EndsAt = end, CreatedAt = now, UpdatedAt = now };
        db.Sessions.Add(created);
        await db.SaveChangesAsync(ct);
        return MutationResult.Success((await queries.Get(created.Id, userId, now, ct))!, created: true);
    }

    public async Task<MutationResult> Join(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await Lock(sessionId, ct);
        if (session is null) return Missing();
        // clock_timestamp is read AFTER waiting for the session lock.
        var now = await queries.Now(ct);
        if (session.CancelledAt is not null || session.EndsAt <= now) return Closed();
        if (session.OrganiserId == userId) return MutationResult.Error(409, "organiser_join", "Организатор меняет число дополнительных мест, а не вступает в свою игру.");
        if (!await Approved(session, ct)) return MutationResult.Error(409, "location_unavailable", "Площадка больше не доступна для набора игроков.");
        var participation = await db.Participations.SingleOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);
        if (participation is { LeftAt: null }) return await Result(sessionId, userId, now, ct);
        if (await Count(sessionId, ct) >= session.Capacity) return MutationResult.Error(409, "session_full", "Последнее место уже занято. Обнови карточку игры.");
        if (participation is null) db.Participations.Add(new Participation { SessionId = sessionId, UserId = userId, JoinedAt = now });
        else { participation.LeftAt = null; participation.JoinedAt = now; }
        session.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return await Result(sessionId, userId, now, ct);
    }

    public async Task<MutationResult> Leave(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await Lock(sessionId, ct);
        if (session is null) return Missing();
        var now = await queries.Now(ct);
        if (session.OrganiserId == userId) return MutationResult.Error(409, "organiser_leave", "Для завершения своей игры используй отмену.");
        var participation = await db.Participations.SingleOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);
        if (participation is null) return MutationResult.Error(404, "participation_missing", "У тебя нет записи на эту игру.");
        if (participation is { LeftAt: null })
        {
            participation.LeftAt = now; session.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }
        return await Result(sessionId, userId, now, ct);
    }

    public async Task<MutationResult> Capacity(Guid userId, Guid sessionId, int capacity, CancellationToken ct)
    {
        var session = await Lock(sessionId, ct);
        if (session is null) return Missing();
        if (session.OrganiserId != userId) return Forbidden();
        var now = await queries.Now(ct);
        if (session.CancelledAt is not null || session.EndsAt <= now) return Closed();
        if (capacity is < 1 or > 30 || capacity < await Count(sessionId, ct))
            return MutationResult.Error(400, "capacity", "Нужно от 1 до 30 дополнительных мест, не меньше числа записавшихся игроков.");
        session.Capacity = capacity; session.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return await Result(sessionId, userId, now, ct);
    }

    public async Task<MutationResult> Cancel(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await Lock(sessionId, ct);
        if (session is null) return Missing();
        if (session.OrganiserId != userId) return Forbidden();
        var now = await queries.Now(ct);
        if (session.CancelledAt is not null) return await Result(sessionId, userId, now, ct);
        if (session.EndsAt <= now) return Closed();
        session.CancelledAt = now; session.UpdatedAt = now; session.CancellationReason = "Отменено организатором.";
        await db.SaveChangesAsync(ct);
        return await Result(sessionId, userId, now, ct);
    }

    private async Task<GameSession?> Lock(Guid id, CancellationToken ct)
    {
        var locationId = await db.Sessions.AsNoTracking().Where(item => item.Id == id).Select(item => (Guid?)item.LocationId).SingleOrDefaultAsync(ct);
        if (locationId is null) return null;
        await LocationLocks.Share(db, locationId.Value, ct);
        return await db.Sessions.FromSqlInterpolated($"SELECT * FROM sessions WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    }
    private Task<int> Count(Guid id, CancellationToken ct) => db.Participations.CountAsync(p => p.SessionId == id && p.LeftAt == null, ct);
    private Task<bool> Approved(GameSession session, CancellationToken ct) => db.LocationActivities.AnyAsync(link => link.LocationId == session.LocationId
        && link.ActivityId == session.ActivityId && link.IsEnabled && link.Location.ApprovalStatus == ApprovalStatus.Approved, ct);
    private async Task<MutationResult> Result(Guid id, Guid userId, DateTimeOffset now, CancellationToken ct) => MutationResult.Success((await queries.Get(id, userId, now, ct))!);
    private static MutationResult Missing() => MutationResult.Error(404, "session_missing", "Игра не найдена.");
    private static MutationResult Closed() => MutationResult.Error(409, "session_closed", "Игра отменена или её время уже истекло.");
    private static MutationResult Forbidden() => MutationResult.Error(403, "session_owner", "Изменить или отменить игру может только её организатор.");
}
