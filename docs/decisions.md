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
- **WPF for the desktop panel and both overlays** — not WinUI 3, not Avalonia.
  Three reasons, in the order they actually bind:
  - **The game overlay is the constraint, not the panel.** It needs per-pixel
    alpha *and* input passing through the transparent regions to the game
    underneath. WPF does this in the box. WinUI 3 architecturally cannot: its
    content renders through DirectComposition, so the window never has access to
    the video memory backing its own content and cannot decide what to pass
    through — an open limitation with no native fix, not a feature awaiting a
    release. Avalonia can reach it through Win32 interop that we would write and
    own, with far fewer worked examples to copy.
  - **UI automation.** WPF's UIA support is the most mature of the three and
    FlaUI drives it. This is intent, not history: nothing exercises it yet. It
    is recorded as a reason for the choice because the tray, the window state
    and the overlays cannot be tested from inside the process, and that testing
    is coming — not because it is already paying off.
  - **Render-to-bitmap for the headset is simple**, and the content is text that
    changes occasionally rather than animation. A CPU-side bitmap handed to
    SteamVR a few times a second costs nothing, so the usual performance
    argument against retained-mode XAML does not apply here.

  Known horizon, accepted deliberately: WPF is in maintenance mode — bug fixes
  and Fluent theming, not new capability — and .NET 10 LTS support runs to
  November 2028. It is also the option with the cheapest exit. Avalonia is a
  XAML-to-XAML migration; WinUI 3 or an immediate-mode stack would be a one-way
  door. Revisit trigger: needing something maintenance mode will not deliver, or
  that 2028 date coming into view.

  Rejected and why, so it is not re-argued: **WinForms** has no vector scaling
  or styling worth having at VR-legible density. **A web stack** (WebView2,
  Photino) styles beautifully but still needs a Win32 host doing the
  click-through work, makes the headset path a capture problem, and adds a
  non-.NET UI layer. **Immediate-mode** (ImGui.NET, Silk.NET) fits the two
  overlays best of anything — you already hold a GPU texture — but has no
  accessibility, primitive text layout, and would make the one surface that
  should feel like an ordinary window feel like a game.
- **SteamVR only.** OpenXR is a someday nice-to-have and appears in no
  definition of done. One `IHeadsetOverlay` interface with a single SteamVR
  implementation keeps the door open without paying for it now.
- **`global.json` pins the SDK *band floor*, not an installed version.**
  `10.0.100` with `rollForward: latestFeature`. Machines carry different feature
  bands — one dev machine was on 10.0.302 (band 3xx) while the cloud container
  had 10.0.110 (band 1xx) — and `rollForward` only moves *up*, so pinning either
  exact version breaks the other machine. Pinning the band floor satisfies both,
  and still refuses every .NET 11 SDK, which is the deliberate-upgrade gate.
  Expect further machines on further bands; that is fine and expected.

## Architecture

- **Capabilities declare, surfaces render.** A capability contributes a
  descriptor: a data object saying what it is and what to show. When complete it
  carries an LLM tool schema, a display model, a group, help text, and example
  utterances — each part arriving with the thing that consumes it. The tool
  schema still lands with the LLM integration; the examples arrived earlier,
  with help's "try saying…" level. Every surface consumes the descriptor, so no
  capability references a UI assembly and none of them contains feature-specific
  rendering code.
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
- **Help is a hierarchy, and a capability's detail is not a fourth string.**
  Groups, then the capabilities in one group, then one capability's help text
  followed by its example utterances. Forty lines spoken aloud is not an answer,
  so the answer stays short at every level. Detail is deliberately *not* a
  longer prose field alongside `HelpText`: a second required description on a
  contract thirty-eight capabilities implement is two things to keep in sync per
  capability, and the drill already earns its keep by adding "try saying…".
  Revisit trigger: a capability whose one line genuinely cannot carry its own
  explanation, at which point the argument is about that capability rather than
  about the contract.
- **Each level of help is asked with a name the level above it produced.** An
  unknown group or capability id throws rather than returning an empty listing,
  because an empty listing reads as "that group is empty" — a different answer,
  and an untrue one. Turning an utterance that resolves to nothing into a name
  help recognizes is a separate job, in front of this one.
- **Help recovers rather than refusing, over the names it already speaks.** An
  unresolved utterance gets the nearest capability id or group name, or — when
  nothing is close — plain acknowledgement followed by the groups. The pool is
  deliberately ids and groups and *not* example utterances: matching those
  against speech-to-text output is the silent-failure mode ruled out two bullets
  down. There is no separate spoken-name field; ids are already short nouns, and
  the argument against a second required string is the same one that kept detail
  out of the descriptor.
- **The name matcher is hand-rolled, internal, and named for its one job.**
  Levenshtein with a tolerance, roughly thirty-five lines, living in `D47.Help`
  as `NearestName`. Two packages were priced first and both were fine on the
  licence allowlist — `Fastenshtein` (MIT, no dependencies, built for `net10.0`)
  and `FuzzySharp` (MIT, no dependencies, newest target `netstandard2.1`) — but
  neither earns a redistributed dependency inside a single-file exe for a
  textbook function that reads in one screen. The real pull toward a package is
  Search machinery
  ([#22](https://github.com/retiring-studios/directive-47/issues/22)), which
  needs the same thing over a far larger and more hostile vocabulary. Revisit
  trigger: that story. Deciding it there means comparing two real call sites
  instead of generalizing from one — which is also why this one is internal and
  not a named abstraction anything else could start conforming to.
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
- **The render lives in `D47.Panel` until a second surface needs it.** The game
  overlay and the VR overlay present this same render, so the shared home will
  not be the panel forever — but naming a rendering project before there are two
  consumers is guessing at a shape, and the panel is the only one that exists.
  When the game overlay arrives the render moves out and `D47.Panel` keeps the
  window. Same bet as `D47.Help`, made for the same reason and expected to be
  called in the same way.
- **The panel is an application, so its types are internal.** CA1515 is right
  about that, and the tests reach them through `InternalsVisibleTo` rather than
  widening the surface to suit a test. WPF classes need `x:ClassModifier` to
  match, which is why the XAML carries it.
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
| 0 · Pure | Parsers, planners, routing, state machines, display models, capability descriptors | CI |
| 1 · Integration, no hardware | Providers against recorded HTTP, journal parsing over fixture logs, WPF layout snapshots | CI |
| 2 · Hardware | Mic, WASAPI, hotkeys, SendInput, SteamVR overlay | Dev PC |
| 3 · Game | Elite Dangerous running — real journal, keybinds actually landing | Dev PC + game |

- **Tiers 2 and 3 contain adapters only, no logic.** If a bug needs the game to
  reproduce, it should live in a file readable in one screen.
- **One `.Tests.csproj` per production `.csproj`.** The production project's
  tier determines the kind of tests in its pair. A project whose tests would
  span two tiers is two projects.
- **Projects split by tier, never by capability.** Capabilities share a project;
  a new one is created only when its tests would land in a different tier, or to
  keep the capability contract free of anything that consumes it. One project per
  capability would mean roughly forty of them, each with a paired test project —
  eighty projects to express a distinction the solution does not have. The
  existing split is exactly these two reasons: `D47.Capabilities` is the contract,
  `D47.Help` is a Tier 0 consumer of it.
- **`D47.Help` is named after its only occupant and that will stop being true.**
  The next Tier 0 capability joins it rather than getting its own project, and
  the project gets renamed at that point. Deliberately not renamed in advance:
  guessing a bucket name before there is anything to generalise from is how
  `D47.Core` gets recreated under a different spelling.
- **Selection via solution filters** (`ci.slnf`, `hardware.slnf`), not
  per-test traits.
- **CI is Windows only, and the tier boundary is hardware rather than operating
  system.** Directive 47 is a Windows product — WPF, SteamVR, and a game that
  runs nowhere else — so a Linux job was enforcing a portability claim nothing
  depends on. One `windows-latest` runner builds every project including the
  `net10.0-windows` ones, which is what lets Tier 1's WPF layout snapshots gate
  a pull request rather than wait for the manual pass. Tier 2 is unchanged and
  still dev-PC-only: no hosted runner has a microphone, a headset, or a game.
  The cost, accepted deliberately: nothing verifies that Tier 0 and Tier 1 still
  build on Linux, so a Claude cloud container is no longer a supported way to
  work on this repo.

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
- Specific analyzer thresholds are deferred until the first Tier 2 adapter,
  because there is nothing to calibrate against until then.
- **The built-in .NET analyzers cannot deliver the size rule above.** There is no
  "method too long" rule in the box, so `AnalysisLevel=latest-all` alone will
  never enforce the first bullet in this section — the gate is aspirational until
  something else provides it. Two candidates, to be decided when the first Tier 2
  adapter lands: SonarAnalyzer.CSharp (S138 method length, S1541 cyclomatic
  complexity, S107 parameter count), or a hand-written architecture test. The
  first is a dependency and therefore stops for the maintainer; the second is
  not.

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

**Cloud provisioning was never a SessionStart hook, and is now unsupported
entirely.** It was a hook briefly. A hook command runs through a shell, and the
shell on the dev PC is PowerShell, which cannot invoke a bash script — so a hook
whose only job was provisioning Linux containers errored at the start of every
local session. Noise that recurs is noise that gets suppressed, and a suppressed
hook is worse than none, so provisioning moved to `scripts/setup-cloud.sh`.

That script is still in version control and still works, but with CI on Windows
nothing verifies that this repo builds on Linux at all. Development happens
locally. The script is kept rather than deleted because deleting it removes a
capability to save nothing.

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
- **Build it when you need it, not before.** A field with no consumer is a guess
  about a consumer. The counter-argument was made and rejected: a required field
  on a widely-implemented contract looks cheap to add now and expensive to add
  at the twelfth implementation, but the twelve-site edit is mechanical and
  certain, while the guessed shape is neither. The descriptor's tool schema and
  example utterances were added on that reasoning and stripped again on this
  one. Example utterances came back one story later, unchanged, when help's
  "try saying…" level turned out to be the consumer — which is the rule working,
  not the rule failing: the second edit was the mechanical one it promised, and
  the shape was confirmed by a consumer rather than guessed at by an author. The
  tool schema is still waiting for the LLM integration.
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
  as an estimate. If it cannot be estimated without building it, ask for a spike
  rather than quietly taking one. Spikes are not timeboxed — they run at the
  maintainer's pleasure and stop when he says so. What matters is that
  exploratory work is asked for, not that it is bounded in advance.
- The dependency half of that line is already enforced: `Directory.Packages.props`
  is in `permissions.ask`, so adding a package stops for the maintainer.
- **Manual test steps are per-PR only**, never an accumulating file. Regression
  is the automated hardware tier's job; the human pass is for new behavior and
  judgment calls. A manual test that keeps recurring is a hole in automation —
  promote it, do not list it.
- CI is to publish a single-file exe on every PR, so manual passes test an
  installed build rather than `dotnet run`. Agreed and not yet built — today CI
  restores, builds and tests only. Until it does, and until there is an app to
  run, the manual pass and the `manual-verification` check below are dormant
  rather than skipped.
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
- **The version is one literal in `Directory.Build.props`, not derived from
  tags.** Deriving it means MinVer or Nerdbank.GitVersioning, and a package
  stops for the maintainer; a number in a file does not. Hand-maintaining it
  costs a deliberate edit per bump, which is the same deliberateness the bullet
  above already asks for — none of "at least a minor", "decided when the epic is
  defined", or "1.0.0 when the MVP lands" is inferable from commit history.
  Started at 0.1.0 with the first feature-complete state, help from the
  capability registry.
- **The pre-release trigger has fired and there is nothing behind it.** Help
  from the capability registry is feature complete and merged, which the table
  above says produces an opt-in pre-release. CI restores, builds and tests; it
  does not publish. Agreed and not yet built.
- Updater candidate is Velopack, on one condition: update availability must
  surface in the panel and the overlay, with installation deferred to app exit.
  No installer window yanking you out of VR.
