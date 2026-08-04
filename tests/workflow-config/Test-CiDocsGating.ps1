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

if ($deployOn -notmatch '(?ms)push:.*?branches:\s*\r?\n(?:\s+-\s.*\r?\n?)*\s+-\s*develop') {
    $failures.Add("[deploy.yml] push trigger no longer targets 'develop'")
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

# =============================================================================
# Three-state local classifier + regression suite
#
# Mirrors (does not call) GitHub's real paths-ignore evaluation so the
# documented boundary and its documented limits (empty/incomplete diff,
# >=3,000-file diff) can be regression-tested locally without hitting the
# GitHub API. This is NOT a replacement for GitHub's own evaluation - it is a
# local sanity/regression tool that fails loudly if someone changes
# $expectedDocsBoundary above without updating this suite, or if the
# classification logic itself drifts from the documented semantics.
#
# States:
#   'docs-only'           - every path matches the passive-doc boundary
#   'automation-required'  - at least one path falls outside the boundary
#   'indeterminate'         - empty/null/incomplete file list, or >=3000 files
#                             (GitHub only evaluates the first 3,000 files in a
#                             diff against paths/paths-ignore - see
#                             copilot-instructions.md#ci-docs-only-automation-gating)
# =============================================================================

$GitHubPathFilterEvaluationLimit = 3000

function ConvertTo-GlobRegex([string]$pattern) {
    # Minimal glob->regex translator sufficient for our boundary patterns:
    # '**' matches any characters (incl. '/'), '*' matches any run of
    # non-'/' characters. Everything else is a literal.
    $escaped = [regex]::Escape($pattern)
    $escaped = $escaped -replace '\\\*\\\*', '.*'
    $escaped = $escaped -replace '\\\*', '[^/]*'
    return "^$escaped$"
}

function Get-DocsClassification {
    param(
        [AllowNull()][string[]]$ChangedFiles,
        [string[]]$Boundary = $expectedDocsBoundary
    )

    # Indeterminate: null/empty (nothing to classify - upstream data was
    # missing or incomplete) or too large for GitHub to reliably evaluate.
    if ($null -eq $ChangedFiles -or @($ChangedFiles).Count -eq 0) {
        return 'indeterminate'
    }
    if (@($ChangedFiles).Count -ge $GitHubPathFilterEvaluationLimit) {
        return 'indeterminate'
    }

    $boundaryRegexes = $Boundary | ForEach-Object { ConvertTo-GlobRegex $_ }
    foreach ($file in $ChangedFiles) {
        $matched = $false
        foreach ($rx in $boundaryRegexes) {
            if ($file -match $rx) { $matched = $true; break }
        }
        if (-not $matched) {
            return 'automation-required'
        }
    }
    return 'docs-only'
}

$classifierFailures = New-Object System.Collections.Generic.List[string]
$classifierCaseCount = 0

function Assert-Classification([string]$case, [string[]]$files, [string]$expected) {
    $script:classifierCaseCount++
    $actual = Get-DocsClassification -ChangedFiles $files
    if ($actual -ne $expected) {
        $countDesc = if ($null -eq $files) { 'null' } else { "$($files.Count) file(s)" }
        $classifierFailures.Add("[$case] expected '$expected' but got '$actual' ($countDesc)")
    }
}

# Pure docs-only.
Assert-Classification 'pure docs - single README' @('README.md') 'docs-only'
Assert-Classification 'pure docs - full boundary set' @('README.md', 'LICENSE', 'docs/setup.md', 'docs/nested/deep/page.md') 'docs-only'

# Mixed docs + code.
Assert-Classification 'mixed - doc plus source' @('README.md', 'src/HardwareStore.Api/Program.cs') 'automation-required'
Assert-Classification 'mixed - doc plus docs-boundary-lookalike' @('docs/readme.md', 'docsite/other.md') 'automation-required'

# .github/** must never be swallowed by the boundary, even if it's a .md file.
Assert-Classification '.github workflow file' @('.github/workflows/ci.yml') 'automation-required'
Assert-Classification '.github markdown file' @('.github/copilot-instructions.md') 'automation-required'

# Source tree, even documentation-shaped files within it, stays automation-required.
Assert-Classification 'source - csharp file' @('src/HardwareStore.Api/Program.cs') 'automation-required'
Assert-Classification 'source - nested README' @('src/HardwareStore.Web/README.md') 'automation-required'

# tests/** stays automation-required.
Assert-Classification 'tests tree' @('tests/HardwareStore.UnitTests/FooTests.cs') 'automation-required'
Assert-Classification 'workflow-config test harness itself' @('tests/workflow-config/Test-CiDocsGating.ps1') 'automation-required'

# Deployable/operational/config artifacts stay automation-required.
Assert-Classification 'deployable - docker-compose' @('docker-compose.yml') 'automation-required'
Assert-Classification 'deployable - dockerfile' @('src/HardwareStore.Api/Dockerfile') 'automation-required'
Assert-Classification 'config - env example' @('.env.example') 'automation-required'
Assert-Classification 'config - solution file' @('HardwareStore.slnx') 'automation-required'

# Empty / incomplete input -> indeterminate, never silently treated as docs-only.
Assert-Classification 'empty file list' @() 'indeterminate'
Assert-Classification 'null file list (incomplete data)' $null 'indeterminate'

# Threshold behavior around GitHub's 3,000-file evaluation limit.
$justUnderThreshold = 1..($GitHubPathFilterEvaluationLimit - 1) | ForEach-Object { "docs/generated-$_.md" }
Assert-Classification 'just under 3,000-file threshold, all docs' $justUnderThreshold 'docs-only'

$atThreshold = 1..$GitHubPathFilterEvaluationLimit | ForEach-Object { "docs/generated-$_.md" }
Assert-Classification 'at 3,000-file threshold, all docs' $atThreshold 'indeterminate'

$overThreshold = 1..($GitHubPathFilterEvaluationLimit + 1) | ForEach-Object { "docs/generated-$_.md" }
Assert-Classification 'over 3,000-file threshold, all docs' $overThreshold 'indeterminate'

if ($classifierFailures.Count -gt 0) {
    Write-Host "FAILED: docs-only classifier regression suite" -ForegroundColor Red
    $classifierFailures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "PASSED: three-state docs-only classifier regression suite ($classifierCaseCount cases)." -ForegroundColor Green

# =============================================================================
# Anti-regression guard: reject unqualified claims that `workflow_dispatch`
# can satisfy/unstick a pull request's required status check.
#
# This is a real defect class we hit once already (a workflow comment stated
# dispatch "lets a maintainer re-run CI to satisfy a required status check
# that is stuck pending" - false: dispatch runs against a chosen ref and
# never attaches a status to a specific PR head). This guard scans every
# workflow file and doc file that could plausibly discuss the dispatch
# escape hatch, and fails if it finds "satisfy"/"unstick" near
# "required"/"pending"/"stuck" WITHOUT a nearby negation word. Correct text
# always negates ("does not", "cannot", "never", "no way to", ...); a
# regression re-introducing the false claim will have no such negation
# nearby and will be caught.
# =============================================================================

$dispatchClaimFailures = New-Object System.Collections.Generic.List[string]
$negationWords = @('not', 'never', 'cannot', "can't", "doesn't", 'no way', 'does not', 'no longer', 'without', "won't")
$claimTermRegex = [regex]'(?i)(satisf(?:y|ies|ied)|unstick)'
$contextTermRegex = [regex]'(?i)(required|pending|stuck)'
$contextWindow = 90

function Test-DispatchClaimSafety([string]$relativePath, [string]$content) {
    foreach ($m in $claimTermRegex.Matches($content)) {
        $start = [Math]::Max(0, $m.Index - $contextWindow)
        $end = [Math]::Min($content.Length, $m.Index + $m.Length + $contextWindow)
        $window = $content.Substring($start, $end - $start)

        if (-not $contextTermRegex.IsMatch($window)) {
            continue # "satisfy"/"unstick" not talking about a required/pending/stuck check here.
        }

        $hasNegation = $false
        foreach ($n in $negationWords) {
            if ($window -match "(?i)\b$([regex]::Escape($n))\b") { $hasNegation = $true; break }
        }
        if (-not $hasNegation) {
            $snippet = ($window -replace '\s+', ' ').Trim()
            $dispatchClaimFailures.Add("[$relativePath] unqualified dispatch-satisfies-check claim near: ...$snippet...")
        }
    }
}

$filesToScanForDispatchClaims = @(
    '.github\workflows\ci.yml',
    '.github\workflows\deploy.yml',
    '.github\workflows\copilot-setup-steps.yml',
    '.github\workflows\setup-repository.yml',
    '.github\workflows\copilot-on-changes-requested.yml',
    '.github\workflows\label-check.yml',
    '.github\copilot-instructions.md',
    '.github\PULL_REQUEST_TEMPLATE.md'
)

foreach ($relative in $filesToScanForDispatchClaims) {
    $fullPath = Join-Path $repoRoot $relative
    if (-not (Test-Path $fullPath)) { continue }
    Test-DispatchClaimSafety $relative (Get-Content -Raw $fullPath)
}

if ($dispatchClaimFailures.Count -gt 0) {
    Write-Host "FAILED: workflow_dispatch false-claim guard" -ForegroundColor Red
    $dispatchClaimFailures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "PASSED: no unqualified workflow_dispatch-satisfies-required-check claims found in workflows or docs." -ForegroundColor Green
exit 0
