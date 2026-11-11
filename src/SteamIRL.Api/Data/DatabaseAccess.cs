using Npgsql;

namespace SteamIRL.Api.Data;

/// <summary>Explicit administrative commands; never called by an HTTP endpoint.</summary>
public static class DatabaseAccess
{
    public static async Task Bootstrap(string connectionString, IConfiguration config)
    {
        var (Migrator, Application, Reader) = Roles(config);
        var passwords = new[] { "MigratorPassword", "AppPassword", "ReaderPassword" }
            .Select(key => config[$"DatabaseRoles:{key}"] ?? throw new InvalidOperationException($"Set DatabaseRoles__{key} for bootstrap only.")).ToArray();
        if (passwords.Any(value => value.Length < 20) || passwords.Distinct().Count() != 3)
            throw new InvalidOperationException("Use three different generated database passwords of at least 20 characters.");
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        for (var i = 0; i < 3; i++)
        {
            var role = new[] { Migrator, Application, Reader }[i];
            await using var check = new NpgsqlCommand("SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls OR EXISTS(SELECT 1 FROM pg_auth_members WHERE member=pg_roles.oid) FROM pg_roles WHERE rolname=$1", connection, transaction);
            check.Parameters.AddWithValue(role);
            var existing = await check.ExecuteScalarAsync();
            if (existing is true) throw new InvalidOperationException($"Role {role} has unexpected elevated rights/memberships. Review it manually.");
            if (existing is null) await Execute(connection, $"CREATE ROLE {Id(role)} NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT", transaction);
            // Values are escaped as PostgreSQL literals, not shell text. Do not log this command.
            await Execute(connection, $"ALTER ROLE {Id(role)} LOGIN PASSWORD {Literal(passwords[i])}", transaction);
        }
        var database = connection.Database;
        await Execute(connection, $"REVOKE ALL ON DATABASE {Id(database)} FROM PUBLIC; GRANT CONNECT ON DATABASE {Id(database)} TO {Migrator}, {Application}, {Reader}; ALTER DATABASE {Id(database)} OWNER TO {Migrator}; REVOKE ALL ON SCHEMA public FROM PUBLIC; ALTER SCHEMA public OWNER TO {Migrator}; GRANT USAGE ON SCHEMA public TO {Application};", transaction);
        // Upgrade an existing dedicated SteamIRL database without recreating its data.
        await using var objects = new NpgsqlCommand("SELECT c.relname,c.relkind::text FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind IN ('r','p','S','v') AND (c.relkind<>'S' OR NOT EXISTS(SELECT 1 FROM pg_depend d WHERE d.classid='pg_class'::regclass AND d.objid=c.oid AND d.deptype IN ('a','i'))) ORDER BY c.relkind,c.relname", connection, transaction);
        var names = new List<(string Name, string Kind)>();
        await using (var rows = await objects.ExecuteReaderAsync()) while (await rows.ReadAsync()) names.Add((rows.GetString(0), rows.GetString(1)));
        foreach (var (name, kind) in names)
            await Execute(connection, $"ALTER {(kind == "S" ? "SEQUENCE" : kind == "v" ? "VIEW" : "TABLE")} public.{Id(name)} OWNER TO {Migrator}", transaction);
        await using var hasReports = new NpgsqlCommand("SELECT to_regnamespace('reporting') IS NOT NULL", connection, transaction);
        if (await hasReports.ExecuteScalarAsync() is true)
        {
            await Execute(connection, $"ALTER SCHEMA reporting OWNER TO {Migrator}", transaction);
            foreach (var view in new[] { "account_directory", "location_catalogue", "session_overview" })
                await Execute(connection, $"ALTER VIEW reporting.{Id(view)} OWNER TO {Migrator}", transaction);
            await Execute(connection, $"ALTER FUNCTION public.protect_moderation_decision() OWNER TO {Migrator}", transaction);
        }
        await transaction.CommitAsync();
    }

    public static async Task Grant(string connectionString, IConfiguration config)
    {
        var (_, Application, Reader) = Roles(config);
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        // No default blanket grants: every new table needs an explicit decision here.
        await Execute(connection, $"REVOKE ALL ON ALL TABLES IN SCHEMA public FROM PUBLIC, {Application}, {Reader}; REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM PUBLIC, {Application}, {Reader}; REVOKE ALL ON SCHEMA public FROM {Reader}; GRANT USAGE ON SCHEMA public TO {Application}; REVOKE CREATE ON SCHEMA public FROM {Application};", transaction);
        foreach (var table in new[] { "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims", "AspNetRoleClaims", "AspNetUserLogins", "AspNetUserTokens", "activities", "public_profiles", "locations", "location_activities", "sessions", "participations", "location_submissions", "submission_activities", "content_reports", "moderation_decisions", "idempotency_requests" })
            await Execute(connection, $"GRANT SELECT ON public.{Id(table)} TO {Application}", transaction);
        foreach (var table in new[] { "AspNetUsers", "public_profiles", "locations", "location_activities", "sessions", "participations", "location_submissions", "content_reports", "idempotency_requests" })
            await Execute(connection, $"GRANT INSERT, UPDATE ON public.{Id(table)} TO {Application}", transaction);
        await Execute(connection, $"GRANT INSERT ON public.submission_activities, public.moderation_decisions TO {Application}; GRANT DELETE ON public.idempotency_requests TO {Application}; GRANT USAGE ON SCHEMA reporting TO {Reader}; GRANT SELECT ON reporting.account_directory, reporting.location_catalogue, reporting.session_overview TO {Reader}; REVOKE ALL ON SCHEMA reporting FROM PUBLIC, {Application}; REVOKE ALL ON ALL TABLES IN SCHEMA reporting FROM PUBLIC, {Application}; REVOKE ALL ON FUNCTION public.protect_moderation_decision() FROM PUBLIC, {Application}, {Reader};", transaction);
        await transaction.CommitAsync();
    }

    public static async Task VerifyApplication(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var query = new NpgsqlCommand("""
            SELECT r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls
                OR d.datdba=r.oid OR has_schema_privilege(current_user,'public','CREATE')
                OR EXISTS(SELECT 1 FROM pg_auth_members WHERE member=r.oid)
                OR EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('public','reporting') AND c.relowner=r.oid)
            FROM pg_roles r JOIN pg_database d ON d.datname=current_database() WHERE r.rolname=current_user
            """, connection);
        if (await query.ExecuteScalarAsync() is not false)
            throw new InvalidOperationException("The web application requires a restricted database login, without ownership, DDL or elevated roles. Run --bootstrap-database, --migrate and --grant-database-access with the documented separate credentials.");
    }
    private static async Task Execute(NpgsqlConnection connection, string sql, NpgsqlTransaction transaction)
    { await using var command = new NpgsqlCommand(sql, connection, transaction); await command.ExecuteNonQueryAsync(); }
    private static string Id(string value) => '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    public static (string Migrator, string Application, string Reader) Roles(IConfiguration config)
    {
        var prefix = config["DatabaseRoles:Prefix"] ?? "steamirl";
        if (!System.Text.RegularExpressions.Regex.IsMatch(prefix, "^[a-z][a-z0-9_]{0,39}$"))
            throw new InvalidOperationException("DatabaseRoles__Prefix must contain 1–40 lowercase ASCII letters, digits or underscores, starting with a letter.");
        return ($"{prefix}_migrator", $"{prefix}_app", $"{prefix}_reader");
    }
}
