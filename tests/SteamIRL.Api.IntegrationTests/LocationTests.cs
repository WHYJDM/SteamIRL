using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Locations;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class LocationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string Volleyball = "11111111-1111-4111-8111-111111111111";
    private const string Basketball = "22222222-2222-4222-8222-222222222222";
    private static Guid HiddenId(ApprovalStatus status) => Guid.Parse($"bbbb000{(int)status}-1111-4111-8111-111111111111");

    public async Task InitializeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var status in new[] { ApprovalStatus.Pending, ApprovalStatus.Rejected, ApprovalStatus.Removed })
        {
            var id = HiddenId(status);
            if (await db.Locations.AnyAsync(location => location.Id == id)) continue;
            db.Locations.Add(new Location { Id = id, Name = $"Test {status}", Description = "Test-only record", Address = "Test",
                Latitude = 54.69m, Longitude = 25.27m, ApprovalStatus = status, UpdatedAt = DateTimeOffset.UtcNow,
                SourceUrl = "https://example.test/", SourceCheckedAt = DateTimeOffset.UtcNow, CoordinateNote = "Test", AccessNotes = "Test" });
            db.LocationActivities.Add(new LocationActivity { LocationId = id, ActivityId = Guid.Parse(Volleyball) });
        }
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Public_list_only_returns_approved_locations_and_supported_activities()
    {
        using var browser = factory.CreateClient();
        var page = (await browser.GetFromJsonAsync<LocationPage>($"/api/locations?activityId={Volleyball}"))!;
        Assert.Equal(2, page.Items.Length);
        Assert.Contains(page.Items, item => item.Id == LocationSeed.WhiteBridgeId);
        Assert.Contains(page.Items, item => item.Id == LocationSeed.KaukysosId);
        Assert.All(page.Items, item => Assert.Contains(item.Activities, activity => activity.Id == Guid.Parse(Volleyball)));
        Assert.DoesNotContain(page.Items, item => item.Name.StartsWith("Test "));
        Assert.Equal(page.Items.Length, page.Items.Select(item => item.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Rejected)]
    [InlineData(ApprovalStatus.Removed)]
    public async Task Hidden_location_is_not_disclosed_in_detail_or_map(ApprovalStatus status)
    {
        using var browser = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/locations/{HiddenId(status)}")).StatusCode);
        var page = (await browser.GetFromJsonAsync<LocationPage>("/api/locations?minLat=54&maxLat=55&minLon=25&maxLon=26"))!;
        Assert.DoesNotContain(page.Items, item => item.Id == HiddenId(status));
    }

    [Fact]
    public async Task Switching_activity_reuses_same_location_and_detail_metadata()
    {
        using var browser = factory.CreateClient();
        var volleyball = (await browser.GetFromJsonAsync<LocationPage>($"/api/locations?activityId={Volleyball}"))!;
        var basketball = (await browser.GetFromJsonAsync<LocationPage>($"/api/locations?activityId={Basketball}"))!;
        var first = Assert.Single(volleyball.Items, item => item.Id == LocationSeed.WhiteBridgeId);
        var second = Assert.Single(basketball.Items, item => item.Id == LocationSeed.WhiteBridgeId);
        var detail = (await browser.GetFromJsonAsync<LocationResponse>($"/api/locations/{first.Id}"))!;
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.UpdatedAt, detail.UpdatedAt);
        Assert.Equal(first.SourceUrl, detail.SourceUrl);
        Assert.Equal(2, detail.Activities.Length);
        Assert.False(string.IsNullOrEmpty(detail.CoordinateNote));
    }

    [Fact]
    public async Task Map_bounds_and_activity_filters_are_combined()
    {
        using var browser = factory.CreateClient();
        var page = (await browser.GetFromJsonAsync<LocationPage>($"/api/locations?activityId={Volleyball}&minLat=54.694&maxLat=54.696&minLon=25.275&maxLon=25.278"))!;
        Assert.Equal(LocationSeed.WhiteBridgeId, Assert.Single(page.Items).Id);
        var empty = (await browser.GetFromJsonAsync<LocationPage>($"/api/locations?activityId={Volleyball}&minLat=0&maxLat=1&minLon=0&maxLon=1"))!;
        Assert.Empty(empty.Items);
        Assert.False(empty.HasMore);
    }

    [Fact]
    public async Task Pagination_is_stable_bounded_and_has_no_duplicates()
    {
        using var browser = factory.CreateClient();
        var all = (await browser.GetFromJsonAsync<LocationPage>("/api/locations"))!;
        var collected = new List<Guid>();
        for (var pageNumber = 1; pageNumber <= all.Items.Length; pageNumber++)
        {
            var page = (await browser.GetFromJsonAsync<LocationPage>($"/api/locations?pageSize=1&page={pageNumber}"))!;
            collected.Add(Assert.Single(page.Items).Id);
            Assert.Equal(pageNumber < all.Items.Length, page.HasMore);
        }
        Assert.Equal(all.Items.Select(item => item.Id), collected);
        Assert.Equal(collected.Count, collected.Distinct().Count());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=100001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("minLat=54")]
    [InlineData("minLat=55&maxLat=54&minLon=25&maxLon=26")]
    [InlineData("minLat=-91&maxLat=55&minLon=25&maxLon=26")]
    [InlineData("minLat=54&maxLat=55&minLon=26&maxLon=25")]
    [InlineData("minLat=54&maxLat=55&minLon=25&maxLon=181")]
    [InlineData("minLat=NaN&maxLat=55&minLon=25&maxLon=26")]
    [InlineData("activityId=invalid")]
    public async Task Invalid_queries_return_400(string query)
    {
        using var browser = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync($"/api/locations?{query}")).StatusCode);
    }

    [Fact]
    public async Task Unknown_records_return_404_and_public_writes_are_unavailable()
    {
        using var browser = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/locations/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/locations?activityId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await browser.PostAsJsonAsync("/api/locations", new { name = "Attack" })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await browser.PatchAsJsonAsync($"/api/locations/{LocationSeed.WhiteBridgeId}", new { approvalStatus = "Approved" })).StatusCode);
    }

    [Fact]
    public async Task Database_rejects_duplicate_location_activity()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LocationActivities.Add(new LocationActivity { LocationId = LocationSeed.WhiteBridgeId, ActivityId = Guid.Parse(Volleyball) });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Database_rejects_orphan_location_activity(bool unknownLocation)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LocationActivities.Add(new LocationActivity { LocationId = unknownLocation ? Guid.NewGuid() : LocationSeed.WhiteBridgeId,
            ActivityId = unknownLocation ? Guid.Parse(Volleyball) : Guid.NewGuid() });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Theory]
    [InlineData(91, 25, ApprovalStatus.Approved)]
    [InlineData(54, 181, ApprovalStatus.Approved)]
    [InlineData(54, 25, (ApprovalStatus)99)]
    public async Task Database_rejects_invalid_coordinates_or_status(int latitude, int longitude, ApprovalStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Locations.Add(new Location { Id = Guid.NewGuid(), Name = "Invalid", Description = "Test", Address = "Test",
            Latitude = latitude, Longitude = longitude, ApprovalStatus = status, UpdatedAt = DateTimeOffset.UtcNow,
            SourceUrl = "https://example.test/", SourceCheckedAt = DateTimeOffset.UtcNow, CoordinateNote = "Test", AccessNotes = "Test" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Referenced_activity_cannot_be_deleted()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Activities.Where(activity => activity.Id == Guid.Parse(Volleyball)).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }
}
