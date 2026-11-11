using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Activities;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class CatalogueTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Catalogue_returns_four_canonical_activities_after_repeated_migration()
    {
        using var client = factory.CreateClient();
        var activities = await client.GetFromJsonAsync<ActivityResponse[]>("/api/activities");
        Assert.NotNull(activities);
        Assert.Equal(4, activities.Length);
        Assert.Equal(4, activities.Select(activity => activity.Id).Distinct().Count());
        Assert.Contains(activities, activity => activity.Slug == "volleyball" && activity.Name == "Волейбол");
    }

    [Fact]
    public async Task Database_rejects_duplicate_activity_slug()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Activities.Add(new Activity { Id = Guid.NewGuid(), Slug = "volleyball", Name = "Duplicate", Description = "Test" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Database_rejects_noncanonical_slug()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Activities.Add(new Activity { Id = Guid.NewGuid(), Slug = "Volleyball!", Name = "Invalid", Description = "Test" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_succeed_with_migrated_database(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
