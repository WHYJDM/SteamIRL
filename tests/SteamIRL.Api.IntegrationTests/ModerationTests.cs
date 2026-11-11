using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Moderation;
using SteamIRL.Api.Features.Sessions;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class ModerationTests(ModerationFixture fixture) : IClassFixture<ModerationFixture>
{
    private ApiFactory Factory => fixture.Factory;
    private static readonly Guid Volley = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Basket = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private HttpClient Browser() => Factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
    private async Task<(HttpClient Client, Guid Id)> Player(bool moderator = false)
    {
        var client = Browser();
        using var response = await Send(client, "/api/auth/register", new { email = $"moderation-{Guid.NewGuid():N}@example.test", password = TestCredentials.Password, displayName = "Проверка модерации" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        if (moderator) await ModeratorAccess.ChangeRole(Factory.Services, id, true);
        return (client, id);
    }
    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, object body, string? key = null)
    {
        using var tokenResponse = await client.GetAsync("/api/auth/csrf"); tokenResponse.EnsureSuccessStatusCode();
        using var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: MutationResult.JsonOptions) };
        request.Headers.Add("X-CSRF-TOKEN", token.RootElement.GetProperty("token").GetString());
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }
    private static async Task<T> Body<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        Assert.True(response.StatusCode == status, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(MutationResult.JsonOptions))!;
    }
    private static SubmissionRequest New() => new($"QA площадка {Guid.NewGuid():N}", "Вильнюс, тестовый ориентир", 54.7m, 25.3m, [Volley, Basket], "Описание для проверки", Evidence: "Проверка тестового предложения");
    private static Task<HttpResponseMessage> Propose(HttpClient client, SubmissionRequest request, string? key = null) => Send(client, "/api/location-submissions", request, key);
    private static Task<HttpResponseMessage> Decide(HttpClient client, Guid id, ModerationAction action = ModerationAction.Approve, string? key = null, bool report = false, string reason = "Сведения проверены модератором.") => Send(client, $"/api/moderation/{(report ? "reports" : "submissions")}/{id}/decision", new DecisionRequest(action, reason), key);
    private static async Task<SubmissionResponse> Submit(HttpClient client, SubmissionRequest? request = null) => await Body<SubmissionResponse>(await Propose(client, request ?? New()), HttpStatusCode.Created);
    private static async Task<SubmissionResponse> Approve(HttpClient author, HttpClient moderator)
    {
        var item = await Submit(author); return await Body<SubmissionResponse>(await Decide(moderator, item.Id));
    }
    private static SubmissionRequest Correction(SubmissionResponse item) => new(item.Proposed.Name + " исправлено", item.Proposed.Address, item.Proposed.Latitude, item.Proposed.Longitude,
        item.Proposed.ActivityIds, "Описание исправлено", Evidence: "Подтверждённое изменение сведений", LocationId: item.LocationId, BaseRevision: item.CurrentRevision);
    private static async Task<ReportResponse> Report(HttpClient author, Guid? locationId = null, Guid? sessionId = null, ReportReason reason = ReportReason.Inaccurate) =>
        await Body<ReportResponse>(await Send(author, "/api/content-reports", new ReportRequest(reason, "Приватные сведения для проверки", locationId, sessionId)), HttpStatusCode.Created);
    private static async Task<SessionResponse> Game(HttpClient owner, Guid locationId, Guid? activity = null) => await Body<SessionResponse>(await Send(owner, "/api/sessions",
        new CreateSessionRequest(locationId, activity ?? Volley, SessionKind.Planned, SkillLevel.Beginner, 2, EquipmentStatus.NotSpecified, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2))), HttpStatusCode.Created);

    [Fact]
    public async Task Pending_place_is_private_and_cannot_host_games()
    {
        using var author = (await Player()).Client; using var other = (await Player()).Client; using var guest = Browser();
        var item = await Submit(author);
        Assert.Equal(SubmissionState.Pending, item.State);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/locations/{item.LocationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/location-submissions/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"/api/location-submissions/{item.Id}")).StatusCode);
        using var own = await author.GetAsync($"/api/location-submissions/{item.Id}"); own.EnsureSuccessStatusCode();
        Assert.Contains("no-store", own.Headers.CacheControl!.ToString());
        Assert.DoesNotContain("email", await own.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var response = await Send(author, "/api/sessions", new CreateSessionRequest(item.LocationId, Volley, SessionKind.Immediate, SkillLevel.Beginner, 2, EquipmentStatus.NotSpecified));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var mine = await author.GetFromJsonAsync<ReviewPage<SubmissionResponse>>("/api/me/submissions", MutationResult.JsonOptions);
        Assert.Contains(mine!.Items, row => row.Id == item.Id);
        var theirs = await other.GetFromJsonAsync<ReviewPage<SubmissionResponse>>("/api/me/submissions", MutationResult.JsonOptions);
        Assert.Empty(theirs!.Items);
    }
    [Fact]
    public async Task Approval_publishes_fields_and_records_audit_and_nearby_places()
    {
        using var author = (await Player()).Client; using var mod = (await Player(true)).Client; using var guest = Browser();
        var item = await Submit(author, New() with { Latitude = 54.6954m, Longitude = 25.2735m, SourceUrl = "https://example.test/place" });
        var detail = await mod.GetFromJsonAsync<SubmissionDetail>($"/api/moderation/submissions/{item.Id}", MutationResult.JsonOptions);
        // Near-duplicate guidance returns only approved places, never the pending target itself.
        Assert.DoesNotContain(detail!.NearbyLocations, place => place.Id == item.LocationId);
        var approved = await Body<SubmissionResponse>(await Decide(mod, item.Id));
        Assert.Equal(SubmissionState.Approved, approved.State); Assert.Equal(2, approved.CurrentRevision);
        Assert.Single(approved.Decisions); Assert.Equal(ModerationAction.Approve, approved.Decisions[0].Action);
        var place = await guest.GetFromJsonAsync<LocationResponse>($"/api/locations/{item.LocationId}");
        Assert.Equal(item.Proposed.Name, place!.Name); Assert.Equal(2, place.Activities.Length);
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.ModerationDecisions.SingleAsync(row => row.SubmissionId == item.Id);
        Assert.Contains("Pending", audit.BeforeJson); Assert.Contains("Approved", audit.AfterJson);
    }
    [Fact]
    public async Task Rejection_keeps_new_location_hidden_and_preserves_record()
    {
        using var author = (await Player()).Client; using var mod = (await Player(true)).Client;
        var item = await Submit(author); var rejected = await Body<SubmissionResponse>(await Decide(mod, item.Id, ModerationAction.Reject));
        Assert.Equal(SubmissionState.Rejected, rejected.State); Assert.Single(rejected.Decisions);
        Assert.Equal(HttpStatusCode.NotFound, (await author.GetAsync($"/api/locations/{item.LocationId}")).StatusCode);
        using var scope = Factory.Services.CreateScope(); Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Locations.AnyAsync(row => row.Id == item.LocationId));
    }
    [Fact]
    public async Task Correction_is_atomic_and_stale_proposals_are_not_overwritten()
    {
        using var author = (await Player()).Client; using var second = (await Player()).Client; using var mod = (await Player(true)).Client;
        var original = await Approve(author, mod);
        var first = await Submit(author, Correction(original)); var stale = await Submit(second, Correction(original) with { Name = "Другой вариант названия" });
        var before = await author.GetFromJsonAsync<LocationResponse>($"/api/locations/{original.LocationId}"); Assert.Equal(original.Proposed.Name, before!.Name);
        Assert.Equal(original.Proposed.Name, first.Original!.Name);
        await Body<SubmissionResponse>(await Decide(mod, first.Id));
        var after = await author.GetFromJsonAsync<LocationResponse>($"/api/locations/{original.LocationId}"); Assert.Equal(first.Proposed.Name, after!.Name); Assert.Equal(3, after.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(mod, stale.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Propose(second, Correction(original) with { Name = "Третья версия названия" })).StatusCode);
        var rejected = await Body<SubmissionResponse>(await Decide(mod, stale.Id, ModerationAction.Reject)); Assert.Equal(SubmissionState.Rejected, rejected.State);
        Assert.Equal(after.Name, (await author.GetFromJsonAsync<LocationResponse>($"/api/locations/{original.LocationId}"))!.Name);
    }
    [Fact]
    public async Task Submission_replay_duplicate_and_changed_payload_have_distinct_outcomes()
    {
        using var author = (await Player()).Client; var request = New(); var key = Guid.NewGuid().ToString("N");
        var first = await Body<SubmissionResponse>(await Propose(author, request, key), HttpStatusCode.Created);
        var replay = await Body<SubmissionResponse>(await Propose(author, request, key), HttpStatusCode.Created); Assert.Equal(first.Id, replay.Id);
        var duplicate = await Propose(author, request); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var json = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync()); Assert.Equal(first.Id, json.RootElement.GetProperty("existingSubmissionId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await Propose(author, request with { Name = "Изменённое имя" }, key)).StatusCode);
    }
    [Fact]
    public async Task Exact_duplicate_from_different_authors_can_only_be_approved_once()
    {
        using var first = (await Player()).Client; using var second = (await Player()).Client; using var mod1 = (await Player(true)).Client; using var mod2 = (await Player(true)).Client;
        var request = New(); var a = await Submit(first, request); var b = await Submit(second, request);
        var results = await Task.WhenAll(Decide(mod1, a.Id), Decide(mod2, b.Id));
        Assert.Single(results, result => result.StatusCode == HttpStatusCode.OK); Assert.Single(results, result => result.StatusCode == HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task Concurrent_decisions_have_one_winner_and_retry_replays_same_audit()
    {
        using var author = (await Player()).Client; using var mod1 = (await Player(true)).Client; using var mod2 = (await Player(true)).Client;
        var item = await Submit(author); var key = Guid.NewGuid().ToString("N");
        var results = await Task.WhenAll(Decide(mod1, item.Id, key: key), Decide(mod2, item.Id, ModerationAction.Reject));
        Assert.Single(results, result => result.StatusCode == HttpStatusCode.OK); Assert.Single(results, result => result.StatusCode == HttpStatusCode.Conflict);
        var replay = await Decide(mod1, item.Id, key: key); Assert.Equal(results[0].StatusCode, replay.StatusCode);
        using var original = JsonDocument.Parse(await results[0].Content.ReadAsStringAsync());
        using var repeated = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.True(JsonElement.DeepEquals(original.RootElement, repeated.RootElement));
        using var scope = Factory.Services.CreateScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().ModerationDecisions.CountAsync(row => row.SubmissionId == item.Id));
    }
    [Fact]
    public async Task Roles_are_checked_fresh_for_old_cookies_and_replayed_decisions()
    {
        var actor = await Player(); using var mod = actor.Client; using var guest = Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/moderation/submissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.GetAsync("/api/moderation/submissions")).StatusCode);
        var item = await Submit(mod);
        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(mod, item.Id)).StatusCode);
        await ModeratorAccess.ChangeRole(Factory.Services, actor.Id, true);
        Assert.Equal(HttpStatusCode.OK, (await mod.GetAsync("/api/moderation/submissions")).StatusCode);
        using var session = JsonDocument.Parse(await mod.GetStringAsync("/api/auth/session")); Assert.True(session.RootElement.GetProperty("user").GetProperty("isModerator").GetBoolean());
        var key = Guid.NewGuid().ToString("N"); await Body<SubmissionResponse>(await Decide(mod, item.Id, key: key));
        await ModeratorAccess.ChangeRole(Factory.Services, actor.Id, false);
        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(mod, item.Id, key: key)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.GetAsync("/api/moderation/reports")).StatusCode);
    }
    [Fact]
    public async Task Reports_are_private_deduplicated_and_decisions_remain_visible_to_author()
    {
        using var author = (await Player()).Client; using var other = (await Player()).Client; using var mod = (await Player(true)).Client;
        var report = await Report(author, LocationSeed.WhiteBridgeId);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/content-reports/{report.Id}")).StatusCode);
        var duplicate = await Send(author, "/api/content-reports", new ReportRequest(ReportReason.Inaccurate, LocationId: LocationSeed.WhiteBridgeId));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); Assert.Contains(report.Id.ToString(), await duplicate.Content.ReadAsStringAsync());
        var closed = await Body<ReportResponse>(await Decide(mod, report.Id, ModerationAction.Dismiss, report: true));
        Assert.Equal(ReportState.Dismissed, closed.State); Assert.Single(closed.Decisions);
        Assert.Equal(HttpStatusCode.OK, (await author.GetAsync($"/api/content-reports/{report.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(mod, report.Id, ModerationAction.Resolve, report: true)).StatusCode);
        var mine = await author.GetFromJsonAsync<ReviewPage<ReportResponse>>("/api/me/reports", MutationResult.JsonOptions); Assert.Contains(mine!.Items, row => row.Id == report.Id);
        Assert.DoesNotContain("email", await author.GetStringAsync($"/api/content-reports/{report.Id}"), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Report_replay_is_safe_and_resolve_does_not_remove_public_content()
    {
        using var author = (await Player()).Client; using var mod = (await Player(true)).Client; var key = Guid.NewGuid().ToString("N");
        var request = new ReportRequest(ReportReason.Other, LocationId: LocationSeed.WhiteBridgeId);
        var a = await Body<ReportResponse>(await Send(author, "/api/content-reports", request, key), HttpStatusCode.Created);
        var b = await Body<ReportResponse>(await Send(author, "/api/content-reports", request, key), HttpStatusCode.Created); Assert.Equal(a.Id, b.Id);
        await Body<ReportResponse>(await Decide(mod, a.Id, ModerationAction.Resolve, report: true));
        Assert.Equal(HttpStatusCode.OK, (await author.GetAsync($"/api/locations/{LocationSeed.WhiteBridgeId}")).StatusCode);
    }
    [Fact]
    public async Task Removing_location_cancels_games_and_preserves_participation_without_private_reason_leaks()
    {
        using var owner = (await Player()).Client; using var player = (await Player()).Client; using var mod = (await Player(true)).Client; using var guest = Browser();
        var place = await Approve(owner, mod); var game = await Game(owner, place.LocationId);
        await Body<SessionResponse>(await Send(player, $"/api/sessions/{game.Id}/join", new { }));
        var report = await Report(player, place.LocationId);
        var closed = await Body<ReportResponse>(await Decide(mod, report.Id, ModerationAction.RemoveContent, report: true, reason: "Конфиденциальное объяснение модератора"));
        Assert.Equal(1, closed.Decisions.Single().AffectedSessions);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/locations/{place.LocationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/sessions/{game.Id}")).StatusCode);
        var history = await player.GetFromJsonAsync<SessionResponse>($"/api/sessions/{game.Id}", MutationResult.JsonOptions);
        Assert.Equal("Cancelled", history!.Status); Assert.True(history.IsParticipating); Assert.DoesNotContain("Конфиденциальное", history.CancellationReason);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(player, $"/api/sessions/{game.Id}/join", new { })).StatusCode);
        await Body<SessionResponse>(await Send(player, $"/api/sessions/{game.Id}/leave", new { }));
        var past = await player.GetFromJsonAsync<SessionResponse>($"/api/sessions/{game.Id}", MutationResult.JsonOptions); Assert.False(past!.IsParticipating);
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Participations.AnyAsync(row => row.SessionId == game.Id)); Assert.True(await db.Sessions.AnyAsync(row => row.Id == game.Id));
    }
    [Fact]
    public async Task Removing_single_game_keeps_other_games_and_place_available()
    {
        using var owner = (await Player()).Client; using var mod = (await Player(true)).Client;
        var place = await Approve(owner, mod); var a = await Game(owner, place.LocationId); var b = await Game(owner, place.LocationId);
        var report = await Report(owner, sessionId: a.Id);
        var closed = await Body<ReportResponse>(await Decide(mod, report.Id, ModerationAction.RemoveContent, report: true)); Assert.Equal(1, closed.Decisions.Single().AffectedSessions);
        var removed = await owner.GetFromJsonAsync<SessionResponse>($"/api/sessions/{a.Id}", MutationResult.JsonOptions); Assert.Equal("Cancelled", removed!.Status);
        var retained = await owner.GetFromJsonAsync<SessionResponse>($"/api/sessions/{b.Id}", MutationResult.JsonOptions); Assert.Equal("Open", retained!.Status);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/locations/{place.LocationId}")).StatusCode);
    }
    [Fact]
    public async Task Removed_activity_disables_pair_and_cancels_only_matching_games_without_deleting_fk()
    {
        using var owner = (await Player()).Client; using var mod = (await Player(true)).Client;
        var place = await Approve(owner, mod); var volley = await Game(owner, place.LocationId); var basket = await Game(owner, place.LocationId, Basket);
        var correction = await Submit(owner, Correction(place) with { ActivityIds = [Basket] });
        var approved = await Body<SubmissionResponse>(await Decide(mod, correction.Id)); Assert.Equal(1, approved.Decisions.Single().AffectedSessions);
        Assert.Equal("Cancelled", (await owner.GetFromJsonAsync<SessionResponse>($"/api/sessions/{volley.Id}", MutationResult.JsonOptions))!.Status);
        Assert.Equal("Open", (await owner.GetFromJsonAsync<SessionResponse>($"/api/sessions/{basket.Id}", MutationResult.JsonOptions))!.Status);
        var publicPlace = await owner.GetFromJsonAsync<LocationResponse>($"/api/locations/{place.LocationId}"); Assert.Equal(Basket, Assert.Single(publicPlace!.Activities).Id);
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.LocationActivities.SingleAsync(row => row.LocationId == place.LocationId && row.ActivityId == Volley)).IsEnabled);
        Assert.True(await db.Sessions.AnyAsync(row => row.Id == volley.Id));
    }
    [Fact]
    public async Task Join_and_creation_wait_for_location_lock_then_observe_removal()
    {
        using var owner = (await Player()).Client; using var joining = (await Player()).Client; using var creating = (await Player()).Client; using var mod = (await Player(true)).Client;
        var place = await Approve(owner, mod); var game = await Game(owner, place.LocationId);
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString()); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var query = new NpgsqlCommand("SELECT * FROM locations WHERE \"Id\"=@id FOR UPDATE", connection, transaction)) { query.Parameters.AddWithValue("id", place.LocationId); await query.ExecuteNonQueryAsync(); }
        var join = Send(joining, $"/api/sessions/{game.Id}/join", new { });
        var create = Send(creating, "/api/sessions", new CreateSessionRequest(place.LocationId, Volley, SessionKind.Immediate, SkillLevel.Beginner, 2, EquipmentStatus.NotSpecified));
        // Wait for actual database lock waiters, rather than a timing-only sleep assertion.
        var waiting = false;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using (var clear = new NpgsqlCommand("SELECT pg_stat_clear_snapshot()", connection, transaction)) await clear.ExecuteNonQueryAsync();
            await using var query = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%FROM locations%'", connection, transaction);
            if ((long)(await query.ExecuteScalarAsync())! >= 2) { waiting = true; break; }
            await Task.Delay(20);
        }
        Assert.True(waiting); Assert.False(join.IsCompleted); Assert.False(create.IsCompleted);
        await using (var query = new NpgsqlCommand("UPDATE locations SET \"ApprovalStatus\"='Removed', \"Revision\"=\"Revision\"+1 WHERE \"Id\"=@id", connection, transaction)) { query.Parameters.AddWithValue("id", place.LocationId); await query.ExecuteNonQueryAsync(); }
        await transaction.CommitAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await join).StatusCode); Assert.Equal(HttpStatusCode.BadRequest, (await create).StatusCode);
    }
    [Fact]
    public async Task Submission_limits_count_logical_commands_not_retries()
    {
        using var author = (await Player()).Client;
        for (var i = 0; i < 10; i++) await Submit(author);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Propose(author, New())).StatusCode);
    }
    [Fact]
    public async Task Report_limit_applies_per_account()
    {
        using var author = (await Player()).Client;
        foreach (var target in new[] { LocationSeed.WhiteBridgeId, LocationSeed.KaukysosId })
            foreach (var reason in Enum.GetValues<ReportReason>().Take(5)) await Report(author, target, reason: reason);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(author, "/api/content-reports", new ReportRequest(ReportReason.Other, LocationId: LocationSeed.WhiteBridgeId))).StatusCode);
    }
    [Fact]
    public async Task Invalid_submission_fields_and_privilege_injection_are_rejected()
    {
        using var author = (await Player()).Client; var valid = New();
        foreach (var request in new[] { valid with { Name = "x" }, valid with { Address = null }, valid with { Latitude = null }, valid with { Longitude = 181 }, valid with { ActivityIds = [] }, valid with { ActivityIds = [Guid.NewGuid()] }, valid with { ActivityIds = [Volley, Volley] }, valid with { SourceUrl = "javascript:alert(1)" }, valid with { SourceUrl = "https://user:pass@example.test" }, valid with { Description = new string('x', 4001) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await Propose(author, request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(author, "/api/location-submissions", new { valid.Name, valid.Address, valid.Latitude, valid.Longitude, valid.ActivityIds, state = "Approved", isModerator = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await author.GetAsync("/api/moderation/submissions")).StatusCode);
    }
    [Fact]
    public async Task Invalid_reports_decisions_and_pagination_are_rejected()
    {
        using var author = (await Player()).Client; using var mod = (await Player(true)).Client; var item = await Submit(author);
        foreach (var request in new[] { new ReportRequest(ReportReason.Other), new ReportRequest(ReportReason.Other, LocationId: LocationSeed.WhiteBridgeId, SessionId: Guid.NewGuid()), new ReportRequest(ReportReason.Other, new string('x', 2001), LocationSeed.WhiteBridgeId) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(author, "/api/content-reports", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(author, "/api/content-reports", new { reason = "Unrecognized", locationId = LocationSeed.WhiteBridgeId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(author, "/api/content-reports", new ReportRequest(ReportReason.Other, LocationId: item.LocationId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(mod, item.Id, reason: "коротко")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(mod, item.Id, ModerationAction.RemoveContent)).StatusCode);
        foreach (var path in new[] { "/api/me/submissions?page=0", "/api/me/reports?pageSize=51", "/api/moderation/submissions?state=999", "/api/moderation/reports?pageSize=0" })
            Assert.Equal(HttpStatusCode.BadRequest, (await mod.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Writes_require_csrf_and_idempotency_key()
    {
        using var author = (await Player()).Client;
        Assert.Equal(HttpStatusCode.BadRequest, (await author.PostAsJsonAsync("/api/location-submissions", New(), MutationResult.JsonOptions)).StatusCode);
        using var token = JsonDocument.Parse(await author.GetStringAsync("/api/auth/csrf"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/location-submissions") { Content = JsonContent.Create(New(), options: MutationResult.JsonOptions) };
        request.Headers.Add("X-CSRF-TOKEN", token.RootElement.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await author.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Database_rejects_reports_with_zero_or_two_targets(bool hasLocation, bool hasSession)
    {
        var actor = await Player(); using var author = actor.Client;
        var game = await Game(author, LocationSeed.WhiteBridgeId);
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.ContentReports.Add(new ContentReport { Id = Guid.NewGuid(), AuthorId = actor.Id, LocationId = hasLocation ? LocationSeed.WhiteBridgeId : null,
            SessionId = hasSession ? game.Id : null, Details = "", Reason = ReportReason.Other, State = ReportState.Open, SubmittedAt = DateTimeOffset.UtcNow });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [Fact]
    public async Task Database_prevents_dangling_report_targets()
    {
        var actor = await Player(); using var author = actor.Client;
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.ContentReports.Add(new ContentReport { Id = Guid.NewGuid(), AuthorId = actor.Id, LocationId = Guid.NewGuid(), Details = "", Reason = ReportReason.Other, State = ReportState.Open, SubmittedAt = DateTimeOffset.UtcNow });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [Fact]
    public async Task Concurrent_duplicate_reports_create_exactly_one_record()
    {
        using var author = (await Player()).Client;
        var request = new ReportRequest(ReportReason.Spam, LocationId: LocationSeed.WhiteBridgeId);
        var results = await Task.WhenAll(Send(author, "/api/content-reports", request), Send(author, "/api/content-reports", request));
        Assert.Single(results, result => result.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, result => result.StatusCode == HttpStatusCode.Conflict);
        var list = await author.GetFromJsonAsync<ReviewPage<ReportResponse>>("/api/me/reports", MutationResult.JsonOptions); Assert.Single(list!.Items);
    }
    [Fact]
    public async Task Reenabled_activity_reuses_historical_pair_and_does_not_revive_cancelled_game()
    {
        using var author = (await Player()).Client; using var mod = (await Player(true)).Client;
        var place = await Approve(author, mod); var game = await Game(author, place.LocationId);
        var disable = await Submit(author, Correction(place) with { ActivityIds = [Basket] });
        var changed = await Body<SubmissionResponse>(await Decide(mod, disable.Id));
        var enable = await Submit(author, Correction(changed) with { ActivityIds = [Volley, Basket] });
        await Body<SubmissionResponse>(await Decide(mod, enable.Id));
        var old = await author.GetFromJsonAsync<SessionResponse>($"/api/sessions/{game.Id}", MutationResult.JsonOptions); Assert.Equal("Cancelled", old!.Status);
        var fresh = await Game(author, place.LocationId); Assert.Equal("Open", fresh.Status);
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.LocationActivities.CountAsync(row => row.LocationId == place.LocationId));
    }
}
