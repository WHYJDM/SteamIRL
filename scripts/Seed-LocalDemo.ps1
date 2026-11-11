param([string]$SecretsPath='.local/demo.env', [string]$DatabaseHost='127.0.0.1', [int]$DatabasePort=55432)
$ErrorActionPreference='Stop'
$settings=@{}
Get-Content -LiteralPath $SecretsPath | ForEach-Object { if ($_ -and !($_.StartsWith('#'))) { $pair=$_.Split('=',2); if ($pair.Length -eq 2) { $settings[$pair[0]]=$pair[1] } } }
if (!$settings['POSTGRES_DB'].EndsWith('_demo')) { throw 'Use a separate database ending in _demo.' }
if (!$env:Demo__Password) { throw 'Provide a generated Demo__Password for the two local demo accounts; do not put it in Git.' }
$prefix=if ($settings['DATABASE_ROLE_PREFIX']) {$settings['DATABASE_ROLE_PREFIX']} else {'steamirl'}
$connection=New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host']=$DatabaseHost; $connection['Port']=$DatabasePort; $connection['Database']=$settings['POSTGRES_DB']; $connection['Username']="${prefix}_migrator"; $connection['Password']=$settings['MIGRATOR_PASSWORD']
$savedConnection=$env:ConnectionStrings__Database; $savedEnvironment=$env:ASPNETCORE_ENVIRONMENT
try {
    $env:ConnectionStrings__Database=$connection.ConnectionString; $env:ASPNETCORE_ENVIRONMENT='Development'; $env:Demo__AllowSeed='true'
    dotnet run --project src/SteamIRL.Api --no-build --no-launch-profile -- --seed-demo
    if ($LASTEXITCODE -ne 0) { throw 'Demo seeding failed.' }
    Write-Host 'Demo records seeded. The password was not printed. Repeating the command does not create duplicate records.'
} finally {
    $env:ConnectionStrings__Database=$savedConnection; $env:ASPNETCORE_ENVIRONMENT=$savedEnvironment
    Remove-Item Env:Demo__AllowSeed -ErrorAction SilentlyContinue
}
