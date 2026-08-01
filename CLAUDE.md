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
| `tests/` | One `.Tests.csproj` per production project |
| `assets/` | Icons, images, audio, other media |
| `docs/` | `decisions.md` and anything else written down |
| `scripts/` | Provisioning and local automation |

This table is a map of the repo, and it is wrong if it does not match what is on
disk. Every path in it exists.

## Building

`dotnet build` / `dotnet test` as usual. `ci.slnf` is everything CI runs — every
project that does not need real hardware. `hardware.slnf` will select the
dev-PC-only projects; it arrives with the first Tier 2 project, because a filter
naming no projects warns on every build.

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

## Working agreement

Architectural decisions go to the maintainer first: propose options and
tradeoffs, then stop. Also enforced in `.claude/settings.json`.

**Red, green, refactor — all three, on every story.** The third step is a pass
over the code with the tests green, looking for what only becomes visible once it
works. It is not a documentation update. Every pull request carries a `Refactor`
section saying what the pass found, or saying nothing needed changing and why it
looked — an empty one is a skipped step, not a clean bill.

Reasoning behind the decisions already made: `docs/decisions.md`.
