#!/usr/bin/env pwsh
#
# Every direct package, and whether its licence is one this repository ships.
#
# docs/decisions.md settles the policy and this script is the half that was
# missing: "A CI license scan over direct packages is agreed and not yet built."
# Until it existed, the enforcement was Directory.Packages.props sitting in
# permissions.ask — a new package stopped for the maintainer, and he read the
# licence. That gate has been removed, so this is what replaces it. The two
# swap: a human prompt for a build failure.
#
# An allowlist, not a blocklist, and the reasoning is worth keeping in front of
# whoever edits it: "A blocklist fails on the license nobody thought to list,
# and that is exactly how GPL codec binaries reached a shipped installer in the
# predecessor project."
#
# Direct dependencies only. The decision scopes it there deliberately, with one
# exception it also names — packages carrying native binaries get a look,
# because those are the ones whose licence travels somewhere a managed
# assembly's does not. That look is printed rather than judged: "it is a glance,
# not an audit."
#
# Safe to run by hand, after a restore:
#
#     ./scripts/check-package-licenses.ps1
#
# It reads the nuspec out of the global packages folder rather than asking the
# network, so it costs about two seconds and works offline.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'D47.slnx'

# The eight from docs/decisions.md, verbatim. Anything else — including a
# package declaring no licence at all — stops and needs an exception recorded
# there, not here.
$allowed = @(
    'MIT'
    'BSD-2-Clause'
    'BSD-3-Clause'
    'Apache-2.0'
    'MS-PL'
    'Unlicense'
    'CC0-1.0'
    'ISC'
)

$packagesRoot = $env:NUGET_PACKAGES
if (-not $packagesRoot) {
    $packagesRoot = Join-Path $HOME '.nuget/packages'
}

# --- What is actually referenced --------------------------------------------

# Captured before it is parsed, and the exit code read before either. Piping
# straight into ConvertFrom-Json means a dotnet that failed and printed a
# sentence gets reported as a JSON syntax error, which sends whoever reads the
# log looking in the wrong place.
$output = & dotnet list $solution package --format json 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host ($output -join [Environment]::NewLine)
    throw "dotnet list package failed. Has the solution been restored?"
}

$listed = $output | ConvertFrom-Json

# The same package is referenced by several projects and by several target
# frameworks within them. One row per package, keyed on id and resolved
# version, is what a licence question is actually about.
$direct = @{}

foreach ($project in $listed.projects) {
    if (-not $project.frameworks) { continue }

    foreach ($framework in $project.frameworks) {
        foreach ($package in $framework.topLevelPackages) {
            $key = "$($package.id)/$($package.resolvedVersion)"
            if (-not $direct.ContainsKey($key)) {
                $direct[$key] = [pscustomobject]@{
                    Id      = $package.id
                    Version = $package.resolvedVersion
                }
            }
        }
    }
}

if ($direct.Count -eq 0) {
    # A scan that finds nothing to scan is not a pass. docs/decisions.md keeps
    # returning to this: "a test that passes because it checked nothing is
    # worse than no test — it reports confidence it never earned."
    throw "No direct packages found. Either the solution is not restored or the output shape has changed."
}

# --- Read each licence out of the nuspec ------------------------------------

$rows = @()
$refused = @()
$native = @()

foreach ($package in ($direct.Values | Sort-Object Id)) {
    # NuGet normalises both to lower case on disk.
    $folder = Join-Path $packagesRoot ($package.Id.ToLowerInvariant())
    $folder = Join-Path $folder ($package.Version.ToLowerInvariant())
    $nuspec = Get-ChildItem $folder -Filter *.nuspec -File -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if (-not $nuspec) {
        $refused += "$($package.Id) $($package.Version): no nuspec under $folder."
        continue
    }

    [xml]$spec = Get-Content $nuspec.FullName -Raw

    # PowerShell's XML adapter matches on local name, so the nuspec's default
    # namespace — which differs by the schema version the package was built
    # with — does not have to be declared to walk it.
    $metadata = $spec.package.metadata
    $licence = $metadata.license

    $expression = $null
    $how = $null

    if ($licence -is [string] -and $licence) {
        # No type attribute at all. Older packaging wrote the expression bare.
        $expression = $licence.Trim()
        $how = 'expression'
    }
    elseif ($licence -and $licence.type -eq 'expression') {
        $expression = $licence.InnerText.Trim()
        $how = 'expression'
    }
    elseif ($licence -and $licence.type -eq 'file') {
        # The licence is a file inside the package. That may well be one of the
        # eight, but reading it is judgement rather than a string comparison,
        # which is exactly what the maintainer is for.
        $how = "file ($($licence.InnerText.Trim()))"
    }
    elseif ($metadata.licenseUrl) {
        # Deprecated by NuGet years ago and still common on older packages. A
        # URL is not a declaration — following it and deciding is a person's
        # job.
        $how = "licenseUrl only ($($metadata.licenseUrl))"
    }
    else {
        $how = 'none declared'
    }

    # SPDX allows compound expressions. Every part has to be acceptable,
    # because "MIT OR GPL-3.0-only" still offers a licence this repository will
    # not ship under, and picking the good half is a decision to record rather
    # than one to infer.
    $ok = $false

    if ($expression) {
        $parts = $expression -split '\s+(?:OR|AND|WITH)\s+' |
            ForEach-Object { $_.Trim('(', ')', ' ') } |
            Where-Object { $_ }

        $ok = $parts.Count -gt 0 -and -not ($parts | Where-Object { $allowed -notcontains $_ })
    }

    $rows += [pscustomobject]@{
        Package = "$($package.Id) $($package.Version)"
        Licence = if ($expression) { $expression } else { $how }
        Ok      = $ok
    }

    if (-not $ok) {
        $refused += "$($package.Id) $($package.Version): $(if ($expression) { "licence $expression" } else { $how })."
    }

    # The glance at native binaries. Printed whatever the licence says, because
    # what it is for is noticing that something ships outside the managed
    # assembly at all.
    $carried = Get-ChildItem (Join-Path $folder 'runtimes') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -match '[\\/]native[\\/]?' }

    if ($carried) {
        $native += "$($package.Id) $($package.Version): $($carried.Count) native file(s) under runtimes/."
    }
}

# --- Report -----------------------------------------------------------------

$rows | ForEach-Object {
    $mark = if ($_.Ok) { ' ' } else { '!' }
    Write-Host ("{0} {1,-52} {2}" -f $mark, $_.Package, $_.Licence)
}

if ($native) {
    Write-Host ''
    Write-Host 'Ships native binaries — worth a look when the version changes:'
    foreach ($line in $native) { Write-Host "  $line" }
}

if ($refused.Count -gt 0) {
    Write-Host ''
    foreach ($line in $refused) { Write-Host $line }
    Write-Host ''
    Write-Host "$($refused.Count) package(s) outside the allowlist."
    Write-Host 'Either the package goes, or the exception goes in docs/decisions.md and the allowlist here.'
    exit 1
}

Write-Host ''
Write-Host "$($rows.Count) direct package(s), all inside the allowlist."
exit 0
