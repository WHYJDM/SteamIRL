using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SteamIRL.Api.Data;

namespace SteamIRL.Api.Features.Sessions;

public sealed class IdempotentCommands(AppDbContext db, SessionQueries queries)
{
    public async Task<MutationResult> Run(Guid userId, string key, string operation, object payload,
        Func<Task<MutationResult>> execute, CancellationToken ct, Func<Task<bool>>? authorize = null)
    {
        if (key.Length is < 8 or > 128 || key.Any(c => c < 33 || c > 126))
            return MutationResult.Error(400, "idempotency_key", "Нужен Idempotency-Key длиной 8–128 печатных символов без пробелов.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, MutationResult.JsonOptions))));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Common lock order: actor, location, then session/submission/report.
        // This also serialises duplicate immediate creations and duplicate keys.
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is null) return MutationResult.Error(401, "account_missing", "Войди в аккаунт снова.");
        if (authorize is not null && !await authorize()) return MutationResult.Error(403, "permission_changed", "Права доступа изменились. Обнови страницу.");
        var now = await queries.Now(ct);
        var saved = await db.IdempotencyRequests.SingleOrDefaultAsync(item => item.UserId == userId && item.Key == key, ct);
        if (saved is not null && saved.ExpiresAt > now)
        {
            if (saved.Operation != operation || saved.RequestHash != hash)
                return MutationResult.Error(409, "idempotency_conflict", "Этот ключ уже использован для другого действия. Обнови данные и повтори.");
            return new MutationResult(saved.ResponseStatus, saved.ResponseJson, saved.ContentType, saved.ResourceLocation);
        }
        await db.IdempotencyRequests.Where(item => item.UserId == userId && item.Key != key && item.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        var result = await execute();
        var completedAt = await queries.Now(ct);
        if (saved is null)
        {
            saved = new IdempotencyRequest { Id = Guid.NewGuid(), UserId = userId, Key = key, Operation = operation,
                RequestHash = hash, ResponseJson = result.Json, ContentType = result.ContentType };
            db.IdempotencyRequests.Add(saved);
        }
        saved.Operation = operation; saved.RequestHash = hash; saved.ResponseJson = result.Json;
        saved.ContentType = result.ContentType; saved.ResponseStatus = result.Status; saved.ResourceLocation = result.Location;
        saved.CreatedAt = completedAt; saved.ExpiresAt = completedAt.AddHours(24);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
