# Calendar / Reminder interoperability follow-up

This is the historical 2026-09-06 scope and evidence record. Its old app IDs,
Reminder API13 limitation and local-preparation delivery status are not current
release instructions. See the [current source review](UI_PARITY.md#2026-09-08-source-review)
and [package provenance](../../Packages/README.md) for the subsequent snapshot.

## Scope and decision

The user approved continuing the reviewed development on 2026-09-06. This change
repairs the existing split Presentation integration and Reminder search annotations.
It preserves app identities and platform schemas. DisplayPresentation now targets
the current `Tizen.Action.Presentation` and common View categories on .NET API 14;
both whole-category bindings were regenerated, without source patching.
Canonical version/catalog lifecycle support remains a separate protocol migration;
this adapter must never claim v0.9.1 conformance or native overlay capability.

Two approaches were considered: constrain producers to the renderer's former
32-node, root-only dialect, or accept their bounded legacy bindings in the renderer.
The latter preserves Calendar's existing 0–100 event contract and generated entity
snapshots. Existing array children remain a named repository compatibility input;
`children.explicitList` is accepted without admitting templates or arbitrary styles.

The official [v0.8 protocol](https://a2ui.org/specification/v0.8-a2ui/), inspected
2026-09-06, documents explicit children and path bindings. The repository's split
Template/Document and plain `value` object are compatibility transport conventions,
not the complete official JSONL lifecycle. Visual treatment retains the recorded
[One UI reference](https://design.samsung.com/global/contents/one-ui/) and renderer
tokens; previous/next paging is an adaptation for bounded TV-distance content.

## Architecture and observable behavior

- The legacy binding adapter accepts explicit children and bounded absolute JSON
  pointer paths; the parser retains component, property, ID, tree and size checks.
- Maximum 512 components accommodate Calendar's 501 nodes for 100 events. Each
  Template/Document remains limited to 64 Ki characters; larger output fails
  explicitly. No raw payload styling or executable behavior is accepted.
- The semantic tree preserves IDs, order and groups. Native and HTML presentation
  pages expose at most four text nodes, retaining their ancestor groups. Previous,
  Next, D-pad/keyboard and Back have deterministic focus. Only the current page is
  serialized into its ViewAnnotation, excluding off-page values.
- Reminder search holds draft and applied keywords separately. Typing updates the
  input annotation; Search applies the filter. Published item IDs come from the
  list actually rendered, including the six-card visible limit.
- Domain/use-case code remains host-runnable. Loading the generated managed
  provider on this host failed because `Tizen.Applications.Common` is unavailable;
  no managed-provider invocation or native RPC success is claimed.

## Acceptance and work sequence

- [x] Reproduce producer → parser failures with real Calendar and shared builders.
- [x] Parse 0/1/2/7/100 short events, reminders and page/control snapshots; reject
  malformed children, missing/wrong binding types, unsupported properties, cycles,
  duplicate IDs, oversized payloads and excess nodes.
- [x] Preserve tree content through paging and parser/serializer round trips; keep
  off-page values out of the serialized visible snapshot.
- [x] Host-verify Reminder draft/Apply/clear and page/store identity; native item
  identity still requires the installed UI check below.
- [x] Browser-verify the single DisplayPresentation sample before NUI
  paging implementation, then build all affected apps and rerun their host suites.
- [x] Regenerate bindings into a temporary directory and compare byte-for-byte.
- [x] Prepare and inspect Common Emulator TPKs and target validation commands.
- [ ] Record installed Action/UI verification separately when target installation
  has been explicitly authorized. Never infer native results from host checks.

## Risks and boundaries

The larger node bound requires pagination rather than silently placing text below
the window. Native bounds must be measured after relayout and republished after
paging, movement, resize and resume. Pending UI callbacks must not resurrect a
superseded or paused surface. Existing user changes and generated artifacts are
preserved. No commit, push or target installation is part of local preparation.

## Verification record (2026-09-06)

- Host: Calendar five suites, Reminder two suites, DisplayPresentation three
  suites pass. Provider-named host suites check adapters/contracts; they do not
  load or invoke the Tizen managed providers.
- Contracts: current catalog revision `4355566aa851470dc290c69672ba1f8bdd8470e7`;
  Calendar 16 and DisplayPresentation five manifest providers checked. Generated
  files match fresh generator output; no `HasPrivilegeLocal` patch is applied.
  `git diff --check` passes for authored files; raw actionc output retains its
  trailing whitespace/EOF blank lines and was not manually reformatted.
- Build: all three Release builds pass, zero warnings/errors.
- Package: all three Common Emulator TPKs signed by Tizen CLI; ZIP integrity,
  manifest, signature entries and payload checked. This is not independent
  cryptographic verification or an installation result.
- Browser: actual Calendar 0/1/7/100 and reminder fixtures, 100 pages/400 fields,
  Enter/focus, Previous/Next, Back, malformed/unsupported input, FHD/720p/4K/8K/4:3
  canvas bounds pass in Chromium. See [UI parity](UI_PARITY.md) for browser evidence.
- Target: read-only discovery identifies Common Emulator `emulator-26111`,
  Tizen 10.1 x86_64. Current `Presentation_Show` schema is available; legacy
  `Schedule_SearchReminder` returns no schema. The standalone Reminder app keeps
  API 13/Schedule and needs a compatible catalog or a separately scoped migration
  before its Action acceptance can pass on this target.
- Installed RPC, native visual/input/lifecycle checks, canonical A2UI version and
  catalog support, and native transparency remain unverified/unimplemented as
  applicable. [Target procedure](TARGET_VALIDATION.md) separates these gates.
