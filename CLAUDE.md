# Directive 47

Voice-first companion layer for Elite Dangerous. .NET 10, C#, WPF panel,
SteamVR overlay.

**The value proposition:** anything you would normally alt-tab for is available
by voice, in a panel or in the headset. VR is a first-class surface, not an
afterthought.

## Where things live

| Path | Contents |
|---|---|
| `D47.sln` | Solution, at the repo root |
| `src/` | Production projects, `D47.*` |
| `tests/` | One `.Tests.csproj` per production project |
| `assets/` | Icons, images, audio, other media |
| `docs/` | `decisions.md` and anything else written down |
| `scripts/` | Provisioning and local automation |

Nothing is built yet. This table is a map of the repo, and it is wrong if it
does not match what is on disk.

## Building

Cloud containers ship no .NET SDK. `scripts/setup-cloud.sh` installs it, and the
cloud environment's setup step is configured to run it. On the dev PC the SDK is
already installed and that script is not used.

Then `dotnet build` / `dotnet test` as usual. `cloud.slnf` runs without hardware,
`hardware.slnf` needs the dev PC.

## Working agreement

Architectural decisions go to the maintainer first: propose options and
tradeoffs, then stop. Also enforced in `.claude/settings.json`.

Reasoning behind the decisions already made: `docs/decisions.md`.
