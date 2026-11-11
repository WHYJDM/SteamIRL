$ErrorActionPreference = 'Stop'
$files = @(git -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Run this check inside the local Git repository after staging files.' }
if ($files.Count -eq 0) { throw 'No tracked files: an empty scan is not a successful credential review.' }
$findings = New-Object 'System.Collections.Generic.List[string]'
foreach ($file in $files) {
    if ($file -match '(^|/)(\.local|pgdata|node_modules|bin|obj|auth-keys)/|(^|/)(\.env|secrets\.json)$|\.(pem|key|pfx|p12|pgpass)$' -or ($file -match '(^|/)\.env\.' -and $file -ne '.env.example')) {
        $findings.Add("Forbidden tracked file: $file"); continue
    }
    if ($file -match '\.(docx|pdf|png|jpg|jpeg|gif|ico|woff2?)$') { continue }
    $body = [IO.File]::ReadAllText((Join-Path (Get-Location) $file))
    if ($body -match '-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,}|AKIA[0-9A-Z]{16}') { $findings.Add("Possible credential: $file") }
    foreach ($match in [regex]::Matches($body, ('(?i)Pass' + 'word=([^;\s"''`]+)'))) {
        $value = $match.Groups[1].Value
        if ($value -notmatch '^(\$|YOUR_|<|test$)') { $findings.Add("Literal database password: $file") }
    }
}
if ($findings.Count -gt 0) { $findings | Sort-Object -Unique | ForEach-Object { Write-Host $_ }; throw 'Repository credential check failed; values were deliberately not printed.' }
Write-Host "Checked $($files.Count) tracked files: no forbidden local files or supported credential signatures found. Manual review is still required."
