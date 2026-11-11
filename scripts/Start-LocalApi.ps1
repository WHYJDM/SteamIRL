param([string]$SecretsPath='.env', [string]$DatabaseHost='127.0.0.1', [int]$DatabasePort=55432)
$ErrorActionPreference='Stop'
$settings=@{}
Get-Content -LiteralPath $SecretsPath | ForEach-Object { if ($_ -and !($_.StartsWith('#'))) { $pair=$_.Split('=',2); if ($pair.Length -eq 2) { $settings[$pair[0]]=$pair[1] } } }
if (!$settings['APP_DATABASE_PASSWORD']) { throw 'Generate local secrets first.' }
$prefix=if ($settings['DATABASE_ROLE_PREFIX']) {$settings['DATABASE_ROLE_PREFIX']} else {'steamirl'}
$connection=New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host']=$DatabaseHost; $connection['Port']=$DatabasePort; $connection['Database']=$settings['POSTGRES_DB']; $connection['Username']="${prefix}_app"; $connection['Password']=$settings['APP_DATABASE_PASSWORD']
$env:ConnectionStrings__Database=$connection.ConnectionString
foreach ($name in @('STEAMIRL_ADMIN_CONNECTION','DatabaseRoles__MigratorPassword','DatabaseRoles__AppPassword','DatabaseRoles__ReaderPassword','Testing__SkipDatabaseRoleCheck')) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
dotnet run --project src/SteamIRL.Api --no-build --launch-profile http
exit $LASTEXITCODE
