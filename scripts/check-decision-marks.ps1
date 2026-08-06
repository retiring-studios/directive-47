<#
.SYNOPSIS
    Fails when a bullet is added to docs/decisions.md without saying whose
    decision it was.

.DESCRIPTION
    CLAUDE.md requires every bullet in docs/decisions.md to be in exactly one of
    three states: unmarked (the maintainer decided or approved it), marked
    "Not his call:" (Claude settled it), or marked "Unrecorded:" (nobody knows).

    Unmarked is the silent case, so it is the one that rots. This check makes the
    silent case cost something: a bullet that is new against the merge base and
    unmarked at the tip must be vouched for by a commit trailer.

    It is deliberately blind to edits of existing bullets. Deciding whether a
    reword constituted a new decision is a judgment a script cannot make, and a
    check that guesses at it produces false positives until somebody disables it.

    Existing bullets are never flagged: only bullets absent from the merge base
    are considered, and every bullet in the file today is present there.

.PARAMETER BaseRef
    The branch this work merges into. The comparison is against the merge base of
    this and HeadRef, not against its tip.

.PARAMETER HeadRef
    The revision to check. Marks are read from here, so intermediate commits that
    add a bullet and mark it later are not flagged.

.EXAMPLE
    pwsh scripts/check-decision-marks.ps1
#>
[CmdletBinding()]
param(
    [string] $BaseRef = 'origin/main',
    [string] $HeadRef = 'HEAD',
    [string] $Path    = 'docs/decisions.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Trailer  = 'Decided-By'
$Vouch    = 'maintainer'
$Marks    = @('Not his call:', 'Unrecorded:')

# Words close enough to a mark that using one is a typo rather than prose.
# Matched capitalised and sentence-initial, so "not derived from tags" is safe.
$WrongVocabulary = 'Derived|Unratified|Not my call|Not his decision'

function Get-FileAtRev {
    param([string] $Rev)

    $blob = & git show "${Rev}:${Path}" 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($blob)
}

function Get-Bullets {
    <#
        A top-level bullet starts at column 0 with "- " and runs until the next
        line beginning with a non-whitespace character. That terminates it on the
        next bullet, on a "## " heading, and on a table or paragraph at column 0,
        while keeping indented sub-bullets, indented paragraphs and the blank
        lines between them.

        The key is the whole bullet with whitespace collapsed, so rewrapping does
        not change it and moving a bullet between sections does not either.
    #>
    param([string[]] $Lines)

    $bullets = @()
    $i = 0

    while ($i -lt $Lines.Count) {
        if ($Lines[$i] -notmatch '^- ') { $i++; continue }

        $start = $i
        $body  = [System.Collections.Generic.List[string]]::new()
        $body.Add($Lines[$i])
        $i++

        while ($i -lt $Lines.Count -and $Lines[$i] -notmatch '^\S') {
            $body.Add($Lines[$i])
            $i++
        }

        while ($body.Count -gt 0 -and [string]::IsNullOrWhiteSpace($body[$body.Count - 1])) {
            $body.RemoveAt($body.Count - 1)
        }

        $text = ($body -join ' ')
        $key  = ($text -replace '\s+', ' ').Trim()

        $marked = $false
        foreach ($mark in $Marks) {
            if ($key -like "*$mark*") { $marked = $true; break }
        }

        $bullets += [pscustomobject]@{
            Key    = $key
            Line   = $start + 1
            Lead   = ($key.Substring(0, [Math]::Min(72, $key.Length)))
            Marked = $marked
        }
    }

    return $bullets
}

function Get-VouchedKeys {
    <#
        Any commit in the range that carries the trailer vouches for every bullet
        present in the file at that commit. That is what lets a trailer be added
        on a later commit than the one that first wrote the bullet, which
        red-green-refactor makes routine.
    #>
    param([string] $Range)

    $vouched = [System.Collections.Generic.HashSet[string]]::new()

    $shas = @(& git log --format='%H' $Range -- $Path)
    if ($LASTEXITCODE -ne 0) { return ,$vouched }

    foreach ($sha in $shas) {
        if ([string]::IsNullOrWhiteSpace($sha)) { continue }

        $values = @(& git log -1 --format="%(trailers:key=$Trailer,valueonly)" $sha)
        $claims = ($values -join ' ').Trim()
        if ($claims -ne $Vouch) { continue }

        foreach ($bullet in (Get-Bullets (Get-FileAtRev $sha))) {
            [void] $vouched.Add($bullet.Key)
        }
    }

    # Comma, or PowerShell unrolls the set to its elements on the way out.
    return ,$vouched
}

function Test-Vocabulary {
    <#
        The check polices its own marks. A mark spelled "Derived:" or written
        without its colon is the convention drifting, and drift here is
        indistinguishable from an unmarked bullet to everything downstream.
    #>
    param([string[]] $Lines)

    $problems = @()

    for ($n = 0; $n -lt $Lines.Count; $n++) {
        $line = $Lines[$n]

        if ($line -match "(?:^|[.)]\s|\p{Pd}\s)($WrongVocabulary)\s*:") {
            $problems += [pscustomobject]@{
                Line = $n + 1
                Why  = "'$($Matches[1]):' is not one of the marks. Use 'Not his call:' or 'Unrecorded:'."
            }
        }

        if ($line -match '(?:^|[.)]\s|\p{Pd}\s)(Not his call|Unrecorded)(?!:)') {
            $problems += [pscustomobject]@{
                Line = $n + 1
                Why  = "'$($Matches[1])' is missing its colon."
            }
        }
    }

    return $problems
}

# --- run ---------------------------------------------------------------------

$mergeBase = (& git merge-base $BaseRef $HeadRef 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($mergeBase)) {
    Write-Host "check-decision-marks: no merge base between $BaseRef and $HeadRef. Nothing to check."
    exit 0
}
$mergeBase = $mergeBase.Trim()

$headLines   = Get-FileAtRev $HeadRef
if ($headLines.Count -eq 0) {
    Write-Host "check-decision-marks: $Path does not exist at $HeadRef. Nothing to check."
    exit 0
}

$baseKeys    = [System.Collections.Generic.HashSet[string]]::new()
foreach ($bullet in (Get-Bullets (Get-FileAtRev $mergeBase))) { [void] $baseKeys.Add($bullet.Key) }

$headBullets = @(Get-Bullets $headLines)
$added       = @($headBullets | Where-Object { -not $baseKeys.Contains($_.Key) })

$failures = @()

if ($added.Count -gt 0) {
    $unmarked = @($added | Where-Object { -not $_.Marked })

    if ($unmarked.Count -gt 0) {
        $vouched = Get-VouchedKeys "$mergeBase..$HeadRef"
        foreach ($bullet in $unmarked) {
            if (-not $vouched.Contains($bullet.Key)) { $failures += $bullet }
        }
    }
}

$vocabulary = @(Test-Vocabulary $headLines)

if ($failures.Count -eq 0 -and $vocabulary.Count -eq 0) {
    Write-Host "check-decision-marks: OK. $($added.Count) bullet(s) added, all accounted for."
    exit 0
}

Write-Host ''
Write-Host "check-decision-marks: $Path"

foreach ($bullet in $failures) {
    Write-Host ''
    Write-Host "  line $($bullet.Line): added, and does not say whose decision it was"
    Write-Host "    $($bullet.Lead)..."
}

foreach ($problem in $vocabulary) {
    Write-Host ''
    Write-Host "  line $($problem.Line): $($problem.Why)"
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host '  Every bullet is in exactly one of three states:'
    Write-Host ''
    Write-Host '    (unmarked)       the maintainer decided it, or approved it'
    Write-Host '    Not his call:    Claude settled it. Never put to him'
    Write-Host '    Unrecorded:      predates any traceable discussion. Nobody knows'
    Write-Host ''
    Write-Host '  Add the mark as a trailing sentence saying why, or - if he decided'
    Write-Host "  it - put this trailer on a commit that touches the file:"
    Write-Host ''
    Write-Host "    ${Trailer}: ${Vouch}"
    Write-Host ''
    Write-Host '  If this fired on a reword rather than a new decision, the trailer is'
    Write-Host '  the escape, and the commit message is where you say which it was.'
}

Write-Host ''
exit 1
