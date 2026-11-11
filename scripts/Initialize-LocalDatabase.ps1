param([string]$SecretsPath='.env', [string]$DatabaseHost='127.0.0.1', [int]$DatabasePort=55432)
$ErrorActionPreference='Stop'
$settings=@{}
Get-Content -LiteralPath $SecretsPath | ForEach-Object { if ($_ -and !($_.StartsWith('#'))) { $pair=$_.Split('=',2); if ($pair.Length -eq 2) { $settings[$pair[0]]=$pair[1] } } }
foreach ($name in @('POSTGRES_DB','POSTGRES_USER','POSTGRES_PASSWORD','MIGRATOR_PASSWORD','APP_DATABASE_PASSWORD','READER_PASSWORD')) { if (!$settings[$name]) { throw "Missing local setting: $name" } }
$prefix=if ($settings['DATABASE_ROLE_PREFIX']) {$settings['DATABASE_ROLE_PREFIX']} else {'steamirl'}
$connection=New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host']=$DatabaseHost; $connection['Port']=$DatabasePort; $connection['Database']=$settings['POSTGRES_DB']; $connection['Username']=$settings['POSTGRES_USER']; $connection['Password']=$settings['POSTGRES_PASSWORD']
$savedConnection=$env:ConnectionStrings__Database
try {
    $env:ConnectionStrings__Database=if ($env:STEAMIRL_ADMIN_CONNECTION) { $env:STEAMIRL_ADMIN_CONNECTION } else { $connection.ConnectionString }
    $env:DatabaseRoles__Prefix=$prefix
    $env:DatabaseRoles__MigratorPassword=$settings['MIGRATOR_PASSWORD']; $env:DatabaseRoles__AppPassword=$settings['APP_DATABASE_PASSWORD']; $env:DatabaseRoles__ReaderPassword=$settings['READER_PASSWORD']
    dotnet run --project src/SteamIRL.Api --no-build --no-launch-profile -- --bootstrap-database
    if ($LASTEXITCODE -ne 0) { throw 'Bootstrap failed.' }
    $connection['Username']="${prefix}_migrator"; $connection['Password']=$settings['MIGRATOR_PASSWORD']; $env:ConnectionStrings__Database=$connection.ConnectionString
    dotnet run --project src/SteamIRL.Api --no-build --no-launch-profile -- --migrate
    if ($LASTEXITCODE -ne 0) { throw 'Migration failed.' }
    dotnet run --project src/SteamIRL.Api --no-build --no-launch-profile -- --grant-database-access
    if ($LASTEXITCODE -ne 0) { throw 'Permission grant failed.' }
    Write-Host 'Database initialized. Use scripts/Start-LocalApi.ps1 with the same host, port and secrets file.'
} finally {
    $env:ConnectionStrings__Database=$savedConnection
    foreach ($name in @('DatabaseRoles__MigratorPassword','DatabaseRoles__AppPassword','DatabaseRoles__ReaderPassword','DatabaseRoles__Prefix')) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
}
