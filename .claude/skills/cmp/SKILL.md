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

### The build is CI's job

**No local build or test before committing.** CI runs `ci.slnf` on every push to
`main`, on every pull request and on every tag, so running it here first is the
same check twice — and the slower copy, on the machine the maintainer is trying
to stay out of.

Report the run after pushing so there is something to watch. If a step here
fails, Partial completion below says what to do.

The release suffixes are covered by the same run: the release job `needs` the
build job, so a red build publishes nothing. What a red build does leave behind
is a tag pointing at a broken commit, which then has to be moved or deleted —
that is why cutting a release watches the run rather than pushing and walking
away.

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

The version is one hand-maintained literal in `Directory.Build.props`, and CI
turns a tag beginning `v` into a GitHub Release carrying the exe the build job
already tested.

Be on `main`, clean, and up to date first. The tag releases whatever ref it
points at.

1. Read the current version the way CI reads it, so there is no second answer:

   ```powershell
   dotnet msbuild src/D47.Panel/D47.Panel.csproj -getProperty:Version
   ```

2. Edit the `<Version>` literal in `Directory.Build.props`. `P` bumps the patch;
   `M` bumps the minor and resets the patch to zero.
3. Commit that edit on its own.
4. Tag the commit that carries the version and push both:

   ```powershell
   git tag v0.1.1
   ```

   ```powershell
   git push origin main
   ```

   ```powershell
   git push origin v0.1.1
   ```

   The tag must point at the commit whose `Directory.Build.props` matches it.
   The release job compares the two and throws if they disagree, so a tag pushed
   before the bump lands fails the run rather than releasing the wrong number.

5. Watch the run and report the release URL.

Anything before `1.0.0` is published as a prerelease. That is inferred from the
version by the workflow, not passed by hand, so there is nothing to remember.

Do not improvise a version scheme. Whether an epic is major or minor is decided
when the epic is defined — `docs/decisions.md` has that, and it is not something
to infer from what changed.

### I — Install latest release

There is no installer yet — that is
[#142](https://github.com/retiring-studios/directive-47/issues/142), and Velopack
is still only a candidate in `docs/decisions.md`. What a release carries is a
self-contained single-file exe that needs no .NET on the machine, so installing
it means fetching it and keeping it somewhere.

**Ask for the tag explicitly.** Everything before `1.0.0` is a prerelease, and
`gh release download` with no tag resolves "latest" the way the API does, which
skips prereleases and fails with nothing to download. `gh release list` includes
them, newest first:

```powershell
$tag = gh release list --repo retiring-studios/directive-47 --limit 1 --json tagName --jq '.[0].tagName'
```

```powershell
gh release download $tag --repo retiring-studios/directive-47 --pattern D47.Panel.exe --dir "$env:USERPROFILE\Downloads\d47-$tag" --clobber
```

`$env:USERPROFILE\Downloads\d47-<tag>` matches what pull requests already tell
the maintainer to type for a per-pull-request artifact, so an installed release
and a build under manual test sit beside each other and neither overwrites the
other.

Report the path and the size. It is about 173MB and grows with every project
added.

### L — Launch

Run the exe that `I` downloaded:

```powershell
& "$env:USERPROFILE\Downloads\d47-$tag\D47.Panel.exe"
```

Launch the *installed* build, never `dotnet run` — the point is to exercise what
a user would actually have.

If `L` is given without `I` in the same code, launch the most recent
`d47-*` directory rather than assuming one was just fetched.

### B[N] — Beep

`[console]::Beep` is used because pitch and duration are controllable.

**Always send the priming tone first.** The dev PC's speakers are Bluetooth
(Logitech Z407). A2DP drops the link when idle and takes over a second to
re-establish, so tones sent to a sleeping link are simply lost — the first two
beeps of a plain sequence never arrive, and the maintainer counts the wrong
number. The prime is a sacrificial tone whose only job is to wake the link, and
the gap after it is what makes the counted tones survive. Verified at 1200 ms;
raise it if beeps start disappearing again.

Success, N beeps:

```powershell
[console]::Beep(110, 200)
Start-Sleep -Milliseconds 1200
1..N | ForEach-Object { [console]::Beep(880, 220); Start-Sleep -Milliseconds 140 }
```

Failure — a low double tone, so a failure is not mistaken for success from
across the room:

```powershell
[console]::Beep(110, 200)
Start-Sleep -Milliseconds 1200
1..2 | ForEach-Object { [console]::Beep(240, 450); Start-Sleep -Milliseconds 150 }
```

Beep last, after everything else has finished or failed. It means "done", so it
must not sound while work is still running.

If beeps are ever lost inconsistently rather than always at the start, stop
relying on a count: a rising two-tone for success and a low double for failure
survives losing a fragment in a way that counting cannot.

## Partial completion

If a step fails or cannot run, do the ones before it, then stop and report
exactly which step stopped and why. Do not silently skip and do not pretend the
whole code succeeded. Sound the failure tone, not the success tone.
