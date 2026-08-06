#!/usr/bin/env pwsh
#
# Parses every piece of PowerShell in this repository — the `run: |` blocks in
# .github/workflows, and every .ps1 file — and fails if any of them has a
# syntax error.
#
# A block is only compiled when its step runs, so a typo sits there until
# something executes it. For the release job that means a release day: its
# blocks only run on a v* tag. Reading them all takes a couple of seconds, so
# CI does it on every pull request and the failure lands on the change that
# wrote it.
#
# The bug that prompted this was `Write-Host "Installed under $root:"` — a
# colon straight after a variable name is PowerShell's drive-qualifier syntax,
# and it cost a full CI run to find.
#
# The .ps1 half was added for one file that nothing else can reach.
# .claude/hooks/guard-git.ps1 runs on the dev PC and only there — CI never
# invokes it, so CI never finds out it does not parse. A hook that throws on
# every tool call is worse than no hook, because the way that gets fixed at
# eleven at night is by turning it off. The scripts CI does run are covered
# incidentally: a syntax error in one of those already fails the step that
# runs it, just later and with a worse message.
#
# Safe to run by hand, from anywhere:
#
#     ./scripts/check-powershell.ps1
#
# Every runner in the workflow is windows-latest, where the default shell is
# pwsh, so every block here is PowerShell. A step declaring `shell: bash` would
# need skipping; none does.
#
# It parses with whichever PowerShell is running it, which on CI is pwsh 7 and
# on a dev machine may be Windows PowerShell 5.1. A block using syntax only
# pwsh 7 has would be flagged by a 5.1 run and not by CI.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$workflows = Join-Path $root '.github/workflows'
$blocks = 0
$scripts = 0
$bad = 0

foreach ($file in Get-ChildItem $workflows -File -Include *.yml, *.yaml -Recurse) {
    $lines = @(Get-Content $file.FullName)

    for ($i = 0; $i -lt $lines.Count; $i++) {
        # Only literal block scalars. A folded `run: >` is one command line and
        # has nothing to parse.
        if ($lines[$i] -notmatch '^(\s*)run:\s*\|[-+]?\s*$') { continue }

        # The block is everything indented past the `run:` key, blank lines
        # included.
        $indent = $Matches[1].Length
        $body = @()
        $at = $i + 1
        while ($at -lt $lines.Count -and (
                $lines[$at].Trim() -eq '' -or
                ($lines[$at] -replace '\S[\s\S]*$').Length -gt $indent)) {
            $body += $lines[$at]
            $at++
        }

        # ${{ }} is GitHub's, not PowerShell's, and it is substituted before
        # the shell ever sees the script. A placeholder stands in so the parser
        # reads what will actually run.
        $script = ($body -join "`n") -replace '\$\{\{[\s\S]*?\}\}', '__GHA_EXPR__'

        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseInput(
            $script, [ref]$null, [ref]$errors)

        $blocks++

        foreach ($e in $errors) {
            $bad++
            # +1 for the run: line itself, +1 to make $i one-based.
            $line = $i + 1 + $e.Extent.StartLineNumber
            Write-Host "$($file.Name) line ${line}: $($e.Message)"
        }
    }
}

# Every .ps1 in the repository, parsed whole. Nothing here is extracted or
# substituted first — a file is already the thing PowerShell reads.
foreach ($file in Get-ChildItem $root -File -Filter *.ps1 -Recurse) {
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile(
        $file.FullName, [ref]$null, [ref]$errors)

    $scripts++

    foreach ($e in $errors) {
        $bad++
        $relative = $file.FullName.Substring($root.Length).TrimStart('/', '\')
        Write-Host "${relative} line $($e.Extent.StartLineNumber): $($e.Message)"
    }
}

if ($bad -gt 0) {
    Write-Host "$bad parse error(s) across $blocks run: block(s) and $scripts script(s)."
    exit 1
}

Write-Host "$blocks run: block(s) and $scripts script(s) parsed, no syntax errors."
exit 0
