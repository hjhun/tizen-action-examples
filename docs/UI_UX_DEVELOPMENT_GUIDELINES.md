# Tizen Action Example Apps UI/UX Development Guide

## Purpose

This guide turns the product qualities demonstrated by Calendar and Reminder into
reusable requirements for every application in this repository. It does not
prescribe one shared screen layout: Calendar is a time-and-detail workspace and
Reminder is a focused smart-list workspace. Each app keeps its domain-specific
information architecture while meeting the same interaction, state, Agent, and
evidence contract.

Use this document with:

- `AGENTS.md` for repository architecture and verification rules;
- `docs/ONE_UI_PRODUCT_UI_POLICY.md` for the One UI/reference-source policy;
- `.agents/workflows/NUI_SCALING_AND_UI_EVIDENCE.md` for native evidence;
- `.agents/workflows/actionc-generation.md` for generated binding workflow.

## 1. Start from a product task, not a widget inventory

Before implementation, document the primary user task, the current Samsung
reference app or One UI system surface, the reference screen/version/source/date,
and the intentional Tizen adaptation. State whether an observed interaction is
source-verified or an adaptation for a large-screen remote environment.

Design a complete flow instead of a collection of commands:

```text
Discover or create → inspect current state → change it → observe the result
```

A user and an Agent must be able to identify the current item, take a bounded
next action, and observe the state transition. Do not add fabricated toolbar
controls, fake dashboards, remote images, account data, or commands with no
product behavior merely to make a screen appear complete.

## 2. Information hierarchy and screen composition

### Content first

The primary domain content occupies the strongest visual region. Navigation and
commands provide context without competing with the content.

- Calendar uses a command bar, period projection, event grid/list, and selected
  detail pane.
- Reminder uses an explicit smart-list context, bounded result workspace, and
  focused detail/editor route.
- Browser uses actual web content with separated address and navigation roles.

Keep a page's hierarchy stable across loading, empty, error, detail, editor, and
confirmation states. Preserve enough context that recovery actions make sense.

### Bounded, truthful content

Render only data that belongs to the current app state. Bound item counts,
string lengths, previews, and result sets. Empty, unavailable, validation,
loading, error, and destructive-confirmation states are first-class product
states, not afterthoughts.

An emulator-only simulation must be visibly and semantically identified as a
simulation. Do not present it as target hardware capability evidence.

## 3. Input, focus, and Back behavior

Every actionable surface needs a deterministic contract for D-pad, keyboard,
pointer, and touch where the target supports them.

1. Define initial focus for every screen and overlay.
2. Define Left/Right and Up/Down order only among currently visible, enabled
   controls; focus must never enter hidden content.
3. Make Enter and pointer/touch activate the same semantic command/reducer.
4. Give focus two cues where practical: high-contrast outline plus a non-color
   change such as surface, scale, elevation, or rail.
5. A modal traps focus. Its initial action is deliberate, destructive actions
   require confirmation, and Back cancels/restores focus to the invoking control.
6. Closing detail/editor/search returns focus by stable Entity ID, not a list
   array index.
7. Back hierarchy is explicit: dismiss modal → leave transient/secondary surface
   → restore the previous product state → apply domain-specific history only when
   valid.

Do not claim semantic accessibility validation when the target accessibility tree
is unavailable. Record that limitation and use screenshot plus visible input
postconditions instead.

## 4. Reference-canvas geometry and NUI scaling

Use the live drawable area from `Window.Default.WindowSize` and
`Window.Default.GetInsets()`. New or migrated NUI surfaces use a centered,
uniform 1920×1080 reference canvas:

```text
availableWidth  = windowWidth  - insetStart - insetEnd
availableHeight = windowHeight - insetTop   - insetBottom
scale           = min(availableWidth / 1920, availableHeight / 1080)
offsetX         = insetStart + (availableWidth  - 1920 * scale) / 2
offsetY         = insetTop   + (availableHeight - 1080 * scale) / 2
```

- The physical root fills the complete window and owns letterbox/pillarbox
  background.
- One centered ancestor applies scale exactly once to app content, typography,
  spacing, focus outlines, overlays, and radii.
- Invalid, non-positive, or transiently exhausted geometry must retain the last
  valid render rather than replacing it with broken bounds.
- Resize and inset changes re-render the same logical UI state.
- ViewAnnotation bounds are measured from actual transformed NUI views; do not
  infer them from design coordinates.

Host geometry tests cover multiple aspect ratios, but only an installed target
screenshot proves a native layout mode.

## 5. State ownership and Agent-facing UI context

UI, Action providers, and View providers use the same application-owned
repository/use-case instances. The UI does not call its own RPC API.

For each meaningful visible surface:

- use stable Entity IDs;
- publish only current, visible, non-sensitive state;
- use generated Entity `ToJson()` for `Annotation.EntityInfo`;
- report real finite positive `ScreenBounds` and `WindowBounds` when available;
- synchronize ViewAnnotation on render, focus, selection, pause/resume, and
  removal;
- provide `GetAnnotatedViews`, `GetFocusedView`, `FindById`, and
  `View_ToPresentation` consistently when those platform Actions are exposed.

Actions must have at least one positive and one bounded-negative test. After a
mutation, prove the postcondition through a resolver/search Action or an
observable UI state—not only through an acknowledgement response.

## 6. Screens and visual evidence

Before NUI coding, keep one executable `refs/one-ui-sample.html` that represents
the actual product flow. It is a preview contract, not a disconnected mockup.
Maintain each app's `docs/UI_PARITY.md` with a state-by-state comparison between
HTML and installed NUI evidence.

For every material UI slice, capture and review:

- default/home or current workspace;
- primary content/result state;
- detail or editor state;
- loading, empty, validation, and error/recovery states as applicable;
- focused control state;
- destructive confirmation and focus restoration;
- relevant D-pad, pointer, and touch postconditions.

Use real native screenshots and record target profile, resolution, input method,
fixture provenance, and accessibility-tree limitations. Public Common Emulator
coverage must never be labeled TV/product-target certification.

## 7. Implementation checklist

Before declaring an app UI/UX complete:

- [ ] Product flow and Samsung/One UI reference adaptation are documented.
- [ ] Screen hierarchy is content-first and every visible command has behavior.
- [ ] Loading, empty, validation, error, unavailable, and destructive states are
      designed where applicable.
- [ ] D-pad, keyboard, pointer, touch, initial focus, modal trap, Back, and
      stable focus restoration are specified and tested.
- [ ] Live window/inset scaling is applied exactly once and invalid geometry is
      safe.
- [ ] UI, Actions, and ViewAnnotation share one state source; annotations expose
      only current visible Entity context.
- [ ] HTML contract, installed NUI screenshots, and parity ledger are current.
- [ ] Host tests, build, package/install, Action E2E, and native UI evidence are
      reported as separate gates.

## 8. Build and generation entry points

Each tracked app provides `build.sh`:

```sh
./Browser/build.sh build
./Calendar/build.sh generate
./Reminder/build.sh all
```

- `build` is the safe default: it builds without changing generated source.
- `generate` regenerates full Action categories with `actionc` without editing
  generated output.
- `all` runs generation followed by a Release build.

Run every tracked app in a stable order from the repository root:

```sh
./build-all.sh build
```

`./build-all.sh generate` and `./build-all.sh all` intentionally modify generated
bindings; inspect the diff and run the appropriate target E2E checks before
committing. Music and Video remain untracked workspaces and are intentionally
not included until adopted as repository projects.
