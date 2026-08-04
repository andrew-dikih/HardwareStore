<#
.SYNOPSIS
    Deterministic checks against the actual trigger blocks in
    .github/workflows/ci.yml and deploy.yml, guarding the passive-docs-only
    automation gating described in .github/copilot-instructions.md.

    This intentionally parses the real workflow files (not a copy/fixture) so
    it fails the moment someone edits a trigger without updating the other
    half of the pair, or widens the docs-only boundary to something unsafe
    (e.g. .github/**, src/**, tests/**).

.EXAMPLE
    pwsh -File tests/workflow-config/Test-CiDocsGating.ps1
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$ciPath = Join-Path $repoRoot '.github\workflows\ci.yml'
$deployPath = Join-Path $repoRoot '.github\workflows\deploy.yml'

$expectedDocsBoundary = @('README.md', 'LICENSE', 'docs/**')
$forbiddenPrefixes = @('.github/', 'src/', 'tests/')

$failures = New-Object System.Collections.Generic.List[string]

function Get-OnBlock([string]$content) {
    # Isolate the top-level `on:` block up to the next top-level key (e.g. `env:`/`jobs:`).
    if ($content -notmatch '(?ms)^on:\r?\n(.*?)(?=^\S|\z)') {
        throw "Could not locate a top-level 'on:' block"
    }
    return $Matches[0]
}

function Get-PathsIgnore([string]$onBlock) {
    if ($onBlock -notmatch '(?ms)paths-ignore:\r?\n((?:\s+-\s.*\r?\n?)+)') {
        return @()
    }
    return ($Matches[1] -split "`r?`n" | Where-Object { $_ -match '^\s*-\s*''?([^'']+)''?\s*$' } |
        ForEach-Object { ($_ -replace "^\s*-\s*'?", '') -replace "'?\s*$", '' })
}

function Assert-DocsBoundary([string]$label, [string[]]$actual) {
    if (@(Compare-Object $expectedDocsBoundary $actual -SyncWindow 0).Count -ne 0) {
        $failures.Add("[$label] paths-ignore boundary mismatch. Expected: $($expectedDocsBoundary -join ', ') | Actual: $($actual -join ', ')")
        return
    }
    foreach ($entry in $actual) {
        foreach ($prefix in $forbiddenPrefixes) {
            if ($entry.StartsWith($prefix)) {
                $failures.Add("[$label] paths-ignore entry '$entry' overlaps a forbidden automation-required prefix '$prefix'")
            }
        }
    }
}

# --- ci.yml -----------------------------------------------------------------
$ciContent = Get-Content -Raw $ciPath
$ciOn = Get-OnBlock $ciContent

if ($ciOn -notmatch '(?ms)pull_request:.*?branches:\s*\r?\n(?:\s+-\s.*\r?\n?)*\s+-\s*main') {
    $failures.Add("[ci.yml] pull_request trigger no longer targets 'main'")
}
if ($ciOn -notmatch '(?ms)pull_request:.*?branches:\s*\r?\n(?:\s+-\s.*\r?\n?)*\s+-\s*develop') {
    $failures.Add("[ci.yml] pull_request trigger no longer targets 'develop'")
}
Assert-DocsBoundary 'ci.yml' (Get-PathsIgnore $ciOn)
if ($ciOn -notmatch '(?m)^\s*workflow_dispatch:\s*$') {
    $failures.Add("[ci.yml] missing manual workflow_dispatch escape hatch")
}

# --- deploy.yml ---------------------------------------------------------------
$deployContent = Get-Content -Raw $deployPath
$deployOn = Get-OnBlock $deployContent

if ($deployOn -notmatch '(?ms)push:.*?branches:\s*\r?\n(?:\s+-\s.*\r?\n?)*\s+-\s*main') {
    $failures.Add("[deploy.yml] push trigger no longer targets 'main'")
}
Assert-DocsBoundary 'deploy.yml' (Get-PathsIgnore $deployOn)
if ($deployOn -notmatch '(?m)^\s*workflow_dispatch:\s*$') {
    $failures.Add("[deploy.yml] missing manual workflow_dispatch escape hatch")
}

# --- Report -------------------------------------------------------------------
if ($failures.Count -gt 0) {
    Write-Host "FAILED: docs-only CI gating checks" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "PASSED: ci.yml and deploy.yml docs-only gating trigger blocks match the documented boundary." -ForegroundColor Green
exit 0
