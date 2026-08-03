# Directive 47

Voice-first companion layer for Elite Dangerous. .NET 10, C#, WPF panel,
SteamVR overlay.

**The value proposition:** never leave the cockpit. Anything you would normally
alt-tab for is available by voice, in a panel or in the headset. VR is a
first-class surface, not an afterthought.

## Where things live

| Path | Contents |
|---|---|
| `D47.slnx` | Solution, at the repo root |
| `src/` | Production projects, `D47.*` |
| `tests/` | One `.Tests.csproj` per production project, plus `D47.TestSupport` for machinery more than one of them needs |
| `assets/` | Icons, images, audio, other media |
| `docs/` | `decisions.md` and anything else written down |
| `scripts/` | Provisioning and local automation |

This table is a map of the repo, and it is wrong if it does not match what is on
disk. Every path in it exists.

## Building

`dotnet build` / `dotnet test` as usual. `ci.slnf` is everything CI runs — every
project that does not need real hardware. `hardware.slnf` selects the dev-PC-only
projects, currently `D47.GameOverlay` and its tests.

**CI is Windows only.** Directive 47 is a Windows product, so a Linux job was
proving a portability claim nothing depends on, and Windows is what lets WPF
layout snapshots gate a pull request. `scripts/setup-cloud.sh` still provisions
a Linux container if one is ever used, but that path is unsupported and nothing
verifies it.

The solution is the XML `.slnx` format, not the classic `.sln`. Solution filters
work against it unchanged. `dotnet new sln` defaults to `.slnx` on .NET 10.

## Referring to issues

The backlog lives in GitHub — `https://github.com/retiring-studios/directive-47`
— so issue numbers come up constantly.

**Always render them as markdown links with the full URL**, never a bare `#53`:

```markdown
[#53](https://github.com/retiring-studios/directive-47/issues/53)
```

A bare number costs a search every single time it is mentioned. This applies to
prose, tables, and lists alike — anywhere the maintainer might want to click
through.

## Working on stories

**Three stories run at a time, one worktree each.** One story at a time makes
thinking time into wall-clock time on a 24-core machine that is otherwise idle.

```powershell
git worktree add -b 125-a-short-slug C:\dev\d47-125 origin/main
```

Branch off `origin/main`, never off another story's branch — a worktree that
inherits unmerged work turns one review into two. After the pull request merges,
`git worktree remove C:\dev\d47-125`.

**Which three can run at once.** Stories in different `D47.*` projects are safe.
Two stories in the same project is the common trap: they will reach for the same
file, and the second one to merge pays for it.

A story that touches any of this **runs alone**, because every other worktree is
built on it:

| Shared surface | Why |
|---|---|
| `Directory.Build.props`, `Directory.Packages.props` | Versions and analyzer settings for every project |
| `D47.slnx`, `ci.slnf`, `hardware.slnf` | Anything that adds or moves a project touches all three |
| `global.json`, `.editorconfig`, `.gitattributes` | SDK band and formatting, repo-wide |
| `tests/D47.TestSupport` | Every test project references it |
| `docs/decisions.md`, `CLAUDE.md` | Append-heavy, so concurrent edits conflict at the same lines |

**The desktop is still one machine, and that is unchanged.** Three worktrees
running `dotnet test` do not contend for it, because desktop tests skip unless
`D47_DESKTOP_TESTS=1` says otherwise. The opt-in is the claim, so the existing
rule below still holds — ask before taking it, say when it is free. What is new
is that the answer is no more often, and that asking means naming **which
worktree** wants it.

**Nothing else about a story changes.** Red, green, refactor; the `Refactor`
section; `needs-manual-test`. This is how many run at once, not what a story is.

## Working agreement

Architectural decisions go to the maintainer first: propose options and
tradeoffs, then stop. Also enforced in `.claude/settings.json`.

**Red, green, refactor — all three, on every story.** The third step is a pass
over the code with the tests green, looking for what only becomes visible once it
works. It is not a documentation update. Every pull request carries a `Refactor`
section saying what the pass found, or saying nothing needed changing and why it
looked — an empty one is a skipped step, not a clean bill.

**Integration and E2E tests settle on CI once they work.** They seize the whole
machine while they run, so a finished one has no business executing again
locally. They skip by default; `D47_DESKTOP_TESTS=1` opts in.

**While writing one, running it locally is the right thing to do.** Pushing to
CI for every iteration of a test you are still shaping is slower for everyone.

So the rule is not "don't", it is **negotiate the machine rather than stomping on
it**. Before the first run, say you need the desktop and roughly for how long.
Say when you are finished with it. If a working test later needs debugging here,
ask first rather than assuming the earlier window still applies. Never loop runs
locally to chase a flaky result — that is what CI is for.

**`docs/decisions.md` records what the maintainer decided, in his words.** Do not
write a decision into it that he did not make. Prompt him instead — say what
needs deciding and why it came up — and let him write it himself.

The file is not a scratchpad for proposals. Everything in it reads afterwards as
settled, and a decision nobody made is indistinguishable from one that was,
which is how invented obligations end up being treated as debt. Recording the
*consequences* of his decision — what was built, what was measured, what failed
— is fine and is most of what the file already is. Inventing the decision is not.

**If something requires manual testing, it gets tested manually before merging,
and the `verified` label is that attestation.** `main` requires one check,
`Build, test and publish`. Nothing reads either label and nothing blocks on
them — that gate existed, appeared on every pull request whether or not there
was anything to look at, and was removed.

**Apply `needs-manual-test` yourself** when a pull request introduces something
a test cannot check, and say so when you hand it over. You wrote the code, so
you know what that is. A pull request without it is claiming the automated
suite covers everything in it.

Then say **what needs testing and how to test it**: the action, what a pass
looks like, and what would be a defect. A step that keeps reappearing is a hole
in automation — promote it rather than listing it again. A step that cannot be
written that concretely is usually not a check at all.

**Never apply `verified` yourself.** That one is his.

Reasoning behind the decisions already made: `docs/decisions.md`.
