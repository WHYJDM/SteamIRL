using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Accounts;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class SessionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid Volleyball = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private HttpClient Browser(string? applicationName = null) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        if (applicationName is not null) builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Database"] = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STEAMIRL_TEST_DATABASE")) { ApplicationName = applicationName }.ConnectionString }));
    }).CreateClient();

    private async Task<HttpClient> Player(string? applicationName = null)
    {
        var client = Browser(applicationName);
        using var response = await Send(client, "/api/auth/register", HttpMethod.Post,
            new { email = $"session-{Guid.NewGuid():N}@example.test", password = TestCredentials.Password, displayName = "Тест игр" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return client;
    }
    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, HttpMethod method, object? body = null, string? key = null)
    {
        using var tokenResponse = await client.GetAsync("/api/auth/csrf");
        tokenResponse.EnsureSuccessStatusCode();
        using var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", token.RootElement.GetProperty("token").GetString());
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        if (body is not null) request.Content = JsonContent.Create(body, options: MutationResult.JsonOptions);
        return await client.SendAsync(request);
    }
    private static CreateSessionRequest Planned(int capacity = 2) => new(LocationSeed.WhiteBridgeId, Volleyball, SessionKind.Planned,
        SkillLevel.Intermediate, capacity, EquipmentStatus.BallAvailable, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2));
    private static CreateSessionRequest Immediate(int capacity = 2) => new(LocationSeed.WhiteBridgeId, Volleyball, SessionKind.Immediate,
        SkillLevel.Intermediate, capacity, DurationMinutes: 60);
    private static async Task<SessionResponse> Body(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<SessionResponse>(MutationResult.JsonOptions))!;
    private static async Task<SessionResponse> Create(HttpClient owner, CreateSessionRequest? request = null)
    {
        using var response = await Send(owner, "/api/sessions", HttpMethod.Post, request ?? Planned());
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Body(response);
    }
    private static Task<HttpResponseMessage> Act(HttpClient client, Guid id, string action, object? body = null, string? key = null) =>
        Send(client, $"/api/sessions/{id}/{action}", action == "capacity" ? HttpMethod.Patch : HttpMethod.Post, body, key);
    private static Task<SessionResponse?> Get(HttpClient client, Guid id) => client.GetFromJsonAsync<SessionResponse>($"/api/sessions/{id}", MutationResult.JsonOptions);
    private static async Task<List<SessionResponse>> All(HttpClient client, string query = "", bool mine = false)
    {
        var result = new List<SessionResponse>();
        for (var page = 1; page < 100; page++)
        {
            var data = (await client.GetFromJsonAsync<SessionPage>($"/api/{(mine ? "me/" : "")}sessions?{query}&page={page}&pageSize=100", MutationResult.JsonOptions))!;
            result.AddRange(data.Items); if (!data.HasMore) return result;
        }
        throw new InvalidOperationException("Unexpected test dataset size.");
    }

    [Fact]
    public async Task Planned_creation_connects_place_activity_profile_and_public_search()
    {
        using var owner = await Player(); using var guest = Browser();
        var game = await Create(owner);
        Assert.True(game.IsOrganiser); Assert.False(game.IsJoinable);
        Assert.Equal("Тест игр", game.OrganiserName); Assert.Equal("Open", game.Status);
        Assert.Equal(2, game.RemainingPlaces); Assert.Equal(LocationSeed.WhiteBridgeId, game.LocationId);
        var publicGame = (await Get(guest, game.Id))!;
        Assert.True(publicGame.IsJoinable); Assert.False(publicGame.IsOrganiser);
        var page = await All(guest, $"activityId={Volleyball}&locationId={game.LocationId}&level=Intermediate&joinableOnly=true");
        Assert.Contains(page, item => item.Id == game.Id);
        Assert.All(page, item => { Assert.Equal(Volleyball, item.ActivityId); Assert.Equal(SkillLevel.Intermediate, item.SkillLevel); Assert.True(item.RemainingPlaces > 0); });
        using var raw = await guest.GetAsync($"/api/sessions/{game.Id}");
        Assert.DoesNotContain("email", await raw.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task Invalid_capacity_is_rejected(int capacity)
    {
        using var owner = await Player();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, "/api/sessions", HttpMethod.Post, Planned(capacity))).StatusCode);
    }

    [Fact]
    public async Task Invalid_times_places_and_unsupported_sports_are_rejected()
    {
        using var owner = await Player();
        var planned = Planned();
        foreach (var bad in new[] { planned with { StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1) }, planned with { EndsAt = planned.StartsAt },
            planned with { StartsAt = null }, planned with { LocationId = Guid.NewGuid() },
            planned with { ActivityId = Guid.Parse("33333333-3333-4333-8333-333333333333") },
            Immediate() with { DurationMinutes = 14 }, Immediate() with { DurationMinutes = 241 }, Immediate() with { StartsAt = DateTimeOffset.UtcNow } })
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, "/api/sessions", HttpMethod.Post, bad)).StatusCode);
    }

    [Fact]
    public async Task Repeated_creation_returns_same_game_and_rejects_changed_payload()
    {
        using var owner = await Player(); var key = Guid.NewGuid().ToString("N"); var request = Planned();
        var first = await Send(owner, "/api/sessions", HttpMethod.Post, request, key);
        var repeat = await Send(owner, "/api/sessions", HttpMethod.Post, request, key);
        Assert.Equal(HttpStatusCode.Created, repeat.StatusCode);
        Assert.Equal((await Body(first)).Id, (await Body(repeat)).Id);
        Assert.Equal(first.Headers.Location, repeat.Headers.Location);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(owner, "/api/sessions", HttpMethod.Post, request with { Capacity = 3 }, key)).StatusCode);
        Assert.Single(await All(owner, mine: true));
    }

    [Fact]
    public async Task Concurrent_same_key_creation_commits_only_once()
    {
        using var owner = await Player(); var key = Guid.NewGuid().ToString("N"); var request = Planned();
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Send(owner, "/api/sessions", HttpMethod.Post, request, key)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Single((await Task.WhenAll(responses.Select(Body))).Select(game => game.Id).Distinct());
        Assert.Single(await All(owner, mine: true));
    }

    [Fact]
    public async Task Final_slot_has_exactly_one_winner_among_twenty_players()
    {
        using var owner = await Player(); var game = await Create(owner, Planned(1));
        var players = new List<HttpClient>();
        try
        {
            for (var index = 0; index < 20; index++) players.Add(await Player());
            var results = await Task.WhenAll(players.Select(player => Act(player, game.Id, "join")));
            Assert.Single(results, result => result.StatusCode == HttpStatusCode.OK);
            Assert.Equal(19, results.Count(result => result.StatusCode == HttpStatusCode.Conflict));
            var full = (await Get(owner, game.Id))!;
            Assert.Equal(1, full.ParticipantCount); Assert.Equal(0, full.RemainingPlaces); Assert.Equal("Full", full.Status);
        }
        finally { foreach (var player in players) player.Dispose(); }
    }

    [Fact]
    public async Task Repeated_join_leave_and_old_leave_replay_do_not_change_membership_twice()
    {
        using var owner = await Player(); using var player = await Player(); var game = await Create(owner, Planned(1));
        var joinKey = Guid.NewGuid().ToString("N"); var leaveKey = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, (await Act(player, game.Id, "join", key: joinKey)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Act(player, game.Id, "join")).StatusCode);
        Assert.Equal(1, (await Get(owner, game.Id))!.ParticipantCount);
        Assert.Equal(HttpStatusCode.OK, (await Act(player, game.Id, "leave", key: leaveKey)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Act(player, game.Id, "leave")).StatusCode);
        Assert.Equal(1, (await Get(owner, game.Id))!.RemainingPlaces);
        Assert.Equal(HttpStatusCode.OK, (await Act(player, game.Id, "join")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Act(player, game.Id, "leave", key: leaveKey)).StatusCode);
        Assert.True((await Get(player, game.Id))!.IsParticipating);
    }

    [Fact]
    public async Task Capacity_cannot_drop_below_responses_and_full_game_reopens()
    {
        using var owner = await Player(); using var first = await Player(); using var second = await Player();
        var game = await Create(owner, Planned(2));
        await Act(first, game.Id, "join"); await Act(second, game.Id, "join");
        Assert.Equal(HttpStatusCode.BadRequest, (await Act(owner, game.Id, "capacity", new { capacity = 1 })).StatusCode);
        var increased = await Body(await Act(owner, game.Id, "capacity", new { capacity = 3 }));
        Assert.Equal("Open", increased.Status); Assert.Equal(1, increased.RemainingPlaces);
        await Act(owner, game.Id, "capacity", new { capacity = 2 });
        await Act(first, game.Id, "leave");
        Assert.Equal("Open", (await Get(owner, game.Id))!.Status);
    }

    [Fact]
    public async Task Only_owner_can_manage_and_organiser_cannot_join()
    {
        using var owner = await Player(); using var other = await Player(); var game = await Create(owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await Act(other, game.Id, "capacity", new { capacity = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Act(other, game.Id, "cancel")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Act(other, game.Id, "leave")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Act(owner, game.Id, "join")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Act(owner, game.Id, "leave")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Act(owner, game.Id, "cancel")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Act(other, game.Id, "join")).StatusCode);
        Assert.Equal("Cancelled", (await Get(other, game.Id))!.Status);
        Assert.DoesNotContain(await All(other), item => item.Id == game.Id);
    }

    [Fact]
    public async Task Immediate_duplicates_include_full_sessions_and_hourly_limit_includes_cancelled()
    {
        using var owner = await Player(); using var player = await Player();
        var game = await Create(owner, Immediate(1)); await Act(player, game.Id, "join");
        var duplicate = await Send(owner, "/api/sessions", HttpMethod.Post, Immediate());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var body = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(game.Id, body.GetProperty("existingSessionId").GetGuid());
        await Act(owner, game.Id, "cancel");
        for (var index = 0; index < 2; index++) { var next = await Create(owner, Immediate()); await Act(owner, next.Id, "cancel"); }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(owner, "/api/sessions", HttpMethod.Post, Immediate())).StatusCode);
    }

    [Fact]
    public async Task Concurrent_immediate_requests_with_different_keys_create_only_one_invitation()
    {
        using var owner = await Player();
        var responses = await Task.WhenAll(Send(owner, "/api/sessions", HttpMethod.Post, Immediate()), Send(owner, "/api/sessions", HttpMethod.Post, Immediate()));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Authentication_csrf_and_idempotency_key_are_required()
    {
        using var guest = Browser(); using var owner = await Player(); var game = await Create(owner);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/sessions", Planned(), MutationResult.JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsync($"/api/sessions/{game.Id}/join", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/me/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/sessions", Planned(), MutationResult.JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, "/api/sessions", HttpMethod.Post, Planned(), key: "")).StatusCode);
    }

    [Fact]
    public async Task History_includes_own_cancelled_games_and_withdrawn_participation_without_other_users_games()
    {
        using var owner = await Player(); using var player = await Player(); using var stranger = await Player();
        var game = await Create(owner); await Act(player, game.Id, "join"); await Act(player, game.Id, "leave"); await Act(owner, game.Id, "cancel");
        Assert.Equal(game.Id, Assert.Single(await All(owner, mine: true)).Id);
        var history = Assert.Single(await All(player, mine: true));
        Assert.Equal(game.Id, history.Id); Assert.False(history.IsParticipating); Assert.Equal("Cancelled", history.Status);
        Assert.Empty(await All(stranger, mine: true));
    }

    [Fact]
    public async Task Expiry_is_enforced_without_background_job()
    {
        using var owner = await Player(); using var player = await Player(); var game = await Create(owner);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Sessions.Where(session => session.Id == game.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(session => session.StartsAt, DateTimeOffset.UtcNow.AddHours(-1)).SetProperty(session => session.EndsAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal("Expired", (await Get(player, game.Id))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Act(player, game.Id, "join")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Act(owner, game.Id, "capacity", new { capacity = 3 })).StatusCode);
        Assert.DoesNotContain(await All(player), item => item.Id == game.Id);
    }

    [Fact]
    public async Task Join_waiting_for_lock_rechecks_expiry_after_the_lock_is_released()
    {
        var applicationName = "steamirl-wait-" + Guid.NewGuid().ToString("N");
        using var owner = await Player(); using var player = await Player(applicationName); var game = await Create(owner);
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("STEAMIRL_TEST_DATABASE"));
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand("SELECT 1 FROM sessions WHERE \"Id\" = @id FOR UPDATE", connection, transaction))
        { lockCommand.Parameters.AddWithValue("id", game.Id); await lockCommand.ExecuteScalarAsync(); }
        var pending = Act(player, game.Id, "join");
        var waiting = false;
        for (var attempt = 0; attempt < 100 && !waiting; attempt++)
        {
            await using var check = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name = @name AND wait_event_type = 'Lock')", connection, transaction);
            check.Parameters.AddWithValue("name", applicationName); waiting = (bool)(await check.ExecuteScalarAsync())!;
            if (!waiting) await Task.Delay(30);
        }
        Assert.True(waiting, "Join did not wait on the held session lock.");
        await using (var expire = new NpgsqlCommand("UPDATE sessions SET \"StartsAt\"=clock_timestamp()-interval '1 hour', \"EndsAt\"=clock_timestamp()-interval '1 second' WHERE \"Id\"=@id", connection, transaction))
        { expire.Parameters.AddWithValue("id", game.Id); await expire.ExecuteNonQueryAsync(); }
        await transaction.CommitAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await pending).StatusCode);
        Assert.Equal(0, (await Get(owner, game.Id))!.ParticipantCount);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("level=Unknown")]
    [InlineData("from=2027-01-02T18:00:00Z&to=2027-01-01T18:00:00Z")]
    [InlineData("from=2027-01-02T18:00:00")]
    public async Task Invalid_search_parameters_return_400(string query)
    {
        using var guest = Browser(); Assert.Equal(HttpStatusCode.BadRequest, (await guest.GetAsync($"/api/sessions?{query}")).StatusCode);
    }

    [Fact]
    public async Task Time_intersection_and_free_slot_filter_work_for_full_games()
    {
        using var owner = await Player(); using var player = await Player();
        var game = await Create(owner, Planned(1)); await Act(player, game.Id, "join");
        var from = Uri.EscapeDataString(game.StartsAt.AddMinutes(10).ToString("O"));
        var to = Uri.EscapeDataString(game.EndsAt.AddMinutes(10).ToString("O"));
        var normal = await All(player, $"from={from}&to={to}");
        Assert.Contains(normal, item => item.Id == game.Id && item.Status == "Full");
        Assert.All(normal, item => { Assert.True(item.EndsAt > game.StartsAt.AddMinutes(10)); Assert.True(item.StartsAt < game.EndsAt.AddMinutes(10)); });
        Assert.DoesNotContain(await All(player, $"from={from}&to={to}&joinableOnly=true"), item => item.Id == game.Id);
        var after = Uri.EscapeDataString(game.EndsAt.ToString("O"));
        Assert.DoesNotContain(await All(player, $"from={after}"), item => item.Id == game.Id);
    }

    [Fact]
    public async Task Cancellation_racing_join_never_allows_another_join_after_cancel_commits()
    {
        using var owner = await Player(); using var player = await Player(); using var later = await Player(); var game = await Create(owner, Planned(1));
        var results = await Task.WhenAll(Act(owner, game.Id, "cancel"), Act(player, game.Id, "join"));
        Assert.Equal(HttpStatusCode.OK, results[0].StatusCode);
        Assert.True(results[1].StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, (await Act(later, game.Id, "join")).StatusCode);
        var final = (await Get(owner, game.Id))!; Assert.Equal("Cancelled", final.Status); Assert.InRange(final.ParticipantCount, 0, 1);
    }

    [Fact]
    public async Task Idempotency_key_cannot_be_reused_for_another_operation_but_expires_after_twenty_four_hours()
    {
        using var owner = await Player(); var request = Planned(); var key = Guid.NewGuid().ToString("N");
        var game = await Body(await Send(owner, "/api/sessions", HttpMethod.Post, request, key));
        Assert.Equal(HttpStatusCode.Conflict, (await Act(owner, game.Id, "cancel", key: key)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.IdempotencyRequests.Where(item => item.UserId == game.OrganiserId && item.Key == key)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var createdAgain = await Send(owner, "/api/sessions", HttpMethod.Post, request, key);
        Assert.Equal(HttpStatusCode.Created, createdAgain.StatusCode); Assert.NotEqual(game.Id, (await Body(createdAgain)).Id);
    }

    [Fact]
    public async Task Unknown_or_unapproved_location_cannot_be_used_and_history_owner_can_still_cancel()
    {
        using var owner = await Player(); using var other = await Player();
        var me = (await owner.GetFromJsonAsync<MeResponse>("/api/me"))!;
        var placeId = Guid.NewGuid(); var gameId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Locations.Add(new Location { Id = placeId, Name = "Private session test place", Description = "Test", Address = "Test", Latitude = 54, Longitude = 25,
            ApprovalStatus = ApprovalStatus.Pending, UpdatedAt = DateTimeOffset.UtcNow, SourceUrl = "https://example.test", SourceCheckedAt = DateTimeOffset.UtcNow, AccessNotes = "Test", CoordinateNote = "Test" });
        db.LocationActivities.Add(new LocationActivity { LocationId = placeId, ActivityId = Volleyball });
        db.Sessions.Add(new GameSession { Id = gameId, LocationId = placeId, ActivityId = Volleyball, OrganiserId = me.Id, Kind = SessionKind.Planned,
            SkillLevel = SkillLevel.Intermediate, Capacity = 1, StartsAt = DateTimeOffset.UtcNow.AddHours(1), EndsAt = DateTimeOffset.UtcNow.AddHours(2), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, "/api/sessions", HttpMethod.Post, Planned() with { LocationId = placeId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/sessions/{gameId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Act(other, gameId, "join")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Act(other, gameId, "leave")).StatusCode);
        Assert.False((await Get(owner, gameId))!.IsJoinable);
        Assert.Equal(HttpStatusCode.OK, (await Act(owner, gameId, "cancel")).StatusCode);
    }

    [Fact]
    public async Task Foreign_keys_and_uniqueness_protect_supported_pairs_and_participation()
    {
        using var owner = await Player(); using var player = await Player(); var game = await Create(owner);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Sessions.Add(new GameSession { Id = Guid.NewGuid(), LocationId = LocationSeed.WhiteBridgeId,
                ActivityId = Guid.Parse("33333333-3333-4333-8333-333333333333"), OrganiserId = game.OrganiserId,
                Kind = SessionKind.Planned, SkillLevel = SkillLevel.Intermediate, Capacity = 2, StartsAt = game.StartsAt, EndsAt = game.EndsAt, CreatedAt = game.ServerNow, UpdatedAt = game.ServerNow });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        await Act(player, game.Id, "join"); var playerId = (await player.GetFromJsonAsync<MeResponse>("/api/me"))!.Id;
        await using var duplicateScope = factory.Services.CreateAsyncScope(); var duplicateDb = duplicateScope.ServiceProvider.GetRequiredService<AppDbContext>();
        duplicateDb.Participations.Add(new Participation { SessionId = game.Id, UserId = playerId, JoinedAt = DateTimeOffset.UtcNow });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
    }

    [Fact]
    public async Task Planned_json_timestamps_require_an_explicit_offset()
    {
        using var owner = await Player();
        var response = await Send(owner, "/api/sessions", HttpMethod.Post, new { locationId = LocationSeed.WhiteBridgeId, activityId = Volleyball,
            kind = "Planned", skillLevel = "Intermediate", capacity = 1, startsAt = "2027-01-01T18:00:00", endsAt = "2027-01-01T19:00:00" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var nonIso = await Send(owner, "/api/sessions", HttpMethod.Post, new { locationId = LocationSeed.WhiteBridgeId, activityId = Volleyball,
            kind = "Planned", skillLevel = "Intermediate", capacity = 1, startsAt = "01/01/2027 18:00+02:00", endsAt = "01/01/2027 19:00+02:00" });
        Assert.Equal(HttpStatusCode.BadRequest, nonIso.StatusCode);
        var wrongType = await Send(owner, "/api/sessions", HttpMethod.Post, new { locationId = LocationSeed.WhiteBridgeId, activityId = Volleyball,
            kind = "Planned", skillLevel = "Intermediate", capacity = 1, startsAt = 1234, endsAt = "2027-01-01T19:00:00Z" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
    }
}
