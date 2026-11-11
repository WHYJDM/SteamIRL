using Npgsql;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class ModerationFixture : IAsyncLifetime
{
    private readonly string database = $"steamirl_test_moderation_{Guid.NewGuid():N}";
    private string adminConnection = "";
    public ApiFactory Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STEAMIRL_TEST_DATABASE"));
        if (settings.Database?.StartsWith("steamirl_test", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Set STEAMIRL_TEST_DATABASE to a dedicated test database.");
        settings.Database = "postgres"; adminConnection = settings.ConnectionString;
        await using var connection = new NpgsqlConnection(adminConnection); await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection)) await create.ExecuteNonQueryAsync();
        settings.Database = database; Factory = new ApiFactory(settings.ConnectionString);
        await Factory.InitializeAsync();
    }
    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await using var connection = new NpgsqlConnection(adminConnection); await connection.OpenAsync();
        // Only this fixture's unique database is ever removed.
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
