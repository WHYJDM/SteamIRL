param([string]$OutputPath = '.env', [string]$DatabaseName='steamirl', [string]$RolePrefix='steamirl')
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputPath) { throw "File already exists: $OutputPath. Existing passwords are never overwritten." }
if ($DatabaseName -notmatch '^[a-z][a-z0-9_]{0,62}$' -or $RolePrefix -notmatch '^[a-z][a-z0-9_]{0,39}$') { throw 'Invalid database name or role prefix.' }
function New-Secret {
    $bytes = New-Object byte[] 32
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    return ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
}
$lines = @("POSTGRES_DB=$DatabaseName", 'POSTGRES_USER=steamirl_admin', "DATABASE_ROLE_PREFIX=$RolePrefix",
    "POSTGRES_PASSWORD=$(New-Secret)", "MIGRATOR_PASSWORD=$(New-Secret)",
    "APP_DATABASE_PASSWORD=$(New-Secret)", "READER_PASSWORD=$(New-Secret)", 'POSTGRES_PORT=55432', 'WEB_PORT=8088')
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputPath), $lines)
Write-Host 'Local secrets generated. Values were not printed. Keep this file outside Git and restrict access to your OS account.'
