<#
.SYNOPSIS
    Exercises check-decision-marks.ps1 against synthetic repositories.

.DESCRIPTION
    A check that cannot fail is worse than no check, so every case below asserts
    an exit code rather than that the script ran. Each case builds a throwaway
    repository, so nothing here depends on this repository's own history.

.EXAMPLE
    pwsh scripts/test-check-decision-marks.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$check  = Join-Path $PSScriptRoot 'check-decision-marks.ps1'
$root   = Join-Path ([System.IO.Path]::GetTempPath()) ("d47-marks-" + [guid]::NewGuid().ToString('N'))
$passed = 0
$failed = @()

function New-Repo {
    param([string] $Name)

    $dir = Join-Path $root $Name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $dir 'docs') -Force | Out-Null

    Push-Location $dir
    & git init --quiet --initial-branch=main
    & git config user.name  'Test'
    & git config user.email 'test@example.com'
    & git config commit.gpgsign false
    & git config core.autocrlf false
    Pop-Location

    return $dir
}

function Set-Decisions {
    param([string] $Dir, [string[]] $Lines)

    $path = Join-Path $Dir 'docs/decisions.md'
    ($Lines -join "`n") + "`n" | Out-File -FilePath $path -Encoding utf8 -NoNewline
}

function Save-Commit {
    param([string] $Dir, [string] $Message)

    Push-Location $Dir
    & git add -A
    & git commit --quiet -m $Message
    Pop-Location
}

function Baseline {
    param([int] $Count = 30)

    $lines = @('# Decisions', '', '## Platform', '')
    for ($n = 1; $n -le $Count; $n++) {
        $lines += "- **Bullet $n.** Something settled a while ago, with a reason"
        $lines += "  that runs onto a second line the way the real file does."
        $lines += ''
    }
    return $lines
}

# Whichever host is running this, rather than a hard-coded pwsh: the dev PC has
# Windows PowerShell only, which is how the settings.json hook in #287 came to be
# inert for a week.
$psExe = (Get-Process -Id $PID).Path

function Invoke-Check {
    param([string] $Dir, [string] $BaseRef = 'main', [string] $HeadRef = 'HEAD')

    Push-Location $Dir
    $output = & $psExe -NoProfile -File $check -BaseRef $BaseRef -HeadRef $HeadRef 2>&1
    $code = $LASTEXITCODE
    Pop-Location

    return [pscustomobject]@{ Code = $code; Output = ($output -join "`n") }
}

function Assert-Case {
    param([string] $Name, [int] $Expected, [pscustomobject] $Result)

    if ($Result.Code -eq $Expected) {
        Write-Host "  pass  $Name"
        $script:passed++
    }
    else {
        Write-Host "  FAIL  $Name (expected exit $Expected, got $($Result.Code))"
        $script:failed += [pscustomobject]@{ Name = $Name; Result = $Result }
    }
}

function Start-Branch {
    param([string] $Dir, [string] $Name)

    Push-Location $Dir
    & git checkout --quiet -b $Name
    Pop-Location
}

Write-Host ''
Write-Host 'check-decision-marks'
Write-Host ''

try {
    # 1 - nothing added
    $dir = New-Repo 'untouched'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('Some prose at column zero, not a bullet.', ''))
    Save-Commit $dir 'Add prose'
    Assert-Case 'no bullet added passes' 0 (Invoke-Check $dir)

    # 2 - added and marked "Not his call:"
    $dir = New-Repo 'marked-not-his-call'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **A new call.** Settled while building. Not his call: the code needed', '  an answer before he was available.', ''))
    Save-Commit $dir 'Add a derived bullet'
    Assert-Case 'added bullet marked Not his call passes' 0 (Invoke-Check $dir)

    # 3 - added and marked "Unrecorded:"
    $dir = New-Repo 'marked-unrecorded'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **An old call.** Unrecorded: predates any traceable discussion.', ''))
    Save-Commit $dir 'Add an unattributable bullet'
    Assert-Case 'added bullet marked Unrecorded passes' 0 (Invoke-Check $dir)

    # 4 - added, unmarked, no trailer
    $dir = New-Repo 'unmarked-no-trailer'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **A silent call.** No mark, and nobody vouched for it.', ''))
    Save-Commit $dir 'Add a bullet quietly'
    Assert-Case 'added bullet with no mark and no trailer FAILS' 1 (Invoke-Check $dir)

    # 5 - added, unmarked, trailer on the same commit
    $dir = New-Repo 'unmarked-with-trailer'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **His call.** He decided this in chat.', ''))
    Save-Commit $dir "Record his decision`n`nDecided-By: maintainer"
    Assert-Case 'added bullet vouched by trailer passes' 0 (Invoke-Check $dir)

    # 6 - written in one commit, marked in a later one (red, green, refactor)
    $dir = New-Repo 'marked-later'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **A call in progress.** Written before it was marked.', ''))
    Save-Commit $dir 'Draft the bullet'
    Set-Decisions $dir ((Baseline) + @('- **A call in progress.** Written before it was marked. Not his call:', '  settled mid-story.', ''))
    Save-Commit $dir 'Mark it'
    Assert-Case 'bullet marked in a later commit passes' 0 (Invoke-Check $dir)

    # 7 - written in one commit, vouched by a later one
    $dir = New-Repo 'vouched-later'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **His call, written first.** Trailer came afterwards.', ''))
    Save-Commit $dir 'Draft the bullet'
    Set-Decisions $dir ((Baseline) + @('- **His call, written first.** Trailer came afterwards.', '', '- **Another of his.** Same conversation.', ''))
    Save-Commit $dir "Record what he decided`n`nDecided-By: maintainer"
    Assert-Case 'bullet vouched by a later commit passes' 0 (Invoke-Check $dir)

    # 8 - the bulk Unrecorded pull request: marks added to existing bullets
    $dir = New-Repo 'bulk-marking'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    $bulk = @('# Decisions', '', '## Platform', '')
    for ($n = 1; $n -le 30; $n++) {
        $bulk += "- **Bullet $n.** Something settled a while ago, with a reason"
        $bulk += "  that runs onto a second line the way the real file does."
        if ($n -le 24) { $bulk += "  Unrecorded: predates any traceable discussion." }
        $bulk += ''
    }
    Set-Decisions $dir $bulk
    Save-Commit $dir 'Mark the unattributable bullets'
    Assert-Case 'bulk marking 24 existing bullets passes' 0 (Invoke-Check $dir)

    # 9 - rewrapping an untouched bullet
    $dir = New-Repo 'rewrapped'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    $wrapped = @('# Decisions', '', '## Platform', '')
    for ($n = 1; $n -le 30; $n++) {
        if ($n -eq 7) {
            $wrapped += "- **Bullet 7.** Something settled a while ago, with a reason that runs onto a second line the way the real file does."
        }
        else {
            $wrapped += "- **Bullet $n.** Something settled a while ago, with a reason"
            $wrapped += "  that runs onto a second line the way the real file does."
        }
        $wrapped += ''
    }
    Set-Decisions $dir $wrapped
    Save-Commit $dir 'Rewrap one bullet'
    Assert-Case 'rewrapping an existing bullet passes' 0 (Invoke-Check $dir)

    # 10 - wrong vocabulary
    $dir = New-Repo 'wrong-word'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **A new call.** Derived: settled while building.', ''))
    Save-Commit $dir 'Use the wrong mark'
    Assert-Case 'Derived: as a mark FAILS' 1 (Invoke-Check $dir)

    # 11 - right words, no colon
    $dir = New-Repo 'no-colon'
    Set-Decisions $dir (Baseline); Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **A new call.** Not his call settled while building.', ''))
    Save-Commit $dir 'Drop the colon'
    Assert-Case 'a mark missing its colon FAILS' 1 (Invoke-Check $dir)

    # 12 - a mark removed without a trailer to justify it
    $dir = New-Repo 'mark-removed'
    $withMark = (Baseline) + @('- **A settled call.** Not his call: settled mid-story.', '')
    Set-Decisions $dir $withMark; Save-Commit $dir 'Baseline'
    Start-Branch $dir 'work'
    Set-Decisions $dir ((Baseline) + @('- **A settled call.**', ''))
    Save-Commit $dir 'Quietly drop the mark'
    Assert-Case 'removing a mark with no trailer FAILS' 1 (Invoke-Check $dir)

    # 13 - the real file, against its own history
    Push-Location $PSScriptRoot
    $repoRoot = (& git rev-parse --show-toplevel).Trim()
    Pop-Location
    $real = Invoke-Check $repoRoot 'HEAD' 'HEAD'
    Assert-Case 'the real decisions.md passes against itself' 0 $real
}
finally {
    if (Test-Path $root) { Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue }
}

Write-Host ''
if ($failed.Count -eq 0) {
    Write-Host "$passed passed."
    Write-Host ''
    exit 0
}

foreach ($case in $failed) {
    Write-Host "--- $($case.Name)"
    Write-Host $case.Result.Output
}
Write-Host ''
Write-Host "$passed passed, $($failed.Count) failed."
Write-Host ''
exit 1
