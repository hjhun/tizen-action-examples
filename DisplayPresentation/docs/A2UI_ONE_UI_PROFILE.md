# Google A2UI Compatibility and Samsung One UI Rendering Profile v0.1

> 이 문서는 Google A2UI protocol compatibility와 Samsung One UI-adapted Tizen NUI rendering을 분리한다. Presentation payload는 공식 A2UI semantics와 data를 제공하며 visual skin이나 executable content를 제공하지 않는다.

## Sources, versions, and adaptation

- **Samsung Developer, “One UI”** (retrieved 2026-08-09): One UI’s large-screen guidance emphasizes a calm, content-first hierarchy, clear grouping, comfortable reach/focus, and adaptable layouts rather than copied Galaxy assets. Source: <https://developer.samsung.com/one-ui>.
- **Canonical A2UI repository**, revision [`ec97cb0d7499932e67003ffe5b709a3db7e7033a`](https://github.com/a2ui-project/a2ui/tree/ec97cb0d7499932e67003ffe5b709a3db7e7033a), committed 2026-08-07 and retrieved 2026-08-09: <https://github.com/a2ui-project/a2ui>. The former `google/A2UI` repository redirects here.
- **Version status at that revision:** [v0.9.1](https://github.com/a2ui-project/a2ui/blob/ec97cb0d7499932e67003ffe5b709a3db7e7033a/specification/v0_9_1/docs/a2ui_protocol.md) is **Current Production**; [v1.0](https://github.com/a2ui-project/a2ui/tree/ec97cb0d7499932e67003ffe5b709a3db7e7033a/specification/v1_0) is **Candidate**, not stable. v0.9.1 defines `createSurface`, `updateComponents`, `updateDataModel`, `deleteSurface`, `catalogId`, and client `action`. v1.0 candidate adds/changes contracts including action IDs/`actionResponse` and `surfaceProperties`.
- **Tizen NUI API10**, public [`Tizen.NET.API10` 10.0.0.17508 package](https://www.nuget.org/packages/Tizen.NET.API10/10.0.0.17508) XML documentation inspected 2026-08-09: [`NUIApplication(string, WindowMode)` / `WindowMode.Transparent`](https://github.com/Samsung/TizenFX/blob/3cc2ad9a6409ada349c243232c9d16c0d1d02e60/src/Tizen.NUI/src/public/Application/NUIApplication.cs), [`Window.SetTransparency`, `SetOpaqueState`, `SetInputRegion`, `SetAcceptFocus`](https://github.com/Samsung/TizenFX/blob/3cc2ad9a6409ada349c243232c9d16c0d1d02e60/src/Tizen.NUI/src/public/Window/Window.cs), and [`Color.Transparent`](https://github.com/Samsung/TizenFX/blob/3cc2ad9a6409ada349c243232c9d16c0d1d02e60/src/Tizen.NUI/src/public/Common/Color.cs). The former <https://docs.tizen.org/application/dotnet/api/TizenFX/API10/api/Tizen.NUI.html> index currently redirects to the current Tizen docs portal, so the package XML plus source permalinks are the recorded API10 evidence. The API surface is documented; target compositor behavior is not yet verified.

This renderer is an **Inspect / Operate** surface, not a landing page or dashboard. The adaptation uses a full-window neutral canvas, a compact context heading, readable content grouping, conservative rounded containers only when a semantic section needs containment, and a high-visibility focus ring. The protected `Music/refs/music-design.html` exemplar additionally validates the reusable TV translation method: centered 1920×1080 reference-canvas scaling, TV-distance type/spacing, strong content-first hierarchy, compact contextual chrome, persistent task context where justified, two-cue focus, and deterministic multi-input transitions. This profile does not copy its Music name/logo, rose/font/media tokens, playback/library controls, domain UI, gradients, or exact geometry, and does not copy Samsung proprietary assets.

Google A2UI가 versioned wire contract, surface lifecycle, catalog, semantic component, data binding과 client action을 소유한다. Samsung One UI adaptation은 그 의미를 Tizen NUI component, typography, spacing, shape, color, focus와 input으로 표현하는 renderer 책임이다. Renderer support matrix는 Google A2UI catalog를 재정의하지 않는다.

## Implemented scope (2026-09-06)

The current app builds against .NET API 14 and exposes
`Tv_Tizen.Action.Presentation_Show` plus the four common View actions. Only the
legacy split transport and Column/Text semantics below are implemented. The
repository's plain `dataModelUpdate.value` object, optional Text `role` and direct
array children are compatibility conventions, not claims of complete official
v0.8 JSONL conformance. Canonical version/catalog negotiation, client actions,
Button/TextField payload components, loading composition, and transparent window
policy remain future work. Renderer-owned pagination controls are not payload
Button support.

JSON pointer bindings accept up to eight segments and 1,024 characters with `~0`
and `~1` escapes and canonical nonnegative array indices. Each JSON string is
bounded to 65,536 UTF-16 code units. Calendar's 100 short events fit 501 nodes;
longer aggregate output fails the transport bound explicitly. Four text fields
are shown per page; Previous/Next and Dismiss are renderer-owned controls.
Initial focus prefers enabled Next, then Previous, then Dismiss. Back/dismiss
clears the presentation to a neutral empty state and never restores old data.

The browser and host checks in [the follow-up record](2026-09-06-interop-followup.md)
passed. Native compositing, input, focus restoration and annotation measurements
remain target gates. The broader matrices below are design requirements where
implementation is explicitly marked pending, not proof of delivered features.

## C0 host envelope recognition (2026-09-08)

The independent [CanonicalA2UiMessageReader](../src/DisplayPresentation.UseCases/CanonicalA2UiMessageReader.cs)
recognizes the outer shape of one already framed JSON message for the selected
`v0.9.1` profile. Other string versions, including `v0.9`, yield
`UnsupportedVersion`, without judging their protocol validity; missing/wrong-type
versions and malformed input yield `InvalidEnvelope`. Failures return no accepted
envelope. Four lifecycle kinds and their catalog-independent required/allowed
body fields are checked, but no lifecycle state or catalog semantics are applied.

The pinned [official protocol](https://github.com/a2ui-project/a2ui/blob/8ff4651232ab0e02b0123730b502711170637a3a/specification/v0_9_1/docs/a2ui_protocol.md)
was inspected on 2026-09-08 at revision `8ff4651232ab0e02b0123730b502711170637a3a`
(committed 2026-09-04). Its four create/update-components/update-data/delete JSON
examples are preserved verbatim in [CanonicalEnvelopeTests](../tests/DisplayPresentation.UseCases.Tests/CanonicalEnvelopeTests.cs),
with source hash and provenance. The pinned schema allows both `v0.9` and `v0.9.1`;
the basic catalog declares a `v0_9` catalog ID while the protocol's `v0.9.1`
examples use a `v0_9_1` ID. C0 preserves literal IDs, without trimming or aliasing;
catalog admission policy remains unresolved.

64 KiB UTF8 input, depth 32, duplicate-key rejection and malformed-Unicode rejection
are **local input policies**, not universal A2UI restrictions. String/property-name
escape validation includes opaque component/theme/data JSON. Valid surrogate pairs
and literal values survive. The returned body owns a clone safe after document
disposal and caller-buffer changes; absent `value`/`path` stay absent, and explicit
null/primitive/object/array values are preserved without defaults.

Initial RED was compilation failure for missing reader/types. A later real Unicode
behavior RED found eight escaping exceptions and twelve unsafe recognitions;
focused correction limits `InvalidOperationException` handling to `GetString` on
known string/property tokens. The 169 host checks (including 22 Unicode checks),
the existing UseCases suite with 54 producer checks, and Release compilation pass
(zero warnings/errors). These are host/compile results only. C0 is not connected
to providers, manifest, Show, the legacy parser/serializer or NUI. Full schema,
catalog/lifecycle validation, transport and canonical native rendering remain open.
Earlier legacy/native evidence retains its recorded scope in [UI_PARITY.md](UI_PARITY.md).

## C1 host catalog admission and surface registry (2026-09-08)

This records C1 at acceptance; its data/component rejections are extended by the C2/C3 subsets below.

The independent [CanonicalSurfaceRegistry](../src/DisplayPresentation.UseCases/CanonicalSurfaceRegistry.cs)
adds a local admission policy after C0: only the exact pre-registered literal
`https://a2ui.org/specification/v0_9/catalogs/basic/catalog.json` is identified.
Wire `v0.9.1` and this catalog ID are independent values; no alias, normalization,
URL fetch or external/inline catalog registration occurs. The definition hash
recorded in source is provenance at the C0 pin above, not proof supplied by an
incoming ID or evidence of full catalog validation.

`Created`/`Deleted` mean registry application only. C0 `InvalidEnvelope` and
`UnsupportedVersion` precede create catalog admission (`UnsupportedCatalog`), then
locked existence/cap checks and mutation. Duplicate create yields `DuplicateSurface`;
missing delete/update yields `MissingSurface`; updates to an existing surface yield
`UnsupportedOperation`, never successful no-ops. Version/catalog stay fixed until
delete/recreate, which admits a new record. Theme/sendDataModel are preserved only.

The **local cap is 16** active surfaces per session instance. Failed applications
preserve the full ID/version/catalog/body snapshot. Atomic admission prevents
same-ID and capacity races; snapshots use immutable records, cloned JSON and a
detached read-only collection. `Clear` affects only its owning registry instance.

[CanonicalSurfaceRegistryTests](../tests/DisplayPresentation.UseCases.Tests/CanonicalSurfaceRegistryTests.cs)
pass 75 host checks, including actual registry races, snapshot isolation and C0
parse/Unicode rejection without state changes. Initial RED was missing-type
compilation failure, not a reproduced behavior failure. The full UseCases suite
(including C0 169 and producer 54 checks) and Release build pass with zero warnings/errors.
The unchanged official protocol create yields `UnsupportedCatalog`; the official
basic v0.9 fixture yields `UnsupportedVersion`. Positive creates are explicitly
local schema-derived fixtures. Official delete succeeds after explicit local
setup; this is not an official end-to-end pairing, and an official create success
pairing remains unavailable under this admission policy.

C1 is not complete canonical lifecycle support. Catalog/theme/component/data
validation and application, transport, provider and native rendering remain
unconnected; no renderer capability is advertised. Existing legacy behavior and
its separately scoped native evidence in [UI_PARITY.md](UI_PARITY.md) are unchanged.

## C2 host data-update subset (2026-09-08)

This records C2 at acceptance; the later C3 component-state subset is independent of Data.

[CanonicalDataModelUpdater](../src/DisplayPresentation.UseCases/CanonicalDataModelUpdater.cs)
now applies `updateDataModel` to registry Data independently of the create Body.
Uninitialized `JsonElement?` and explicit JSON null remain distinct. Omitted path
or `/` replaces the whole root; existing object ancestors allow leaf upsert/delete.
Values retain JSON types and number lexemes; replacement does not merge objects.
Numeric/`01`/`-` object keys are literal, `/parent/` addresses an empty object key,
and `~1`/`~0` are decoded once without percent decoding or trimming.

Array traversal, inferred parent creation, root deletion and empty path remain
`UnsupportedOperation`, not declarations of invalid canonical input. Array deletion
is not approximated by shifting elements or assigning null in place of undefined.
Missing object-leaf deletion yields `MissingPath` as a **local policy**.
After C0 and surface-existence checks, precedence is path UTF8 bytes → token count
→ full pointer syntax → supported operation/ancestors → candidate depth → encoded
bytes. Relative/fragment paths and malformed `~` escapes yield `InvalidPath`;
local limit failures yield `StateLimitExceeded`, including an overlong malformed path.

Local limits are **1024 UTF8 path bytes / 32 tokens** and cumulative Data
**64 KiB compact UTF8 / depth 32**, separate from C0 message bounds. Stored byte
measurement uses `JavaScriptEncoder.Default`; escaping expansion counts. A bounded
serialization sink rejects overflow without first constructing a full output string.
The 16-surface cap bounds retained Data payload to 1 MiB, not total RAM: snapshots,
create bodies and transient candidates are additional allocations.

Cloned candidate validation and record swap occur under the C1 session lock.
Failure preserves complete identity/Body/Data state; snapshots and session/surface
ownership stay isolated. Concurrent separate-key updates lose no updates and
same-key results reflect an atomic winner. Delete/recreate races follow lock order;
there is no wire-generation protection against an update reaching a recreated ID.
`DataUpdated` means registry application only.

The unchanged pinned protocol whole-model, field-update and field-delete messages
pass with explicitly **local** create/parent/leaf setup; this is not an official
end-to-end pairing. Tests pass **174 C2 host checks**, including exact cumulative
byte/depth boundaries, rejection recovery, ownership and concurrency; C1 75, C0 169,
producer 54 and the full UseCases suite also pass. Release has zero warnings/errors.
Initial RED was missing-API compilation failure. A fixture-extraction preparation
error and its initial compile log are preserved separately, not behavior RED.
Provider/native integration remains absent. Full canonical/catalog/component,
binding/theme/actions/transport/renderer support and earlier legacy evidence are
unchanged by this host subset.

## C3 host literal component-state subset (2026-09-08)

[CanonicalComponentStateUpdater](../src/DisplayPresentation.UseCases/CanonicalComponentStateUpdater.cs)
admits new IDs and structurally identical repeats as `ComponentsUpdated`, a registry
result only. Object property order is ignored; optional-field presence, strings and
child-array order/duplicates are preserved. Changed existing IDs yield
`UnsupportedComponentUpdate`; replacement/merge semantics are not implemented.
Literal Text strings with optional catalog `variant`, and Column child-ID arrays
with optional catalog `justify`/`align`, are checked against the pinned fields/enums.
Other types yield `UnsupportedComponent`; binding/function/template and common
weight/accessibility forms yield `UnsupportedComponentForm`, without certifying
those forms' schema validity. Invalid supported shapes yield `InvalidComponent`;
same-batch duplicate IDs yield `DuplicateComponentId`. Unknown fields are not dropped.

A root component need not be present at admission: pre-root nodes, unresolved
references, multiple parents, repeated edges and unreferenced nodes remain graph
state. No placeholders or reachability garbage collection are created. Memoized
whole-candidate graph checks include disconnected nodes; cycles yield the local
`ComponentCycle` result. Late resolution that introduces a cycle or excessive
known-node depth rejects the entire batch, without inventing the missing node.

After C0 and surface lookup, precedence is batch cardinality → shape then duplicate
per input item → same-ID policy → cumulative count/edge/byte bounds → cycle →
longest known-node depth → swap. Local bounds are **256 records / 1024 references
(including unresolved/repeated edges) / depth 32 / 64 KiB compact Default-encoded,
ID-sorted component array**. Failures of resource limits yield `StateLimitExceeded`.
The byte figure measures serialized component payload, not actual RAM including
JsonElement representation/whitespace/metadata, clones, snapshots and transients.
C0 input bounds, C2 Data limits and the 16-surface cap remain separate.

Only the fully checked candidate is swapped under the registry lock. Failure
preserves Body/Data/Components and derived graph state; snapshots, session/surface
ownership and concurrent independent-ID additions remain isolated. No wire-generation
protection across delete/recreate is added.

The unchanged pinned official three-node `updateComponents` message succeeds after
**local** exact-catalog create; it is not an official end-to-end pairing. Other graph
and boundary cases are local fixtures. Initial RED was missing-API compilation,
not a reproduced behavior failure. **144 C3 host checks**, C2 174, C1 75, C0 169,
producer 54 and the full UseCases suite pass; Release has zero warnings/errors.
The older malformed components test now expects `InvalidComponent` for its missing
required component field; known unsupported forms are tested separately.
This is not render-ready, general component-update or full-catalog support.
Binding/theme/actions/transport/renderer/provider/native integration remain outside
C3; earlier native/legacy evidence retains its original scope.

## C4 host semantic projection (2026-09-08)

[Registry.Project](../src/DisplayPresentation.UseCases/CanonicalSurfaceRegistry.cs)
consumes the C3 graph through separate [canonical projection records](../src/DisplayPresentation.UseCases/CanonicalSurfaceProjector.cs).
Legacy `VerticalGroup` lacks justify/align and `TextValue.Role` does not preserve
canonical variant behavior in the existing NUI renderer; those models/consumers
remain unchanged. C4 preserves only the C3-admitted literal attributes, optional
absence versus explicit enum values, source IDs and ordered occurrences. Each
occurrence has a separate immutable numeric child-index path; repeated/shared
references retain their source ID without collapsing occurrences.

`MissingSurface` and `WaitingForRoot` return no root. `PartialProjection` means
root-reachable missing-edge slots, not NUI placeholders or resource truncation.
`Projected` means a resolved host projection, not render-ready. Resource failure is
`ProjectionLimitExceeded` with `Root=null`. Data/create Body are not passed to the
projector; disconnected payload is not emitted. Components are captured under the
registry lock and projected outside it: results represent that immutable capture,
not necessarily the latest state at return. Success and failure leave registry
state unchanged; old projections survive later changes, delete and Clear.

Independent **local** output limits are **256 emitted slots / depth 32**, including
unresolved slots, checked before occurrence allocation/descent, and **64 KiB compact
Default-encoded UTF8**. The resource-only representation is `{surfaceId,root,status}`;
each node includes `kind`, `sourceId`, numeric `path`, plus Text `text`/optional
`variant`, or Column optional `justify`/`align` and ordered `children`. Absent
optionals are omitted; unresolved slots add no other fields. All keys, derived
fields, repeated strings, paths and status count through an explicit bounded writer,
with flushing before further expansion. This is not a new wire serializer or RAM
cap. C3 unique-node bounds alone do not bound repeated-graph expansion; a graph
accepted by C3 can exceed C4 limits without altering its admitted state.

Tests pass **328 C4 host checks**, including the unchanged pinned official three-node
message after **local** create/data setup, not an official end-to-end fixture.
Other optional/progressive/graph/boundary cases are local fixtures. Tests cover
exact 256/depth32/64KiB and +1 byte rejection, derived fields and repeated encoded
strings, dense DAG bounds, immutable paths/children, excluded payload and consistent
concurrent captures. Initial RED was missing-API compilation, not behavior RED.
C3 144, C2 174, C1 75, C0 169, producer 54 and the full UseCases suite pass; Release
has zero warnings/errors. Provider/NUI/native integration and full canonical
support remain incomplete; binding/Markdown/theme/actions/transport/rendering are
not added. Earlier C0–C3 and legacy/native records retain their dated scope.

## 2026-09-08 — C5 ingress contract blocker and host incompatibility coverage

This bounded review reuses pin `8ff4651232ab0e02b0123730b502711170637a3a`
(2026-09-04; inspected 2026-09-08), not a fresh check of latest upstream.
The generated [ServiceBase](../src/DisplayPresentation.ActionProvider/Generated/DisplayActions.cs)
exposes public `Sender`/`Instance`, with connection-specific Service creation and
`OnTerminate` on disconnect. These are real proxy-connection seams, not proof of
the original agent's authentication behind a broker or reconnect/session continuity.
The blocker is **unsettled transport and ownership contracts**, not missing identifiers.

The current `Presentation_Show` / `Tizen.Entity.Presentation` Template/Document pair
does not define canonical framing, ordered delivery, metadata/negotiation or the
canonical application meaning of Status. The existing legacy provider remains
unchanged. C0–C4 stay internal host instances; instance isolation is not authenticated
transport isolation, and catalog recognition is not a capability advertisement.
No wrapper, alias, fallback, router or new transport is introduced.

[ProductProducerInteropTests](../tests/DisplayPresentation.UseCases.Tests/ProductProducerInteropTests.cs)
passes the unchanged actual Browser `CreatePresentations` create message to C0 and
the real registry: **Recognized/CreateSurface**, then **UnsupportedCatalog** for its
`v0_9_1` catalog literal versus the registered `v0_9` literal. The nonempty LOCAL
setup's complete public snapshot (IDs/version/catalog/Body/Data/Components) remains
unchanged; subsequent producer component/data frames are not applied to bypass the
failure. This is host incompatibility coverage, not a production fix, canonical
success path or proof of the complete Browser canonical payload's validity.
ProductProducerInteropTests now has **60 checks = earlier 54 + 6 new checks**; existing legacy checks
are retained, and earlier 54-check records keep their historical scope. The full
UseCases suite passes with unchanged production; no artificial RED, repeated App
build, Packages or target execution is claimed. Evidence is preserved under
`/tmp/p5-canonical-boundary/c5-interop/` (`review.diff`, `result.json`, `suite.stdout`).

Product connection remains blocked pending five decisions for the platform-binding
alternative B: (1) exact pair framing/order/metadata and legacy distinction;
(2) trusted owner identity across broker/local execution/reconnect and delete/clear
authority; (3) session lifetime, teardown and late-message semantics;
(4) truthful negotiation of partial support and the catalog literal mismatch;
(5) whether Status acknowledges state admission or display application.
No external contact or provider/NUI/native implementation is part of this step.

## Host Text path-binding subset (2026-09-08)

This step extends the dated C3 literal admission and C4 Components-only projection
above; those records retain their original scope. Using the same pinned catalog,
Text accepts a literal or exact `{path:string}`. Exact repeat preserves optional
presence and the path; changed existing IDs remain `UnsupportedComponentUpdate`.
Malformed binding shape is invalid component input; FunctionCall/template forms
remain unsupported without certifying their schema validity. Literal text is not
interpolated, and no Markdown, functions or rendering behavior is added.

Only nonempty absolute object paths are evaluated. Local precedence is **1024 UTF8
bytes → 32 tokens → whole escape syntax → supported path subset → lookup**.
Malformed escapes return `InvalidBindingPath`; empty, `/`, relative, array or
nonobject traversal returns `UnsupportedBindingPath`. Decode `~1`/`~0` once without
trim or percent decoding; numeric/`01`/`-` are object keys and `/parent/` can select
an empty leaf key. Missing/uninitialized data produces a distinct pending-binding
slot and `PartialProjection`, separate from a missing component edge. Terminal
null, number, boolean, object and array return `UnsupportedBindingValue` with the
observed JSON kind and `Root=null`; this is a **local unsupported subset**, not
upstream-invalid data. An actual empty string resolves normally, with no null
coercion. Path/limit failures likewise return no accepted root.

Registry.Project clones Components and nullable Data together under one lock and
resolves outside it. Results are capture-consistent, not latest-at-return; stored
state and old projections remain unchanged. Bound output retains exact path,
resolution status, optional variant and occurrence identity. Only referenced text
is emitted, never whole Data/create Body or disconnected payload. Existing literal
resource representation is unchanged; bound nodes add `bindingPath`/`resolution`,
and pending nodes omit `text`. These fields and repeated resolved text/pending slots
count toward local emitted256/depth32/compact Default UTF8 64KiB limits. This is
resource-only accounting, not wire format or total RAM. No cache/subscription is added.

[Registry tests](../tests/DisplayPresentation.UseCases.Tests/CanonicalSurfaceRegistryTests.cs)
pass **250 Text binding host checks** using LOCAL fixtures, not rewritten official
messages. The concurrent writer checks all 100 `DataUpdated` results and final
`99/99`; 100 captures check pair consistency, without claiming all overlapped the
writer. Initial compile failures mixed unimplemented APIs with test argument/oracle
errors, not pure API RED or behavior RED; the first executable implementation passed.
Existing C0 169/C1 75/C2 174/C3 144/C4 328, producer 60 and the full UseCases suite
pass. The unchanged production SHA retains the prior App Release 0 warnings/errors;
only UseCases was rerun for the writer assertion correction. Evidence remains in
`/tmp/p5-canonical-boundary/binding-green-reviewed.txt` and `binding-release.txt`.
C5 ingress and its five binding/ownership decisions remain open. Provider,
transport, NUI/native and full canonical support are not established by this step.

## Wire envelope and safety boundary

The current Tizen Presentation compatibility adapter requires `Template` to be one JSON object with legacy v0.8 `surfaceUpdate` and `Document` to be one JSON object with matching `dataModelUpdate`. Both are untrusted. This split pair is retained for existing producers but is **not** labeled v0.9.1. A future canonical adapter must explicitly negotiate version/catalog and process the matching v0.9.1 lifecycle (`createSurface`, `updateComponents`, `updateDataModel`, `deleteSurface`) or a separately declared candidate profile; message names from different versions may not be mixed. Every adapter performs JSON parsing, type checks, schema/profile validation, binding resolution, depth/count/string limits, lifecycle/order checks, and stale-request checks before the NUI renderer receives a semantic tree.

```text
Presentation strings
  → bounded parser / profile validator
  → immutable SemanticSurface (not JSON)
  → OneUiProfileMapper + tokens
  → reusable NUI actors + one input reducer
  → measured visible View snapshot / View_ToPresentation
```

Payload values may not supply HTML, script, URL, image, font, color, spacing, dimensions, transforms, event handlers, or arbitrary layout. Unknown components/properties and stale/mismatched requests return typed failure and render the profile-owned recovery state; they never receive a guessed card fallback.

`catalogId` chooses a mutually supported semantic component/function catalog; it does not choose the native visual skin. DisplayPresentation owns native component selection, tokens, layout, accessibility, focus, and input behavior. Renderer-local functions are allowlisted, and agent events become bounded version-correct `action` messages. A2UI's logical surface lifecycle has no authority over the NUI/OS window.

## Logical surface, buffer, and native window

| Layer | Meaning | What it does not prove |
|---|---|---|
| 32-bit ARGB8888 buffer | Four 8-bit channels available to store each rendered pixel. | That alpha reaches/is blended by the window compositor, that another app is visible, or that input passes through. |
| Per-pixel-alpha native window | A transparent/translucent NUI window whose compositor blends pixel alpha. API10 provides `WindowMode.Transparent`, `SetTransparency(true)`, and alpha-zero `Color.Transparent`; `SetOpaqueState` is a visual-occlusion hint only. | Buffer format, A2UI conformance, pointer hit regions, key focus, runtime target support, or screenshot correctness. |
| A2UI logical surface | Version/catalog-scoped component/data state with create/update/delete lifecycle and actions. | An ARGB8888 buffer, a transparent/32-bit OS window, compositing, z-order, pass-through, or focus policy. |

The default/effective supported host remains opaque until a capability probe and installed Common Emulator evidence pass. Transparent overlay mode must fail closed to that opaque host. Input is independently configured and verified: transparency alone never means click-through; use only documented input regions/focus policy and prove D-pad/key, pointer/touch, Back, focus restoration, and pause/resume behavior.

## v0.1 renderer semantic matrix

The host parser currently implements the bounded legacy-compatible `Column`/path-bound-`Text` semantic-tree portion of this renderer matrix. `Button` and `TextField` remain renderer-specified but unimplemented and therefore reject as typed unsupported input until their official A2UI mapping, reducer, and NUI components are added. This matrix records renderer support; it does not redefine the Google A2UI catalog.

| A2UI component | Accepted v0.1 properties/bindings | Semantic node | reusable NUI component | One UI treatment | Input/state behavior | bounds and privacy |
|---|---|---|---|---|---|---|
| `Column` | `id`; ordered `children.explicitList` IDs (direct array retained as legacy repository shorthand); no styling props | `VerticalGroup` | `OneUiSection` / `OneUiStack` | 1920×1080 reference canvas; content at (132,176), 1656×660; four 120px fields per page; neutral surface and explicit group actors | Not independently focusable. D-pad traverses its enabled focusable descendants. | Maximum depth 4, 512 nodes; IDs ≤64 ASCII-safe chars; no hidden content node publication. |
| `Text` | `id`; `text.path` through bounded nested objects/arrays to a scalar string in document `value`; optional profile enum `role`: `headline`, `title`, `body`, `label`, `supporting` | `TextValue` | `OneUiText` | Profile picks type, never payload font: reference PixelSize: headline 44, title 36, body 28, label 24, supporting 26; ink/muted tokens provide hierarchy | Static, non-focusable unless it is the label child of a supported control. Loading uses profile skeleton; error is profile-owned. | One bound value ≤256 chars; only scalar strings; text is escaped; source data fields not bound into rendered nodes are not exposed. |
| `Button` | `id`; exactly one `Text` label child; `action.name` from a registered allowlist; `enabled` boolean binding | `Command` | `OneUiButton` | Primary filled button or secondary outlined button is selected by registered action semantics, not payload color; minimum 48dp hit/focus height | D-pad/keyboard arrows move in deterministic document order; Enter/Space and pointer Down+Up-inside dispatch the same command; disabled is not focusable; Back restores invoker after profile modal recovery. | Action names ≤64 chars and must be registered by app; no payload callback/script; action argument is bounded schema data only. |
| `TextField` | `id`; `label` Text child; `value.path`; `inputType` enum `shortText`/`number`/`obscured`; `enabled` binding | `Input` | `OneUiTextField` | Label above field, 1dp neutral outline, 12dp radius, 16dp internal padding; focused outline + elevation/scale cue | Enter begins/commits edit according to active editor; pointer focuses; Back cancels edit and restores focus; obscured text is never reflected in Annotation or Presentation snapshot. | Maximum input 256 chars; no remote validation or arbitrary regex; obscured values are redacted. |

All other catalogue components/properties are **unsupported in v0.1**. The typed result is `Unsupported` with the component/property name and the visible profile error state offers an enabled “Dismiss” recovery control. `Image`, web/HTML, URL, style, arbitrary `Row`, custom templates, and payload-selected color/font/layout are deliberately unsupported until a source-audited profile extension is implemented and tested.

## State, focus, and responsive policy

| State | Profile behavior | Focus / input |
|---|---|---|
| Loading | Profile-owned text skeleton and noninteractive progress label; no stale prior surface remains annotated. | Focus moves to the enabled Cancel/Dismiss recovery only when present; otherwise the app root holds focus safely. |
| Valid | Semantic component tree is rendered in document order, preserving hierarchy rather than flattening title/body fields. | Initial focus is first enabled command/input; visual focus uses both contrast outline and 1.02 scale/elevation. |
| Selected/pressed | Only components with a registered semantic selection/action may become selected. | Pointer requires Down then Up inside; interrupted/leave cancels; key and pointer share reducer command. |
| Disabled | Muted text/surface plus opacity and no focusability; never color-only. | Skipped by directional traversal and activation. |
| Invalid / mismatch / oversize / privacy | Profile-owned error title, bounded reason, and recovery command; untrusted source is not partially rendered. | Recovery is initial focus and Back dismisses it, restoring prior valid focus if one exists. |
| Unsupported | Same safe recovery surface with typed unsupported reason; no generic-card fallback. | Same as invalid. |

NUI uses an inset-aware 1920×1080 reference canvas with one centered uniform ancestor transform. Parsing is cancellable off the UI thread. Only a completed request matching the current monotonically increasing request ID can publish a visible tree. Native bounds are measured after layout and must be finite and positive before publishing a `Tizen.Entity.View`.

## Annotation and A2UI round trip

The current page has a stable View ID (`display:<surface-id>:page:<index>`); its enabled Previous/Next/Dismiss controls append `:control:<name>`. These annotations serialize the visible semantic page only, not the full source surface. The enclosing `Tizen.Entity.View` owns measured `ScreenBounds`/`WindowBounds` and actual `IsFocused`; nested `Annotation` carries `EntityType`, `EntityId`, and generated `Presentation.ToJson()` in `EntityInfo`. Snapshot content excludes obscured values, unrendered source fields, and parser diagnostics that could disclose raw payloads.

`View_ToPresentation` reconstructs separate `surfaceUpdate` and `dataModelUpdate` JSON from the currently visible semantic page, not the raw incoming payload or off-page content. It preserves surface ID, supported node order, allowed properties, bounded resolved current values, enabled/selected state, and profile version, while remaining semantically equivalent to the rendered tree.

For the current compatibility profile this reconstruction intentionally remains legacy v0.8 so existing consumers are not broken. Canonical v0.9.1 publication, when added, must emit its own ordered lifecycle messages and catalog declaration through a distinct adapter/contract; v1.0 remains Candidate and cannot be advertised as stable.

## Evidence status

- **Protocol source audit:** current v0.9.1 lifecycle/catalog baseline and v1.0 Candidate status inspected; canonical v0.9.1 parser conformance is not implemented.
- **Legacy compatibility:** current `surfaceUpdate` / `dataModelUpdate` parser and serializer are bounded host-tested compatibility behavior, not official v0.9.1 conformance.
- **Renderer source audit:** complete for the v0.1 Samsung One UI adaptation references above.
- **Executable browser preview:** Chromium checks pass for actual Calendar/reminder fixtures, 100 pages, keyboard, resize, dismissal and malformed/unsupported states. The FHD browser image is recorded in [UI_PARITY.md](UI_PARITY.md); native comparison remains pending.
- **Cross-app fixtures:** Calendar event/reminder and shared page/control producers pass host parser/serializer checks. Earlier Browser/PhotoGallery gaps remain outside this follow-up; no fresh positive evidence is claimed for those apps.
- **Native/Aurum parity and target round-trip:** not yet verified; tracked in `UI_PARITY.md`. The app now publishes a lock-protected View snapshot only after `CalculateScreenPositionSize()` yields finite positive geometry; the generated View provider maps discovery and `View_ToPresentation` back to that exact current generated Presentation snapshot. Invalid/unsupported inputs instead render a profile-owned `Dismiss` recovery control; dismissing never restores a prior payload. This is host-build evidence, not target evidence.
- **Transparent overlay:** unverified. No claim may be made from an ARGB8888 buffer, host compile, `WindowMode.Transparent`, `SetTransparency(true)`, or transparent View/root color. Required evidence is a Common Emulator native screenshot sequence over a known underlying app (fully transparent, semitransparent, and opaque regions), plus D-pad/key focus, pointer/touch inside/outside declared hit regions, Back/dismiss, focus restoration, pause/resume, and opaque-fallback traces.
