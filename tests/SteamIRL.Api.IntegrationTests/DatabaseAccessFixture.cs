using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SteamIRL.Api.Data;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class DatabaseAccessFixture : IAsyncLifetime
{
    private readonly string database = $"steamirl_test_access_{Guid.NewGuid():N}";
    private string adminConnection = "";
    public string MigratorConnection { get; private set; } = "";
    public string AppConnection { get; private set; } = "";
    public string ReaderConnection { get; private set; } = "";
    public string DatabaseAdminConnection { get; private set; } = "";
    public IConfiguration Config { get; private set; } = null!;
    public ApiFactory Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STEAMIRL_TEST_DATABASE"));
        if (settings.Database?.StartsWith("steamirl_test", StringComparison.Ordinal) != true) throw new InvalidOperationException("Dedicated test database required.");
        settings.Database = "postgres"; adminConnection = settings.ConnectionString;
        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection); await command.ExecuteNonQueryAsync();
        }
        settings.Database = database; DatabaseAdminConnection = settings.ConnectionString;
        var passwords = Enumerable.Range(0, 3).Select(_ => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))).ToArray();
        Config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["DatabaseRoles:Prefix"] = $"si_test_{Guid.NewGuid():N}", ["DatabaseRoles:MigratorPassword"] = passwords[0],
            ["DatabaseRoles:AppPassword"] = passwords[1], ["DatabaseRoles:ReaderPassword"] = passwords[2] }).Build();
        await DatabaseAccess.Bootstrap(DatabaseAdminConnection, Config);
        var roles = DatabaseAccess.Roles(Config);
        string AsRole(string name, string password) => new NpgsqlConnectionStringBuilder(settings.ConnectionString) { Username = name, Password = password }.ConnectionString;
        MigratorConnection = AsRole(roles.Migrator, passwords[0]); AppConnection = AsRole(roles.Application, passwords[1]); ReaderConnection = AsRole(roles.Reader, passwords[2]);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(MigratorConnection).Options);
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        await DatabaseAccess.Grant(MigratorConnection, Config); await DatabaseAccess.Grant(MigratorConnection, Config);
        Factory = new ApiFactory(AppConnection);
    }
    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await using var connection = new NpgsqlConnection(adminConnection); await connection.OpenAsync();
        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection)) await drop.ExecuteNonQueryAsync();
        if (Config is not null)
            foreach (var role in new[] { DatabaseAccess.Roles(Config).Application, DatabaseAccess.Roles(Config).Reader, DatabaseAccess.Roles(Config).Migrator })
            { await using var drop = new NpgsqlCommand($"DROP ROLE IF EXISTS \"{role}\"", connection); await drop.ExecuteNonQueryAsync(); }
    }
}
