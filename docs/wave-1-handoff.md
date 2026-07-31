# Directive 47 — Wave 1 handoff (laptop → desktop)

Paste into a fresh Claude Code session started in the local clone on the desktop.
Delete this file once Wave 1 lands; it is transient, not a record.

Read `CLAUDE.md` and `docs/decisions.md` first — both are in the repo and are the
source of truth. This brief does not restate them.

---

## Repo state

`main` is at `a480021`, clean, nothing unpushed. There is still **no solution, no
projects, and no CI**. Wave 1 creates them.

`CLAUDE.md` mentions `cloud.slnf` and `hardware.slnf`; they do not exist yet.

## Already done — do not redo

**The `permissions.ask` globs in `.claude/settings.json` are verified.** This was
the first task of Wave 1 and it is finished. Three probes were run — a Write to a
nested `src/.../X.csproj`, a Write to a root-level `Directory.Build.props`, and an
Edit to the nested `.csproj` — and the maintainer confirmed a prompt appeared for
all three. That includes the case most likely to have silently failed: `**/`
matching zero directories for the root-level `Directory.Build.props` rule. It
matches. Probe files were deleted; the tree is clean.

**Gap found:** the globs cover `*.csproj`, `*.sln`, `*.slnf`,
`Directory.Build.props`, `Directory.Packages.props`. They do **not** cover
`.editorconfig`, `global.json`, `.gitattributes`, or `.github/workflows/*.yml`,
all of which Wave 1 needs and all of which are load-bearing. Whether to add them
is still open — see below.

## Decisions locked in this session

| Decision | Outcome |
|---|---|
| First project | `D47.Capabilities` — the capability descriptor contract. Tier 0, no dependencies, Linux-buildable, and the abstraction the whole architecture hangs off. Paired with `D47.Capabilities.Tests`. |
| Naming | `D47.Core` explicitly rejected — junk-drawer names attract junk. |
| Target framework | `net10.0`, set **per-project, never in `Directory.Build.props`**. A global `net10.0-windows` would break cloud CI, and `UseWPF` needs the Windows-only Desktop SDK. Tier 0/1 → `net10.0` → `cloud.slnf`. WPF/overlay/hardware → `net10.0-windows` → `hardware.slnf`. |
| Test framework | xUnit **v3**. Note the cloud verification in the previous handoff used `dotnet new xunit`, which scaffolds v2 — v3 is a different template and different packages, so that verification does not transfer and needs redoing. |
| Assertions | **Shouldly.** (FluentAssertions v8 went to a paid commercial license under Xceed in early 2025 — avoided deliberately.) |
| Analyzers | `AnalysisLevel=latest-all` kept, with per-rule carve-outs in `.editorconfig` as they appear, so each suppression is a deliberate decision rather than a blanket downgrade. |
| CI | GitHub Actions, `ubuntu-latest`, over `cloud.slnf`. Ubuntu on purpose: it *enforces* the platform-neutral claim for Tier 0/1 instead of trusting it. Windows CI arrives with `hardware.slnf` on a self-hosted runner, Wave 2+. Does **not** call `scripts/setup-cloud.sh` — that is for Claude cloud containers; `actions/setup-dotnet` is the runner equivalent. |
| SDK pinning | `global.json` pinned to `10.0.100` with `rollForward: latestFeature`. See below — the exact number matters. |

### `Directory.Build.props` — agreed contents

- `Nullable=enable`
- `TreatWarningsAsErrors=true`
- `EnforceCodeStyleInBuild=true`
- `AnalysisLevel=latest-all`
- `ManagePackageVersionsCentrally=true` (plus a `Directory.Packages.props`)
- `GenerateDocumentationFile=true` — **not cosmetic.** `IDE0005` (unnecessary
  usings) does not fire during build without it. Long-standing Roslyn quirk.
- **No `TargetFramework`.** See the table above.
- **No thresholds.** Deferred by agreement. Do not invent numbers.

## Still open — needs the maintainer

1. **The `.editorconfig` proposal below has not been reviewed yet.** It was
   written but the maintainer had not read it before the machine transfer.
2. **Whether `.editorconfig`, `global.json`, `.gitattributes`, and
   `.github/workflows/*.yml` go behind `permissions.ask`.** `global.json` in
   particular is the only thing standing between the project and an unplanned
   .NET 11 jump.
3. **Commit signing.** Wave 0's commits are verified on GitHub only because the
   GitHub App pushed them. No signing is configured in git locally, so local
   commits land unverified on both machines.
4. **Which analyzer package enforces the deferred size/complexity thresholds.**
   The built-in .NET analyzers have no "method too long" rule — `latest-all`
   alone will never deliver the Quality gates section of `docs/decisions.md`.
   Candidates: SonarAnalyzer.CSharp (S138 method length, S1541 complexity, S107
   parameter count), or a hand-written architecture test. Decide when the first
   Tier 2 adapter lands, not before — there is nothing to calibrate against
   until then.

## Environment facts, measured

- **The dev PC runs PowerShell, always.** Nothing may depend on bash locally.
  `scripts/setup-cloud.sh` is bash and that is fine — it only runs in Linux cloud
  containers.
- **There is no SessionStart hook, deliberately.** Removed in `a480021`; the
  reasoning is in `docs/decisions.md`. Do not reintroduce it.
- **SDK feature bands differ per machine, and this is why `global.json` pins
  `10.0.100` rather than an installed version.** The laptop had **10.0.302**
  (band 3xx). The cloud container has **10.0.110** (band 1xx). Pinning either
  exact version breaks the other, because `rollForward` only moves up. Pinning
  the band *floor* with `latestFeature` satisfies both and **still refuses every
  .NET 11 SDK** — which is the deliberate-upgrade escape hatch the maintainer
  asked for. The desktop will likely be a third band; that is fine and expected.
- The desktop needs the SDK installed: `winget install Microsoft.DotNet.SDK.10`.
  Confirm `dotnet --version` reports 10.x before building.
- `core.autocrlf=true` is set system-wide in Git for Windows on the laptop. The
  desktop probably matches, but a `.gitattributes` with `* text=auto` makes it
  moot and is proposed alongside `end_of_line = crlf`.
- `permissions.ask` surfaces as a real prompt locally, versus a hard block in a
  cloud session. That is why Wave 1 is being done locally.
- Cloud container: Ubuntu 24.04.4, 4 cores, 15 GB RAM, ~30 GB free, no SDK until
  `scripts/setup-cloud.sh` runs. `builds.dotnet.microsoft.com` is blocked by
  network policy, so `dotnet-install.sh` does not work; apt is the path.

## New capability on the desktop

The desktop has VR. It is therefore the **Tier 2 and Tier 3 machine** — mic,
WASAPI, hotkeys, SendInput, SteamVR overlay, and Elite Dangerous itself. The
laptop could never have run those tiers. Nothing in Wave 1 needs them, but
`hardware.slnf` stops being hypothetical from here on.

## Carried forward from Wave 0

1. **Configure the cloud environment's setup step** to run
   `scripts/setup-cloud.sh`. Until then, cloud sessions have no .NET SDK. Not
   blocking for local work. The maintainer asked to be walked through this.
2. **The `Surfaces` issue field is invisible through the GitHub MCP layer** — it
   models only text, number, date, and single-select, so multi-select fields are
   dropped from listings and cannot be set. `Tier` and `Verified` work fine. Set
   `Surfaces` by hand in the GitHub UI. Mirror labels are a Wave 2 option.
3. **Wave 2, not now:** labels, issue forms, PR template, rulesets, first epic
   decomposed into stories, and the `manual-verification` status check satisfied
   by a `verified` label (GitHub does not permit approving your own PR).

## Working agreement

Architectural decisions go to the maintainer first: propose options and
tradeoffs, then stop. Enforced by `permissions.ask`, not merely written down.

Acceptance criteria are written before code and phrased so they become test
names. One story, one branch, one PR (`Closes #N`). Review starts at the tests.

## First actions on the desktop

1. `winget install Microsoft.DotNet.SDK.10`, then confirm `dotnet --version`.
2. Get a verdict on the `.editorconfig` below and on open items 2 and 3.
3. Create the skeleton — `global.json`, `.gitattributes`, `.editorconfig`,
   `Directory.Build.props`, `Directory.Packages.props`, `D47.sln`,
   `D47.Capabilities`, `D47.Capabilities.Tests`, `cloud.slnf`, `hardware.slnf`.
4. Green build and green test locally **before** touching CI.
5. Then the workflow.

---

## Appendix — proposed `.editorconfig`, awaiting review

Severities are `warning`; `TreatWarningsAsErrors` promotes them. Keeping that
separation means this file states what matters and the props file states how
fatal warnings are — if `TreatWarningsAsErrors` is ever scoped down, this file
does not quietly become decorative.

The picks most likely to draw objection: the `var` policy (explicit unless the
type is already visible on the line); primary constructors at `suggestion` rather
than `warning`, because forcing them changes how every class reads and they are
too new to mandate; expression-bodied constructors banned; and `crlf`.

**The test carve-out at the bottom is load-bearing, not housekeeping.** The
process says acceptance criteria are phrased so they become test names, which
produces names like `Descriptor_WithNoVoicePhrasings_IsRejected`. Under
`latest-all`, CA1707 flags every underscore in an identifier, and
`TreatWarningsAsErrors` turns that into a failed build. Without those lines the
stated convention cannot compile.

```ini
root = true

[*]
charset = utf-8
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 2

[*.{yml,yaml,json,md}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.{csproj,props,targets,slnf}]
indent_size = 2

[*.cs]
indent_size = 4
end_of_line = crlf

# Usings
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
csharp_using_directive_placement = outside_namespace:warning
dotnet_diagnostic.IDE0005.severity = warning

# Namespaces and layout
csharp_style_namespace_declarations = file_scoped:warning
csharp_prefer_braces = true:warning
csharp_new_line_before_open_brace = all

# No redundant qualification
dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning
dotnet_style_qualification_for_method = false:warning
dotnet_style_qualification_for_event = false:warning
dotnet_style_predefined_type_for_locals_parameters_members = true:warning
dotnet_style_predefined_type_for_member_access = true:warning

# var: explicit for built-ins, var only when the type is already on the line
csharp_style_var_for_built_in_types = false:warning
csharp_style_var_when_type_is_apparent = true:warning
csharp_style_var_elsewhere = false:warning

# Immutability
dotnet_style_readonly_field = true:warning
csharp_prefer_static_local_function = true:warning
csharp_style_prefer_readonly_struct = true:warning

# Modern C#
csharp_style_pattern_matching_over_is_with_cast_check = true:warning
csharp_style_pattern_matching_over_as_with_null_check = true:warning
csharp_style_prefer_switch_expression = true:warning
csharp_style_prefer_pattern_matching = true:warning
csharp_style_null_propagation = true:warning
dotnet_style_coalesce_expression = true:warning
dotnet_style_object_initializer = true:warning
dotnet_style_collection_initializer = true:warning
csharp_style_prefer_primary_constructors = true:suggestion

# Expression bodies
csharp_style_expression_bodied_properties = true:warning
csharp_style_expression_bodied_accessors = true:warning
csharp_style_expression_bodied_lambdas = true:warning
csharp_style_expression_bodied_methods = when_on_single_line:silent
csharp_style_expression_bodied_constructors = false:warning

# Unused code
dotnet_code_quality_unused_parameters = all:warning
csharp_style_unused_value_expression_statement_preference = discard_variable:silent

# Naming
dotnet_naming_style.pascal.capitalization = pascal_case
dotnet_naming_style.camel.capitalization = camel_case
dotnet_naming_style.i_prefixed.capitalization = pascal_case
dotnet_naming_style.i_prefixed.required_prefix = I
dotnet_naming_style.underscored.capitalization = camel_case
dotnet_naming_style.underscored.required_prefix = _

dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_rule.interfaces_are_i_prefixed.symbols = interfaces
dotnet_naming_rule.interfaces_are_i_prefixed.style = i_prefixed
dotnet_naming_rule.interfaces_are_i_prefixed.severity = warning

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private, internal
dotnet_naming_rule.private_fields_are_underscored.symbols = private_fields
dotnet_naming_rule.private_fields_are_underscored.style = underscored
dotnet_naming_rule.private_fields_are_underscored.severity = warning

dotnet_naming_symbols.members.applicable_kinds = class, struct, enum, property, method, event, namespace
dotnet_naming_rule.members_are_pascal.symbols = members
dotnet_naming_rule.members_are_pascal.style = pascal
dotnet_naming_rule.members_are_pascal.severity = warning

dotnet_naming_symbols.locals.applicable_kinds = parameter, local
dotnet_naming_rule.locals_are_camel.symbols = locals
dotnet_naming_rule.locals_are_camel.style = camel
dotnet_naming_rule.locals_are_camel.severity = warning

# Test projects
[tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none
dotnet_naming_rule.members_are_pascal.severity = none
dotnet_diagnostic.CA1034.severity = none
dotnet_diagnostic.CA2007.severity = none
dotnet_diagnostic.CA1861.severity = none
```

Unverified in the above: per-section `dotnet_naming_rule.*.severity` overrides
are fiddly in Roslyn. Confirm the test carve-out actually takes effect when the
first test file lands, rather than assuming it.
