#!/usr/bin/env pwsh
#
# Reports how much of an Epic 1 wave is left, by counting the unticked
# acceptance criteria on every feature the wave names.
#
# A feature's criteria are its stories — `docs/decisions.md` and every feature
# body say "One User Story each" — so unticked criteria are the honest measure
# of remaining work, and they stay meaningful whether or not anyone has
# decomposed the feature into issues yet. Sub-issue counts do not: a feature
# with six criteria and no stories reads as empty.
#
# The wave's membership is read out of Epic 1 rather than written down here,
# for the reason the epic itself now gives for not writing its feature count
# down: a number in a file drifts, and nobody notices until they read it.
#
#     ./scripts/wave-progress.ps1
#     ./scripts/wave-progress.ps1 -Wave 3
#     ./scripts/wave-progress.ps1 -Issue 7, 8, 9
#
# Needs `gh` authenticated. Run it from anywhere inside a clone; the repository
# comes from `gh repo view`, so a fork reports on itself.
#
# What it does not do: decide whether the wave's header line is complete. A
# wave's ordering notes routinely name issues the header omits, and those are
# reported separately rather than folded into the total — guessing which prose
# mentions are membership and which are cross-references is exactly the
# judgment a script should not be making silently.

[CmdletBinding()]
param(
    # Which wave of Epic 1 to report on.
    [int] $Wave = 2,

    # Report on these issues instead, ignoring the epic entirely.
    [int[]] $Issue,

    # The epic whose build order names the waves.
    [int] $Epic = 1
)

$ErrorActionPreference = 'Stop'

function Get-IssueBody {
    param([int] $Number)

    $json = gh issue view $Number --repo $script:Repo --json body,title,state
    if ($LASTEXITCODE -ne 0) {
        throw "gh issue view $Number failed with exit code $LASTEXITCODE"
    }
    return ($json | ConvertFrom-Json)
}

$repoJson = gh repo view --json nameWithOwner
if ($LASTEXITCODE -ne 0) {
    throw "gh repo view failed. Run this from inside a clone, with gh authenticated."
}
$script:Repo = ($repoJson | ConvertFrom-Json).nameWithOwner

$numbers = $Issue
$extras = @()

if (-not $numbers) {
    $epicBody = (Get-IssueBody -Number $Epic).body
    $lines = $epicBody -split "`r?`n"

    # The wave's own heading line lists its members. Everything from there to
    # the next wave heading is the wave's section, which is scanned separately.
    $start = -1
    $end = $lines.Count
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^\*\*Wave $Wave\b") { $start = $i; continue }
        if ($start -ge 0 -and $lines[$i] -match '^\*\*Wave \d+\b') { $end = $i; break }
    }

    if ($start -lt 0) {
        throw "No '**Wave $Wave' heading in issue #$Epic of $script:Repo."
    }

    $numbers = [int[]] @(
        [regex]::Matches($lines[$start], '#(\d+)') |
            ForEach-Object { [int] $_.Groups[1].Value } |
            Select-Object -Unique)

    $section = ($lines[($start + 1)..($end - 1)] -join "`n")
    $extras = @(
        [regex]::Matches($section, '#(\d+)') |
            ForEach-Object { [int] $_.Groups[1].Value } |
            Select-Object -Unique |
            Where-Object { $numbers -notcontains $_ })
}

$rows = @()
foreach ($n in ($numbers | Sort-Object)) {
    # Not $issue: PowerShell variable names are case-insensitive, so that name
    # would overwrite the [int[]] $Issue parameter and fail on the next loop.
    $record = Get-IssueBody -Number $n
    $bodyLines = $record.body -split "`r?`n"

    $rows += [pscustomobject] @{
        Issue = "#$n"
        State = $record.state
        Left  = @($bodyLines | Where-Object { $_ -match '^- \[ \]' }).Count
        Done  = @($bodyLines | Where-Object { $_ -match '^- \[x\]' }).Count
        Title = $record.title
    }
}

$rows | Format-Table -AutoSize

$left = ($rows | Measure-Object -Property Left -Sum).Sum
$done = ($rows | Measure-Object -Property Done -Sum).Sum
$total = $left + $done

if ($Issue) {
    Write-Host "$left of $total criteria left across $($rows.Count) issue(s)."
}
else {
    Write-Host "Wave ${Wave}: $left of $total criteria left across $($rows.Count) feature(s)."
}

# Not added to the total. See the header comment: which of these are members
# and which are cross-references is a judgment, and a silently wrong total is
# worse than a visible question.
if ($extras.Count -gt 0) {
    $list = ($extras | Sort-Object | ForEach-Object { "#$_" }) -join ', '
    Write-Host ''
    Write-Host "Also named in Wave $Wave's notes but not in its heading: $list"
    Write-Host 'Counted in neither total. Add them to the heading if they are members.'
}
