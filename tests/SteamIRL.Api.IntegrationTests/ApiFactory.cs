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
    private readonly string? adminConnection;
    private readonly string? ownedDatabase;
    public string ConnectionString => connectionString;

    public ApiFactory()
    {
        var settings = TestSettings();
        ownedDatabase = $"steamirl_test_api_{Guid.NewGuid():N}";
        adminConnection = new NpgsqlConnectionStringBuilder(settings.ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        }.ConnectionString;
        settings.Database = ownedDatabase;
        connectionString = settings.ConnectionString;
    }

    internal ApiFactory(string connectionString)
    {
        ValidateTestDatabase(new NpgsqlConnectionStringBuilder(connectionString));
        this.connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, config) =>
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connectionString, ["Authentication:AllowInsecureCookies"] = "true", ["Testing:SkipDatabaseRoleCheck"] = "true" }));

    public async Task InitializeAsync()
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        ValidateTestDatabase(settings);
        try
        {
            if (ownedDatabase is not null) await CreateOwnedDatabase();
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            // Re-running migrations must not duplicate catalogue entries.
            await db.Database.MigrateAsync();
        }
        catch
        {
            await base.DisposeAsync();
            if (ownedDatabase is not null) await DropOwnedDatabase();
            throw;
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (ownedDatabase is not null) await DropOwnedDatabase();
    }

    private async Task CreateOwnedDatabase()
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{ownedDatabase}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropOwnedDatabase()
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{ownedDatabase}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlConnectionStringBuilder TestSettings()
    {
        var connectionString = Environment.GetEnvironmentVariable("STEAMIRL_TEST_DATABASE")
            ?? throw new InvalidOperationException("Set STEAMIRL_TEST_DATABASE to a PostgreSQL administrative connection whose database name starts with steamirl_test. See docs/development.md.");
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        ValidateTestDatabase(settings);
        return settings;
    }

    private static void ValidateTestDatabase(NpgsqlConnectionStringBuilder settings)
    {
        if (settings.Database is null || !settings.Database.StartsWith("steamirl_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Integration tests require a PostgreSQL administrative connection whose database name starts with steamirl_test.");
    }
}
