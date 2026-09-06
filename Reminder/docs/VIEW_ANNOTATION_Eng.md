# Reminder current ViewAnnotation contract (API 14)

[Korean contract](VIEW_ANNOTATION.md) · [Stage 1 validation](../../Calendar/docs/STAGE1_VALIDATION.md)

GetAnnotatedViews returns the current rendered page, meaningful Entities and
controls. GetFocusedView returns actual annotated NUI focus from that same frame;
missing focus is a typed failure. FindById looks only in the current store.
Page/control Entities use generated TizenEntity serialization and a versioned
Extra document with page context and optional live draft fields. Persisted
Entities keep stable IDs; different visible copies have distinct View IDs.

Bounds come from CalculateScreenPositionSize after the single ancestor transform.
Only finite positive bounds are published. WindowBounds subtracts the actual
window origin; if that origin is unavailable, a zero non-null WindowBounds DTO
satisfies the generated serializer. No design-size fallback is used.

Root replacement and pause/termination clear the old snapshot. Focus, text,
layout and resize changes publish the measurable live tree atomically immediately,
then remeasure on relayout and a 50ms timer. Ordinary refresh does not create a
clear-before-publish gap. Hidden or detached surfaces are excluded. Resume
renders current state. Delete confirmation traps focus and excludes the background.

View_ToPresentation resolves View ID, EntityType and EntityId against the current
store. It uses authoritative current EntityInfo, ignoring stale or forged caller
content. Removed or mismatched identities fail. Presentations bind current state
using the legacy A2UI v0.8 split surfaceUpdate / dataModelUpdate profile. This is
not a claim of canonical v0.9.1 conformance.

Twenty consecutive list/find pairs, actual focus/bounds, bounded ID failures and
View-to-Display round trips passed on the installed Common Emulator. FHD, UHD and
DCI 4K were measured; 8K host geometry passed while native 8K is unverified due to
the emulator DRM mode constraint. See the stage 1 record for per-gate evidence.
