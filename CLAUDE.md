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
disk. `assets/` does not exist yet.

## Building

Cloud containers ship no .NET SDK. `scripts/setup-cloud.sh` installs it, and the
cloud environment's setup step is configured to run it. On the dev PC the SDK is
already installed and that script is not used.

Then `dotnet build` / `dotnet test` as usual. `cloud.slnf` runs without hardware.
`hardware.slnf` will select the dev-PC-only projects; it arrives with the first
Tier 2 project, because a filter naming no projects warns on every build.

The solution is the XML `.slnx` format, not the classic `.sln`. Solution filters
work against it unchanged. `dotnet new sln` defaults to `.slnx` on .NET 10.

## Working agreement

Architectural decisions go to the maintainer first: propose options and
tradeoffs, then stop. Also enforced in `.claude/settings.json`.

Reasoning behind the decisions already made: `docs/decisions.md`.
