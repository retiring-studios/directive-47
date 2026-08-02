# Decisions

Architectural decisions and the reasoning behind them. The reasoning is recorded so it does not get rediscovered.

This file is descriptive. Nothing here enforces itself — see [Enforcement](#enforcement) for where the load-bearing rules actually live.

## Positioning

- **The value proposition is "never leave the cockpit," and 1.0.0 is its
  realization.**
- **The only third-party Elite tools mentioned** are sources we actually draw
  information from — Spansh, EDSM, EDCD/Coriolis — which are named because
  attribution is owed.
- **Personas are Guardian AIs.** Every preset is a flavour of one. `Ward` is the
  shipped default.

## Platform

- **.NET 10, Windows.** Future upgrades are possible.
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
  - **UI automation.** WPF's UIA support is the most mature of the three, and
    the tests drive it through in-box `System.Windows.Automation`. This is
    history rather than intent: the tray, the window state, and the shell's own
    overflow flyout cannot be reached from inside the process, and the panel's
    desktop tests reach all of them from outside. FlaUI was priced twice and not
    taken — the in-box client was enough both times, including for driving
    another process's context menu. Revisit trigger: the first thing the in-box
    client cannot reach.
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
- **A descriptor may have an escape hatch.** The display model covers text,
  lists, and key-value readouts, which is most of what anyone alt-tabs for. A
  capability needing something the model cannot normally express — a map, a
  chart, a plotted route — may supply its own view instead. The cost of using it
  is that the parity test then demands a view for *every* visual surface, not
  just the one that motivated it. Without the hatch, the display model grows a
  `Chart` concept, then a `Map` concept, and becomes a UI framework nobody chose
  to write.
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
  unresolved utterance gets the nearest capability id (an intelligible string, not a guid) or group name, or — when
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
- **Four surfaces: three visual, one spoken.** The **game overlay** (uses a configurable degree of transparency,
  not clickable, over a borderless-windowed Elite), the **VR overlay** (SteamVR,
  in the headset), the **panel** (an ordinary window, outside the game), and
  **voice**. Each of the three visual surfaces toggles independently; a system
  tray icon is the way back when all of them are hidden.
- **Render once, display wherever.** One render target at VR-legible density —
  large type, high contrast, low density — presented three ways. Parity between
  the visual surfaces is therefore guaranteed by construction rather than by
  discipline: a capability cannot appear on one visual surface and not another, because there
  is only one render.
- **The render lives in `D47.Panel` until a second surface needs it.** The game
  overlay and the VR overlay present this same render.
- **The panel is an application, so its types are internal.** CA1515 is right
  about that, and the tests reach them through `InternalsVisibleTo` rather than
  widening the surface to suit a test. WPF classes need `x:ClassModifier` to
  match, which is why the XAML carries it.
- **The panel is convenience, not requirement.** It exists because a window with
  a pointer is both funtional and familiar. It is not
  privileged: it shows the same layout as the other two and adds pointer
  affordances over the same rows.
- **No surface is read-only.** Anything editable is editable by voice on every
  surface. Games hide and capture the cursor or motion controllers, so the overlays can never take
  pointer input — which makes voice the only universal input and pointer support
  a convenience layered on top in the panel. A capability that requires a pointer is broken.
- **Parity is strict, and it is an enumerated test, not a checklist.** Enumerate
  all capabilities against all surfaces and assert a descriptor exists for each
  pairing. Strict, not declared-and-satisfied: letting a capability name the
  subset of surfaces it supports makes VR opt-out, but for Directive 47 VR is "first-class". Strict costs little here because *Render once, display wherever* does
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
  here — not a general retreat to declared parity. Note that both overlays may be turned off if not used. Indeed, even the main window (panel) may be minimized or even closed, but voice control will still work.
- **The tray icon is not one of the surfaces parity is about.** Parity is a rule
  about the three surfaces the application renders — panel, game overlay, VR
  overlay. The tray icon is not a fourth one. Its job is different: it is the way
  back when every visual surface is hidden, and it is the only sign the
  application is running at all. Being different is the point, so it is allowed
  to work differently, and so is anything hanging off it — its context menu, and
  the Exit gesture in that menu, belong to the icon. The window title bar is not part of the "surface" for the
  plainer reason that Windows draws it and there is no render of ours to
  project.

- **A left-click on the tray icon restores the panel, and takes the foreground
  to do it.** The icon exists for two things — exit, and bring the window back —
  so bringing it back has to actually bring it back. There is no case for being
  shy about focus here: if the notification area is on screen at all, the
  Commander has already looked away from the game, and that is their business. "Never leave the cockpit" is simply our tagline, certainly not to be enforced.

- **Closing the panel hides it; exiting is a different gesture.** The panel is
  convenience and the voice loop is the product, so the close control cancels
  the close and hides the window, `ShutdownMode` is `OnExplicitShutdown`, and
  the deliberate way out is Exit on the tray icon's context menu. Two gestures
  rather than one is the point of the third criterion in
  [#68](https://github.com/retiring-studios/directive-47/issues/68) — an exit
  you can reach by reflex is one you will hit while meaning to put the window
  away.
- **The notification-area icon is WinForms' in-box `NotifyIcon`.** WPF has none
  of its own, so the field was that, `Hardcodet.NotifyIcon.Wpf` (MIT, zero
  dependencies, the community standard), or roughly 100–150 lines of
  `Shell_NotifyIcon` P/Invoke with a hidden message window. All three put the
  same icon in the same place through the same shell API; what differs is what
  the *menu* will be made of, and that question belongs to
  [#70](https://github.com/retiring-studios/directive-47/issues/70). Of the
  three only the in-box one adds nothing to a redistributed single-file exe and
  nothing to the licence allowlist, and it costs one `UseWindowsForms` line
  beside `UseWPF`. A WPF application referencing WinForms reads oddly until you
  know why, which is what that line's comment is for. Revisit trigger: #70, if
  WinForms menus turn out to fight the panel's look — swapping is contained,
  because nothing above the icon depends on which of the three created it.

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

- **Tiers 2 and 3 contain adapters only, with only the logic required to
  accomplish interaction with the external system.**
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
- **`D47.Help` is named after its only occupant and that will stop being true if and when another Tier 0 Capability is implemented. Currently none are planned.**
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

- **There is one desktop, so tests that drive it take turns.** xUnit runs test
  classes in parallel, and the automation tests do not own what they are driving
  — there is a single notification area, a single overflow flyout, and windows
  that are found by name. Two such classes running at once produced three
  failures with three unrelated-looking causes: a test closed another test's
  window and then waited forever for its own process to exit, a test asked
  whether the tray icon had gone and was answered by a still-running panel's
  icon, and UI Automation returned a bare `COMException` under the contention.
  Every class that touches the real desktop shares one collection name; the
  in-process tests that build a visual tree and inspect it stay parallel,
  because they touch nothing outside their own thread. The related fix is that
  the harness now finds its window by name **and process id** — matching on name
  alone was a latent bug that only one desktop-driving class had been hiding.

- **Synthesized mouse and keyboard input is Tier 1, not Tier 2.** UI Automation
  has no right-click, and a tray icon's context menu opens on one, so the test
  moves the real pointer and presses the real button. That is not the hardware
  tier: Tier 2 is about devices no hosted runner has — a microphone, a headset,
  a game — and synthesized input needs only a desktop, which the runner has.
  The line is "can a hosted runner do this at all", not "does it touch an input
  API".
- **Integration and end-to-end tests settle on CI once they work.** They take the
  whole desktop for as long as they run, and the maintainer needs that machine
  for other things — so a finished one does not execute locally again. CI sets
  `CI`, has nobody at the keyboard, and gates every pull request. On a
  development machine they skip with the reason in the test output, unless
  `D47_DESKTOP_TESTS=1` opts in.

  **Writing one is the exception, and deliberately so.** A test still being
  shaped is developed locally; pushing to CI for each iteration is slower than
  the problem it avoids. What the gate buys is not a ban but a negotiation — the
  desktop is a shared resource between the maintainer and whoever is running
  tests, and opting in is the moment to say so and to say when it is free again.

  **The line is what a test touches, not how slow it is.** Starting the
  application as a process, or reading what the shell is drawing, puts it behind
  the gate. Building a visual tree in-process and inspecting it does not: it
  touches nothing outside its own thread, nobody using the machine can disturb
  it, and gating it would cost local coverage for nothing. Inheriting
  `DesktopTest` is what applies both the gate and the take-turns collection.

  The immediate cause was a pointer-driven test, and it is worth recording why
  retrying was the wrong answer. The shell's overflow flyout is dismissed by any
  focus change at all — a click, a keystroke, a media key — and an icon in a
  flyout that has just closed still reports the rectangle it used to occupy, so
  the click lands on whatever is behind it and opens *that* window's context
  menu. The failure that found this was Microsoft Edge's tab menu. No amount of
  retrying makes a test that drives the pointer survive somebody using the
  pointer. A bounded retry and an Escape after a miss are still kept, for the
  transient case on CI where the shell's own animations are the only competition.

## Fixtures

- Two journal corpora: sanitized real captures, and synthetic ones built through
  a `JournalBuilder` fluent API.
- **Real journals carry the Commander name and play history.** They are scrubbed
  into `tests/` before anything lands in this public repo. Raw captures are
  gitignored.
- Recorded HTTP responses back the provider tests.

## Quality gates

- **Size limits (maximums) are strictest on adapters, not loosest.** "Thin
  adapter" is an architectural invariant, and the size metric is how it is
  enforced. SonarQube's defaults are the baseline — 100 lines a method (S138),
  cyclomatic complexity 10 (S1541), 7 parameters (S107), 1000 lines a file
  (S104) — and adapters get numbers tighter than those, set once the first Tier
  2 adapter exists to calibrate against. Only cyclomatic complexity gets a
  carve-out, and only per-construct (flat dispatch and mappers), never
  per-directory.
- **Coverage is a floor per tier, never a global percentage.** A global number
  produces tests written for the number: the fastest route to 80% is executing
  code without asserting anything. Per tier the question is answerable, because
  what is reachable differs by tier rather than by taste.

  | Tier | Line | Branch |
  |---|---|---|
  | 0 · Pure | 95% | 90% |
  | 1 · Integration, no hardware | 80% | 70% |
  | 2 · Hardware | N/A | N/A |
  | 3 · Game | N/A | N/A |

  These are floors. Everywhere else in this section a number is a maximum; here
  it is a minimum, and the two read alike on the page.

  Tier 0 is at 100% line and branch today, so 95/90 costs nothing while red,
  green, refactor holds. Deliberately not 100%: the first guard clause that
  cannot be reached would fail the build, and the fix would be a test asserting
  nothing.

  Tiers 2 and 3 are N/A rather than 0%. They do not run in CI, so there is no
  number to enforce — N/A says not measured here, where 0% says measured and
  anything accepted. Their invariant is that adapters hold no logic, and size is
  what enforces that. Only two tiers can ever be gated.

  Two things about the measurement itself. It is a CI measurement only: the
  desktop tests skip on a development machine, so `D47.Panel` reads 9.9% locally
  and far higher in CI. And test assemblies are excluded, or the gate becomes
  the coverage of the tests by themselves.

  Tier 1's 80 is the mainstream number rather than a measured one. Revisit
  trigger: the first CI run that reports it.

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
- CI publishes a single-file exe on every PR, so manual passes test an installed
  build rather than `dotnet run`. Built at
  [#63](https://github.com/retiring-studios/directive-47/issues/63): the
  artifact is attached to every pull request and kept for fourteen days.
- **Definition of done includes a manual verification pass by the maintainer.**
  It verifies the things that cannot be verified via automated testing. GitHub
  does not allow approving your own pull request, so the attestation is not a
  review approval: it is a required `manual-verification` status check
  satisfied by applying the `verified` label, which new commits strip.

  **Not built.** There is no `verified` label, no workflow producing that check,
  and no branch protection or ruleset requiring it — so nothing stops a pull
  request merging without a pass, and several have. Recorded as missing rather
  than described as though it exists, which is the whole point of the
  Enforcement section below.

## Releases

| Channel | Trigger | Audience |
|---|---|---|
| CI artifact | Every PR | Maintainer |
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
- **The per-PR artifact is real; the stable channel is not.** Every pull request
  publishes a self-contained single-file exe and attaches it, and a publish that
  produces nothing fails the run rather than passing with nothing attached.
  Releasing on epic complete is not built.
- **Self-contained, single file, not trimmed.** Self-contained because the
  artifact is what a manual pass installs and runs, and it has to work on a
  machine with no .NET on it. Not trimmed because trimming and WPF's reflection
  over XAML do not get along. The cost is an exe of about 165MB, which is why
  the per-PR artifact is kept for fourteen days rather than the default ninety —
  it exists to be downloaded once and tried, and a release is what keeps a build
  for good.
- **The published exe's version is asserted in CI, not assumed.** The workflow
  reads `Version` from the project, publishes, and then fails if the exe does not
  report it. A publish quietly emitting a default or stale version is the kind
  of thing nobody notices until an installed build claims the wrong number, and
  by then it is in someone's hands.
- Updater candidate is Velopack, on one condition: update availability must
  surface in the panel and the overlay, with installation deferred to app exit.
  No installer window yanking you out of VR.
