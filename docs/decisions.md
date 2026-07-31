# Decisions

Architectural decisions and the reasoning behind them. These were argued once;
the reasoning is recorded so it does not get rediscovered.

This file is descriptive. Nothing here enforces itself — see
[Enforcement](#enforcement) for where the load-bearing rules actually live.

## Platform

- **.NET 10, Windows.**
- **WPF for the desktop panel** — not WinUI, not Avalonia. Mature UIA automation
  (FlaUI) and a simple render-to-bitmap path for the VR overlay.
- **SteamVR only.** OpenXR is a someday nice-to-have and appears in no
  definition of done. One `IHeadsetOverlay` interface with a single SteamVR
  implementation keeps the door open without paying for it now.

## Architecture

- **Capabilities declare, surfaces render.** A capability contributes a
  descriptor: a data object saying what it is and what to show. It carries an
  LLM tool schema, a display model, help text, and example utterances. The
  desktop panel, VR overlay, and voice layer all consume it, so no capability
  references a UI assembly and none of them contains feature-specific rendering
  code.
- **The descriptor has an escape hatch, and it is not optional to fill.** The
  display model covers text, lists, and key-value readouts, which is most of
  what anyone alt-tabs for. A capability needing something the model cannot
  express — a map, a chart, a plotted route — may supply its own view instead.
  The cost of using it is that the parity test then demands a view for *every*
  surface, not just the one that motivated it. Without the hatch, the display
  model grows a `Chart` concept, then a `Map` concept, and becomes a UI
  framework nobody chose to write.
- **Example utterances are examples, not a matcher.** They are few-shot
  examples inside the tool schema, helping the LLM map a sloppy transcription to
  the right capability, and "try saying…" text so the panel and headset are
  discoverable. Nothing matches phrases against speech-to-text output.
  Transcription of system, ship, and commodity names is exactly where STT fails,
  and a phrase matcher fails silently when it misses. This bullet previously
  read "voice phrasings", which implied the matcher.
- **VR parity by projection.** The desktop panel is built at VR-legible density
  (large type, high contrast, low density), and the overlay is that same render
  target blitted to a texture.
- **Parity is an enumerated test, not a checklist.** Enumerate all capabilities
  against all surfaces and assert a descriptor exists for each pairing. A
  missing VR descriptor is a red build, not a missed checkbox. The test must
  also assert that the enumeration is not empty. A discovery-based test that
  finds nothing passes, and a test that passes because it checked nothing is
  worse than no test — it reports confidence it never earned.
- **`TimeProvider` and `FakeTimeProvider`**, not a hand-rolled `IClock`.
- **Interfaces at every process boundary.** NSubstitute for stubs; hand-written
  fakes for stateful things such as the journal source and the audio device.

## Test tiers

The tiers drive the project layout, not just test selection.

| Tier | Contents | Runs |
|---|---|---|
| 0 · Pure | Parsers, planners, routing, state machines, display models, capability descriptors | Cloud |
| 1 · Integration, no hardware | Providers against recorded HTTP, journal parsing over fixture logs, layout snapshots | Cloud |
| 2 · Hardware | Mic, WASAPI, hotkeys, SendInput, SteamVR overlay | Dev PC |
| 3 · Game | Elite Dangerous running — real journal, keybinds actually landing | Dev PC + game |

- **Tiers 2 and 3 contain adapters only, no logic.** If a bug needs the game to
  reproduce, it should live in a file readable in one screen.
- **One `.Tests.csproj` per production `.csproj`.** The production project's
  tier determines the kind of tests in its pair. A project whose tests would
  span two tiers is two projects.
- **Selection via solution filters** (`cloud.slnf`, `hardware.slnf`), not
  per-test traits.

## Fixtures

- Two journal corpora: sanitized real captures, and synthetic ones built through
  a `JournalBuilder` fluent API.
- **Real journals carry the Commander name and play history.** They are scrubbed
  into `tests/` before anything lands in this public repo. Raw captures are
  gitignored.
- Recorded HTTP responses back the provider tests.

## Quality gates

- **Size limits are strictest on adapters, not loosest.** "Thin adapter" is an
  architectural invariant, and the size metric is how it is enforced. Only
  cyclomatic complexity gets a carve-out, and only per-construct (flat dispatch
  and mappers), never per-directory.
- **Coverage is not gated on a global percentage.** It produces tests written
  for the number. Per-tier at most.
- Specific analyzer thresholds are deferred.

## Enforcement

If a rule matters, it must not live only in a Markdown file.

| Tier | Mechanism | Can it be ignored? |
|---|---|---|
| 1 | MSBuild, analyzers, architecture tests | No — the build fails |
| 2 | `settings.json` hooks and permission rules | No — the harness blocks |
| 3 | `CLAUDE.md`, `docs/decisions.md` | Yes, silently |

`CLAUDE.md` is descriptive by design: a map of what lives where, how to build,
and which decisions not to re-litigate.

**Cloud provisioning is not a SessionStart hook.** It was one briefly. A hook
command runs through a shell, and the shell on the dev PC is PowerShell, which
cannot invoke a bash script — so a hook whose only job is provisioning Linux
containers would have errored at the start of every local session. Noise that
recurs is noise that gets suppressed, and a suppressed hook is worse than none.
Provisioning lives in `scripts/setup-cloud.sh`, which the cloud environment's
setup step runs. The script stays in version control either way; only the
trigger moved.

## Process

- Acceptance criteria are written before code and phrased so they become test
  names. They live in the issue body, where each criterion is a checkbox to tick
  off as tests land — not in a custom field, which would flatten them to a blob.
- One story, one branch, one PR (`Closes #N`). Review starts at the tests, then
  the diff.
- **Red, green, refactor — all three.** It is test *driven*, not merely test
  first. Red and green only prove the code works; the third step is where the
  design gets made, with a passing suite as the safety net. Skip it and TDD
  decays into writing the tests slightly earlier, which buys coverage and
  nothing else. Two signals only pay off if acted on while green: a test that is
  awkward to write means the design is wrong, and duplication appearing across
  the second and third test is telling you what the abstraction is. Test code
  gets refactored too.
- **The refactor line: one instance is mine, a general conclusion is yours.**
  Eliminating a specific redundancy is not an architectural decision. Deciding
  to *eliminate redundancies* is. A smell that resolves into a conclusion about
  the shape of the system — "this verbose, repetitive code is starting to look
  like NuGet package X, should we take the dependency?" — is architecture no
  matter how small the resulting diff. Extracting a method, renaming, collapsing
  duplication at a call site: done without asking, the tests prove it. Naming an
  abstraction that later code must conform to, taking a dependency, or adopting
  a pattern as policy: proposed first.
- The dependency half of that line is already enforced: `Directory.Packages.props`
  is in `permissions.ask`, so adding a package stops for the maintainer.
- **Manual test steps are per-PR only**, never an accumulating file. Regression
  is the automated hardware tier's job; the human pass is for new behavior and
  judgment calls. A manual test that keeps recurring is a hole in automation —
  promote it, do not list it.
- CI publishes a single-file exe on every PR, so manual passes test an installed
  build rather than `dotnet run`.
- **Definition of done includes a manual verification pass by the maintainer.**
  GitHub does not allow approving your own pull request, so the attestation is
  not a review approval: it is a required `manual-verification` status check
  satisfied by applying the `verified` label, which new commits strip.

## Releases

| Channel | Trigger | Audience |
|---|---|---|
| CI artifact | Every PR | Maintainer |
| Pre-release | Feature complete, merged to `main` | Opt-in |
| Stable | Epic complete | Default |

- Versioning: an epic bumps the minor version, a bug fix bumps the patch.
- Updater candidate is Velopack, on one condition: update availability must
  surface in the panel and the overlay, with installation deferred to app exit.
  No installer window yanking you out of VR.
