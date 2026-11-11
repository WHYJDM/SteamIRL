using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SteamIRL.Api.Data;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string connectionString;
    public ApiFactory() : this(Environment.GetEnvironmentVariable("STEAMIRL_TEST_DATABASE")
        ?? throw new InvalidOperationException("Set STEAMIRL_TEST_DATABASE to a dedicated PostgreSQL database named steamirl_test*. See docs/development.md.")) { }
    internal ApiFactory(string connectionString) => this.connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, config) =>
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connectionString, ["Authentication:AllowInsecureCookies"] = "true", ["Testing:SkipDatabaseRoleCheck"] = "true" }));

    public async Task InitializeAsync()
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (settings.Database is null || !settings.Database.StartsWith("steamirl_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Integration tests require a dedicated database named steamirl_test*.");

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        // Re-running bootstrap must not duplicate catalogue entries.
        await db.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}
