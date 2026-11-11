using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;

namespace SteamIRL.Api.Features.Moderation;

public sealed class SubmissionCommands(AppDbContext db, ModerationReads reads, SessionQueries clock)
{
    public async Task<MutationResult> Submit(Guid authorId, SubmissionRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? ""; var address = request.Address?.Trim() ?? "";
        var description = request.Description?.Trim() ?? ""; var source = request.SourceUrl?.Trim() ?? "";
        var coordinateNote = request.CoordinateNote?.Trim() ?? ""; var access = request.AccessNotes?.Trim() ?? "";
        var evidence = request.Evidence?.Trim() ?? "";
        if (name.Length is < 2 or > 200 || name.Any(char.IsControl) || address.Length is < 2 or > 300 || address.Any(char.IsControl))
            return Error("Укажи название от 2 до 200 символов и адрес или ориентир от 2 до 300 символов.");
        if (request.Latitude is null or < -90 or > 90 || request.Longitude is null or < -180 or > 180)
            return Error("Укажи корректные широту (−90…90) и долготу (−180…180).");
        if (description.Length > 4000 || coordinateNote.Length > 500 || access.Length > 2000 || evidence.Length > 2000 || source.Length > 1000)
            return Error("Описание, примечания или источник превышают допустимую длину.");
        if (source.Length > 0 && (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || source.Any(char.IsControl)))
            return Error("Источник должен быть абсолютной HTTPS-ссылкой без данных входа.");
        if (request.ActivityIds is not { Length: >= 1 and <= 20 }) return Error("Выбери от одной до двадцати активностей.");
        var activities = request.ActivityIds.Distinct().Order().ToArray();
        if (activities.Length != request.ActivityIds.Length || await db.Activities.CountAsync(activity => activities.Contains(activity.Id), ct) != activities.Length)
            return Error("Проверь выбранные активности: повторы и неизвестные значения недопустимы.");
        var kind = request.LocationId.HasValue ? SubmissionKind.Correction : SubmissionKind.New;
        Location? location = null; var originalJson = "null";
        if (request.LocationId.HasValue)
        {
            location = await LocationLocks.Share(db, request.LocationId.Value, ct);
            if (location?.ApprovalStatus != ApprovalStatus.Approved) return MutationResult.Error(404, "location_missing", "Площадка не найдена.");
            if (request.BaseRevision != location.Revision) return MutationResult.Error(409, "stale_location", "Площадка уже обновилась. Загрузи её заново перед исправлением.");
            if (evidence.Length < 10) return Error("Для исправления поясни источник изменений: не менее 10 символов.");
            originalJson = JsonSerializer.Serialize(await reads.Snapshot(location, ct), MutationResult.JsonOptions);
        }
        else if (request.BaseRevision is not null) return Error("Для новой площадки версия существующей записи не передаётся.");
        var latitude = decimal.Round(request.Latitude.Value, 6); var longitude = decimal.Round(request.Longitude.Value, 6);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            kind, target = request.LocationId, revision = request.BaseRevision, name = name.ToUpperInvariant(), latitude, longitude, activities
        }, MutationResult.JsonOptions))));
        var existing = await db.LocationSubmissions.Where(item => item.AuthorId == authorId && item.Fingerprint == fingerprint && item.State == SubmissionState.Pending)
            .Select(item => (Guid?)item.Id).FirstOrDefaultAsync(ct);
        if (existing.HasValue) return Conflict("submission_exists", "Такая заявка уже ожидает рассмотрения.", "existingSubmissionId", existing.Value);
        var now = await clock.Now(ct);
        if (await db.LocationSubmissions.CountAsync(item => item.AuthorId == authorId && item.SubmittedAt > now.AddHours(-1), ct) >= 10)
            return MutationResult.Error(429, "submission_limit", "Можно отправить не более десяти заявок за час.");
        if (coordinateNote.Length == 0) coordinateNote = "Точка предложена пользователем. Точный вход и состояние площадки уточни перед поездкой.";
        if (location is null)
        {
            location = new Location { Id = Guid.NewGuid(), Name = name, Description = description, Address = address, Latitude = latitude,
                Longitude = longitude, ApprovalStatus = ApprovalStatus.Pending, SourceUrl = source, SourceCheckedAt = now, UpdatedAt = now,
                CoordinateNote = coordinateNote, AccessNotes = access };
            db.Locations.Add(location);
            foreach (var activity in activities) db.LocationActivities.Add(new LocationActivity { LocationId = location.Id, ActivityId = activity });
        }
        var submission = new LocationSubmission { Id = Guid.NewGuid(), LocationId = location.Id, AuthorId = authorId, Kind = kind,
            State = SubmissionState.Pending, BaseRevision = location.Revision, Fingerprint = fingerprint, Name = name, Description = description,
            Address = address, Latitude = latitude, Longitude = longitude, SourceUrl = source, CoordinateNote = coordinateNote,
            AccessNotes = access, Evidence = evidence, OriginalDataJson = originalJson, SubmittedAt = now,
            Activities = activities.Select(id => new SubmissionActivity { ActivityId = id }).ToList() };
        db.LocationSubmissions.Add(submission);
        await db.SaveChangesAsync(ct);
        return MutationResult.Value((await reads.Submission(submission.Id, ct))!, 201, $"/api/location-submissions/{submission.Id}");
    }

    public async Task<MutationResult> Report(Guid authorId, ReportRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Reason) || request.LocationId.HasValue == request.SessionId.HasValue || (request.Details?.Trim().Length ?? 0) > 2000)
            return Error("Выбери один объект, причину жалобы и описание до 2000 символов.");
        Guid? locationId = request.LocationId;
        if (request.SessionId.HasValue) locationId = await db.Sessions.Where(item => item.Id == request.SessionId).Select(item => (Guid?)item.LocationId).SingleOrDefaultAsync(ct);
        if (locationId is null) return MutationResult.Error(404, "report_target", "Объект жалобы не найден.");
        var location = await LocationLocks.Share(db, locationId.Value, ct);
        var visible = location?.ApprovalStatus == ApprovalStatus.Approved;
        if (request.SessionId.HasValue)
        {
            var session = await db.Sessions.AsNoTracking().Where(item => item.Id == request.SessionId).Select(item => new {
                item.OrganiserId, item.PlaceActivity.IsEnabled, Participated = item.Participations.Any(p => p.UserId == authorId)
            }).SingleAsync(ct);
            visible = visible && session.IsEnabled || session.OrganiserId == authorId || session.Participated;
        }
        if (!visible) return MutationResult.Error(404, "report_target", "Объект жалобы не найден.");
        var existing = await db.ContentReports.Where(item => item.AuthorId == authorId && item.LocationId == request.LocationId
            && item.SessionId == request.SessionId && item.Reason == request.Reason && item.State == ReportState.Open)
            .Select(item => (Guid?)item.Id).FirstOrDefaultAsync(ct);
        if (existing.HasValue) return Conflict("report_exists", "У тебя уже есть открытая жалоба с этой причиной.", "existingReportId", existing.Value);
        var now = await clock.Now(ct);
        if (await db.ContentReports.CountAsync(item => item.AuthorId == authorId && item.SubmittedAt > now.AddHours(-1), ct) >= 10)
            return MutationResult.Error(429, "report_limit", "Можно отправить не более десяти жалоб за час.");
        var report = new ContentReport { Id = Guid.NewGuid(), AuthorId = authorId, LocationId = request.LocationId, SessionId = request.SessionId,
            Reason = request.Reason, Details = request.Details?.Trim() ?? "", State = ReportState.Open, SubmittedAt = now };
        db.ContentReports.Add(report); await db.SaveChangesAsync(ct);
        return MutationResult.Value((await reads.Report(report.Id, ct))!, 201, $"/api/content-reports/{report.Id}");
    }

    private static MutationResult Error(string message) => MutationResult.Error(400, "contribution_fields", message);
    public static MutationResult Conflict(string code, string message, string field, Guid id) => new(409,
        JsonSerializer.Serialize(new Dictionary<string, object?> { ["status"] = 409, ["title"] = message, ["code"] = code, [field] = id }, MutationResult.JsonOptions), "application/problem+json");
}
