#!/usr/bin/env pwsh
#
# Refuses the handful of git operations that cannot be undone.
#
# This exists because permission rules are prefix matches and the operations
# worth refusing are not distinguishable by a prefix. `.claude/settings.json`
# denied `git push --force*` for months, and that rule never matched
# `git push origin main --force`, because the flag is not where the pattern
# says it is. While `git push` itself prompted, the gap cost nothing. Allowing
# `git push*` — which is what makes an unattended run possible — is what turns
# it into a hole, so the deny list is now belt and the whole command string,
# read here, is braces.
#
# It is a .ps1 on purpose. docs/decisions.md records why the last hook was
# removed: "a hook command runs through a shell, and the shell on the dev PC is
# PowerShell, which cannot invoke a bash script." A hook that errors at the
# start of every session gets suppressed, "and a suppressed hook is worse than
# none" — so this one is silent on the happy path, writes nothing to stdout
# ever, and does no work at all unless the command mentions git.
#
# What it deliberately does NOT refuse:
#
#   * An ordinary push to main. The cmp skill pushes main directly and
#     documents the remote's "Required status check" answer as something that
#     "reads like a rejection and is not". Branch protection is the thing that
#     decides whether that lands; refusing it here would break a shorthand the
#     maintainer types daily to guard something the server already guards.
#   * Deleting a story branch. The story loop does that after every merge.
#   * Pushing a tag. Releases are cut by a session now, and a tag is how.
#
# What it refuses is the set where the remote history, or uncommitted work, is
# gone and not coming back.

$ErrorActionPreference = 'Stop'

# --- Read the command -------------------------------------------------------
#
# On anything unexpected — no stdin, a shape that has changed under us, a tool
# that is not a shell — allow and say nothing. A guard that starts refusing
# everything the first time the harness changes its payload is a guard that
# gets turned off, and the operations below are already denied by prefix as
# well.

try {
    $raw = [Console]::In.ReadToEnd()
    if (-not $raw) { exit 0 }

    $payload = $raw | ConvertFrom-Json
    $command = $payload.tool_input.command

    if (-not $command) { exit 0 }
}
catch {
    exit 0
}

# Cheapest possible early out. Almost every command through this hook is a
# dotnet build or a Get-ChildItem, and those should not pay for any of what
# follows.
if ($command -notmatch '\bgit\b') { exit 0 }

# --- Look at each command in the line separately ----------------------------
#
# `cd src; git push --force` is one string and two commands, and only the
# second one matters. Splitting on the separators is coarse — it will split
# inside a quoted string — but the failure mode of being too eager here is a
# refusal with a clear reason, which is recoverable, and the failure mode of
# being too clever is missing one.

$refusals = @()

foreach ($segment in ($command -split '(?:&&|\|\||;|\||\r?\n)')) {
    $text = $segment.Trim()
    if ($text -notmatch '^\s*git\b') { continue }

    # Tokenise loosely. Quotes are stripped because a quoted --force is still a
    # --force.
    $tokens = @($text -split '\s+' | ForEach-Object { $_.Trim('"', "'") } | Where-Object { $_ })
    if ($tokens.Count -lt 2) { continue }

    # Not $args — that is an automatic variable and assigning to it at script
    # scope is asking for a bug that only shows up when something else reads it.
    $verb = $tokens[1]
    $rest = @($tokens | Select-Object -Skip 2)

    switch ($verb) {
        'push' {
            foreach ($arg in $rest) {
                if ($arg -in @('-f', '--force') -or $arg -like '--force-with-lease*') {
                    $refusals += "git push $arg rewrites history on the remote. If a branch genuinely needs replacing, delete it and push again."
                }
                if ($arg -eq '--mirror') {
                    $refusals += 'git push --mirror replaces every ref on the remote at once.'
                }
                if ($arg -eq '--prune') {
                    $refusals += 'git push --prune deletes remote branches that have no local counterpart.'
                }
                # A refspec beginning + is a force push wearing a different hat,
                # and it is the form that gets past a flag-shaped check.
                if ($arg -match '^\+\S*:' -or ($arg -match '^\+' -and $arg -notmatch '^--')) {
                    $refusals += "The refspec $arg begins with +, which forces the update."
                }
            }

            # Deleting main. Any other branch is the story loop cleaning up
            # after itself and is left alone.
            $deleting = $rest -contains '--delete' -or $rest -contains '-d'
            foreach ($arg in $rest) {
                $target = if ($arg -match ':') { ($arg -split ':', 2)[1] } else { $arg }
                $target = $target -replace '^refs/heads/', ''

                if ($target -eq 'main' -and ($deleting -or $arg -match '^:')) {
                    $refusals += 'That deletes main on the remote.'
                }
            }
        }

        'reset' {
            if ($rest -contains '--hard') {
                $refusals += 'git reset --hard throws away uncommitted work with no reflog entry to recover it from. git stash, or reset without --hard.'
            }
        }

        'clean' {
            # -f is required for clean to do anything, so its presence is the
            # signal. -n and --dry-run are the harmless forms.
            if (($rest -join ' ') -notmatch '(-n|--dry-run)\b' -and ($rest -join ' ') -match '-[a-z]*[fdx]') {
                $refusals += 'git clean deletes untracked files, which includes anything not yet added. Run it with -n first and read the list.'
            }
        }

        'filter-branch' {
            $refusals += 'git filter-branch rewrites every commit it touches.'
        }

        'update-ref' {
            if ($rest -contains '-d') {
                $refusals += 'git update-ref -d deletes a ref directly, bypassing everything that would otherwise record it.'
            }
        }
    }
}

if ($refusals.Count -eq 0) { exit 0 }

# stderr, and exit 2, is the shape that comes back to the session as something
# to act on rather than to the user as a broken hook.
foreach ($refusal in ($refusals | Select-Object -Unique)) {
    [Console]::Error.WriteLine("Refused by .claude/hooks/guard-git.ps1: $refusal")
}

exit 2
