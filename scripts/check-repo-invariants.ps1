#!/usr/bin/env pwsh
#
# Facts about this repository that two files have to agree on, and that nothing
# else checks.
#
# Each of these is currently guarded by a comment asking the next person to
# remember. A comment is a Tier 3 rule in the sense docs/decisions.md means it:
# ignorable, silently. This is the Tier 1 version — the build fails.
#
# Safe to run by hand, from anywhere:
#
#     ./scripts/check-repo-invariants.ps1
#
# It reads files and compares strings. No restore, no build, no network, so CI
# runs it early where it costs nothing.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$problems = @()

# --- Velopack and vpk must be the same version ------------------------------
#
# Directory.Packages.props pins the library the application links against;
# .config/dotnet-tools.json pins the tool that builds the installer. They are
# two halves of one format, and the comment in Directory.Packages.props says
# what happens when they drift: "A library and a tool that disagree about the
# package format is the kind of thing that fails on somebody else's machine at
# install time."
#
# That failure is invisible here. CI packs an installer and runs it, but it
# packs with the tool and installs what the tool produced, so a mismatched pair
# agrees with itself all the way through a green build and disagrees only with
# the copy already on a Commander's machine.

$packagesFile = Join-Path $root 'Directory.Packages.props'
$toolsFile = Join-Path $root '.config/dotnet-tools.json'

[xml]$packages = Get-Content $packagesFile -Raw

# SelectNodes with an XPath would need the MSBuild namespace declared, and this
# file has no namespace on it today. Walking the adapter is shorter and does not
# care either way.
$velopack = $packages.Project.ItemGroup.PackageVersion |
    Where-Object { $_.Include -eq 'Velopack' } |
    Select-Object -First 1

$tools = Get-Content $toolsFile -Raw | ConvertFrom-Json
$vpk = $tools.tools.vpk

if (-not $velopack) {
    $problems += "Directory.Packages.props has no PackageVersion for Velopack."
}
elseif (-not $vpk) {
    $problems += ".config/dotnet-tools.json has no vpk tool."
}
elseif ($velopack.Version -ne $vpk.version) {
    $problems +=
        "Velopack is $($velopack.Version) in Directory.Packages.props but vpk " +
        "is $($vpk.version) in .config/dotnet-tools.json. They build and read " +
        "the same package format and have to match."
}
else {
    Write-Host "Velopack and vpk agree at $($velopack.Version)."
}

# --- The CLAUDE.md path table has to match what is on disk ------------------
#
# CLAUDE.md says of its own table: "This table is a map of the repo, and it is
# wrong if it does not match what is on disk. Every path in it exists." That is
# a claim a script can settle, and a stale map is worse than no map — it sends
# the next session looking in the wrong place with confidence.
#
# Only the leading `| \`path\` |` cell of each row is read, and only rows whose
# path exists as a literal are checked. Prose in the second column is not a
# path and is not treated as one.

$claude = Join-Path $root 'CLAUDE.md'
$mapped = 0

foreach ($line in Get-Content $claude) {
    if ($line -notmatch '^\s*\|\s*`([^`]+)`\s*\|') { continue }

    $path = $Matches[1]

    # Rows describing a family of files rather than one file. A glob is a
    # statement about a shape, and Test-Path answers it for the shape rather
    # than for a name, which is what is wanted here.
    if (-not (Test-Path (Join-Path $root $path))) {
        $problems += "CLAUDE.md's table names ``$path``, which is not on disk."
    }

    $mapped++
}

if ($mapped -eq 0) {
    # The table having moved or changed shape is itself worth knowing. A check
    # that silently stops checking is the failure docs/decisions.md keeps
    # naming: "a test that passes because it checked nothing is worse than no
    # test."
    $problems += "No path rows found in CLAUDE.md's table — the check has stopped checking."
}
else {
    Write-Host "$mapped path(s) in CLAUDE.md's table, all present."
}

# --- Report -----------------------------------------------------------------

if ($problems.Count -gt 0) {
    Write-Host ''
    foreach ($problem in $problems) { Write-Host $problem }
    Write-Host ''
    Write-Host "$($problems.Count) invariant(s) broken."
    exit 1
}

Write-Host 'Repository invariants hold.'
exit 0
