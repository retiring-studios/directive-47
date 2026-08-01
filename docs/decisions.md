# Decisions

Architectural decisions and the reasoning behind them. These were argued once;
the reasoning is recorded so it does not get rediscovered.

This file is descriptive. Nothing here enforces itself — see
[Enforcement](#enforcement) for where the load-bearing rules actually live.

## Positioning

- **The value proposition is "never leave the cockpit," and 1.0.0 is its
  realization.** It is stated in the present tense on purpose: it is the target
  the work is measured against, kept in front of us rather than earned later.
  The MVP epic is scoped to cover every common reason to alt-tab — search,
  planning, outfitting, engineering, materials — which is what makes the claim
  true at 1.0.0 rather than aspirational forever. Honesty about what exists
  *today* lives in the README's Status section, not in a hedged tagline.
- **Third-party Elite tools are not named in the code, the docs, or the UI.**
  The exceptions are sources we actually draw information from — Spansh, EDSM,
  EDCD/Coriolis — which are named because attribution is owed. Competing
  companions are not mentioned at all, favourably or otherwise.
- **Personas are Guardian AIs.** Every preset is a flavour of one. `Ward` is the
  shipped default.

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
  LLM tool schema, a display model, help text, and example utterances. Every
  surface consumes it, so no capability references a UI assembly and none of
  them contains feature-specific rendering code.
- **A descriptor is the declaration of a function, as data — never a variable.**
  It is registered once at startup and never mutates. What a capability
  *returns* is a separate per-invocation result conforming to the shape the
  descriptor declared: the descriptor says "a key-value readout with the keys
  System and Body", the result says "Shinrarta Dezhra, Jameson Memorial".
  Collapsing the two would put the LLM tool schema — which must stay
  byte-identical for prompt caching — on a mutating object, race two callers on
  one instance, and stop the parity test from running without invoking
  capabilities.
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
- **Four surfaces: three visual, one spoken.** The **game overlay** (transparent,
  click-through, over a borderless-windowed Elite), the **VR overlay** (SteamVR,
  in the headset), the **panel** (an ordinary window, outside the game), and
  **voice**. Each of the three visual surfaces toggles independently; a system
  tray icon is the way back when all of them are hidden.
- **Render once, display wherever.** One render target at VR-legible density —
  large type, high contrast, low density — presented three ways. Parity between
  the visual surfaces is therefore guaranteed by construction rather than by
  discipline: a capability cannot appear on one and not another, because there
  is only one render.
- **The panel is convenience, not requirement.** It exists because a window with
  a pointer is familiar, and because setup and diagnostics want one. It is not
  privileged: it shows the same layout as the other two and adds pointer
  affordances over the same rows.
- **No surface is read-only.** Anything editable is editable by voice on every
  surface. Games hide and capture the cursor, so the overlays can never take
  pointer input — which makes voice the only universal input and pointer support
  a convenience layered on top. A capability that requires a pointer is broken.
- **Parity is strict, and it is an enumerated test, not a checklist.** Enumerate
  all capabilities against all surfaces and assert a descriptor exists for each
  pairing. Strict, not declared-and-satisfied: letting a capability name the
  subset of surfaces it supports makes VR opt-out, one defensible local reason at
  a time, which is precisely the drift "VR is first-class" was written to
  prevent. Strict costs little here because *Render once, display wherever* does
  most of the work — what the test actually catches is a capability with no
  visual representation at all, and an escape-hatch capability that implemented
  its custom view for some surfaces and not others. A miss is a red build, not a
  missed checkbox.
- **Parity is truly strict: everything the application renders appears on every
  visual surface.** UI chrome is not exempt — the status readout, the live log
  and the cancel affordance appear in the headset and over the game, not only in
  the panel. The objection to this was "a cancel button in VR, with no pointer to
  press it", and it does not hold: *no surface is read-only*, so a button is a
  labelled affordance whose activation path is voice, and showing it in the
  headset is how the Commander learns the word to say. Same argument as help.
  Revisit trigger, recorded deliberately: if an element turns out to be noise in
  the headset rather than discovery, the fix is a per-element exemption argued
  here — not a general retreat to declared parity.
- **What Windows draws is not ours and cannot participate.** The tray icon and
  the title bar are rendered by the operating system. There is no render of ours
  to project onto another surface, so they are outside parity entirely. This is
  not a carve-out from strict; it is the boundary of what strict can apply to.
- **The parity test must assert the enumeration is not empty.** A
  discovery-based test that finds nothing passes, and a test that passes because
  it checked nothing is worse than no test — it reports confidence it never
  earned.
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

## Dependencies

- **Licenses are an allowlist, not a blocklist.** Permitted: MIT, BSD-2-Clause,
  BSD-3-Clause, Apache-2.0, MS-PL, Unlicense, CC0, ISC. Anything else — including
  a package declaring no license at all — stops and needs an exception recorded
  here. A blocklist fails on the license nobody thought to list, and that is
  exactly how GPL codec binaries reached a shipped installer in the predecessor
  project.
- **The check covers what we depend on directly.** A transitive package's
  license is the upstream author's obligation to declare, not ours to audit.
  One exception: a package that ships **native binaries** gets a look at what
  those binaries are, once, when it is added. That is where the predecessor's
  GPL exposure came from — an MIT package three levels above an FFmpeg build
  carrying GPL codecs — and it is a glance, not an audit.
- **Redistribution is why this matters at all.** CI publishes a single-file exe,
  so everything inside it is redistributed. Using a library and shipping it are
  different acts with different obligations, and a single-file publish also
  makes LGPL's relink obligation awkward to satisfy.
- **Standing rejection: FluentAssertions v8**, which moved to a paid commercial
  license under Xceed in early 2025. Shouldly is used instead.
- Enforcement: `Directory.Packages.props` is already in `permissions.ask`, so a
  new package stops for the maintainer. A CI license scan over direct packages
  is agreed and not yet built.

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
- **Work breaks down Epic → Feature → User Story → Task**, using GitHub's issue
  types and sub-issues. Where the boundaries fall is decided by the acceptance
  criteria, not by feel:
  - **Each acceptance criterion is one User Story.** A "criterion" that states a
    constraint rather than a behaviour is not a criterion at all — it is an
    **invariant**, listed separately on the Feature and true of every story
    under it. Invariants must not spawn stories, or the result is a PR with
    nothing to demonstrate.
  - **A Feature yields at least three stories.** Exactly two, and it was a story
    all along: make it one story with two Tasks, one per criterion.
  - **Trouble breaking a Feature into stories means the Feature is a story.**
  - **The happy path is a criterion.** Criteria that name only edge cases and
    constraints leave the feature's whole reason to exist with no story
    attached — which is how the first pass at these issues went wrong.
- **Implementation order is the implementer's call.** Which feature comes next,
  how it splits into stories, and what order those land in is decided by
  whoever is doing the work — from dependencies and from what makes each PR
  verifiable. This is not a carve-out from the working agreement: sequencing is
  not an architectural decision, and architectural decisions still go to the
  maintainer first.
- **Features are decomposed just in time**, one or two ahead of the work. The
  criteria on a Feature carry the intent; writing every story up front produces
  guesses about a codebase that does not exist yet, and they go stale before
  anyone reaches them.
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
- **Stop at the moment of recognition, not after building it.** When hand-rolled
  code starts looking like an argument for a package or a shared abstraction,
  raise it then, with the work unfinished. Finishing first buys a better-evidenced
  proposal and poisons it: a working implementation makes "keep it" the path of
  least resistance, and the decision is half-made by the existence of the thing
  before the maintainer sees it. Bring what the code has to do, the candidate
  package and what it drags in, and an estimate of the hand-rolled size labelled
  as an estimate. If it cannot be estimated without building it, ask for a
  timeboxed spike rather than quietly taking one.
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

- Versioning: an epic bumps **at least** the minor version, a bug fix bumps the
  patch. Whether a given epic is major or minor is decided when the epic is
  defined, not inferred from its size afterwards. The MVP epic completing is
  1.0.0.
- Updater candidate is Velopack, on one condition: update availability must
  surface in the panel and the overlay, with installation deferred to app exit.
  No installer window yanking you out of VR.
