---
name: cmp
description: Ship shorthand for Directive 47. Use whenever the maintainer types a bare code built from CMP plus optional suffixes — CMP, CMPP, CMPM, CMPPIL, CMPPILB3, CMPB2, B3 and so on. CMP is commit, merge to main if on a branch, push to GitHub. Trailing P cuts a patch release, trailing M cuts a minor release, I installs the latest release, L launches it, and B[N] beeps N times as audible confirmation. Also use when asked to "ship it", "cut a patch", or "cut a minor".
---

# Ship shorthand

The maintainer works in VR and away from the keyboard. These codes are typed as
a bare prompt with no slash and no explanation. Execute them; do not ask what
they mean.

## Parsing

`CMP` is an **atomic prefix**, never three separate codes. Everything after it is
a suffix. This matters because the same letters mean different things in each
position:

| Position | Code | Meaning |
|---|---|---|
| In the `CMP` prefix | `C` | Commit |
| In the `CMP` prefix | `M` | Merge to `main`, if on a branch |
| In the `CMP` prefix | `P` | Push to GitHub |
| Suffix | `P` | Cut a **patch** release (SemVer) |
| Suffix | `M` | Cut a **minor** release (SemVer) |
| Suffix | `I` | Install the latest release |
| Suffix | `L` | Launch the installed app |
| Suffix | `B[N]` | Beep N times when finished |

Letter-by-letter parsing would be ambiguous — the trailing `P` in `CMPP` is
Patch, not a second Push. Read the prefix first, then the suffixes.

`B[N]` may appear alone. `B3` on its own means beep three times, nothing else.

Execute suffixes left to right, which is also their natural order: release →
install → launch → beep.

## Steps

### Preflight — always, before committing

Run `dotnet build cloud.slnf` and `dotnet test cloud.slnf`. **If either fails,
stop.** Do not commit. Report what broke, and sound the failure tone if a `B`
code was given. Never commit a red build to get to the release step.

### C — Commit

Stage everything and commit. Write the message to a file and use
`git commit -F <file>`: PowerShell mangles embedded double quotes when passing a
multi-line `-m` to a native executable.

Follow the message style already in the log — a subject line, a blank line, then
prose explaining *why*, not what. Read the last few commits if unsure.

### M — Merge to main

**On `main` already**: nothing to merge. Skip to push.

**On a branch**: this is a story, so it lands through a pull request, not a local
merge. Push the branch, open or update the PR with `Closes #N`, wait for CI,
merge on GitHub when green, delete the branch, return to `main` and pull.

The reason is not process purity: `docs/decisions.md` has CI publish an exe on
every PR, which is what the `I` code installs and what manual verification tests.
A local merge produces no PR, so no artifact, so nothing to install.

If the maintainer explicitly asks for a local merge in words, do that instead.

### P — Push

`git push origin <branch>`. Report the resulting SHA.

### Suffix P / M — Cut a release

**Not implemented.** Stop and say so. These need things that do not exist yet:

- No version anywhere — no `<Version>` in `Directory.Build.props`, no version
  file, no tags. There is nothing to increment. Where the version lives is an
  open decision; deriving it from tags would mean a dependency such as MinVer,
  which is a stop-and-ask under the refactor line in `docs/decisions.md`.
- No release pipeline. CI builds and tests; it does not publish.

Do not improvise a version scheme.

### I — Install latest release

**Not implemented.** No app, no installer. Velopack is a candidate in
`docs/decisions.md`; nothing is built. Stop and say so.

### L — Launch

**Not implemented.** No WPF project and no exe. Stop and say so.

Once it exists, launch the *installed* build, never `dotnet run` — the point is
to exercise what a user would actually have.

### B[N] — Beep

Both mechanisms are audible on the dev PC; `[console]::Beep` is used because
pitch and duration are controllable.

Success, N beeps:

```powershell
1..N | ForEach-Object { [console]::Beep(880, 180); Start-Sleep -Milliseconds 120 }
```

Failure — a distinct low double tone, so a failure is not mistaken for success
from across the room:

```powershell
1..2 | ForEach-Object { [console]::Beep(220, 400); Start-Sleep -Milliseconds 150 }
```

Beep last, after everything else has finished or failed. It means "done", so it
must not sound while work is still running.

## Partial completion

If a code asks for steps that are not implemented, do the implemented ones, then
stop and report exactly which step could not run and why. Do not silently skip
and do not pretend the whole code succeeded. Sound the failure tone, not the
success tone.
