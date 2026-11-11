param([string]$MetadataPath='.local/review-git', [string]$BundlePath='SteamIRL-review.bundle')
$ErrorActionPreference='Stop'
$savedDir=$env:GIT_DIR; $savedTree=$env:GIT_WORK_TREE
try {
    # Keep review metadata separate when the root .git is read-only in the agent workspace.
    # Only writes to the explicitly selected local metadata directory and bundle.
    $env:GIT_DIR=[IO.Path]::GetFullPath($MetadataPath); $env:GIT_WORK_TREE=(Get-Location).Path
    if (!(Test-Path -LiteralPath $env:GIT_DIR)) { git init -b main; if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize local review metadata.' } }
    git add .
    if ($LASTEXITCODE -ne 0) { throw 'Cannot stage local files.' }
    & (Join-Path (Get-Location) 'scripts/Check-Repository.ps1')
    git diff --cached --quiet
    if ($LASTEXITCODE -eq 1) {
        git commit -m 'Prepare local database assessment aligned with BA'
        if ($LASTEXITCODE -ne 0) { throw 'Local commit failed.' }
    } elseif ($LASTEXITCODE -ne 0) { throw 'Cannot inspect staged changes.' }
    git bundle create ([IO.Path]::GetFullPath($BundlePath)) --all
    if ($LASTEXITCODE -ne 0) { throw 'Bundle creation failed.' }
    git bundle verify ([IO.Path]::GetFullPath($BundlePath))
    if ($LASTEXITCODE -ne 0) { throw 'Bundle verification failed.' }
    Write-Host 'Local Git review bundle created and verified. No remote configured or publication performed.'
} finally { $env:GIT_DIR=$savedDir; $env:GIT_WORK_TREE=$savedTree }
