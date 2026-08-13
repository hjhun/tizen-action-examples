# tidlc `HasPrivilegeLocal` ABI incident record

## Status

This document records two deliberately separate Browser evidence tracks:

- **Canonical provenance:** the complete Browser and View categories emitted by the authoritative `default-actions` catalog via `actionc` are the only canonical bindings. They contain the direct `HasPrivilegeLocal` call and reproduce the Common Emulator 10.1 dispatch failure.
- **Historical compatibility experiment:** earlier tracked Browser bindings contained a post-generation, fail-closed exclusion of that call. It is historical target-RPC evidence only, not canonical generated output, and is not permitted for future generation under the current project rules.

`actionc` generates the call through `action2tidl` and `tidlc`; Browser application code did not introduce it.

The Public Tizen 10.1 Common Emulator provides an RPCPort runtime that does not contain `StubBase.HasPrivilegeLocal(string, string)`. A generated binding built against a newer Tizen.NET package can therefore compile but terminate its provider with `MissingMethodException` at Action dispatch time.

## Current rule

Generate the complete Action category with `actionc` and retain the generated binding unchanged. If it calls `HasPrivilegeLocal(b.Sender, item)` while the selected target runtime omits that API, record a framework generator/runtime ABI blocker. Do not comment out, replace, guard, or otherwise edit the generated call.

The historical post-generation edit is not a template for new work. Resolve the incompatibility in the framework generator, runtime, or a separately designed application boundary without modifying generated bindings. Preserve the existing early return for methods that have no declared privileges; do not weaken declared-privilege validation.

## Historical Browser evidence

The former compatibility experiment affected these generated bindings:

- Browser: `Tizen.Action.Browser`, `Tizen.Action.View`

At the time it was performed, Browser Common Emulator evidence covered all five advertised Browser Actions and all four View Actions. Typed Action responses were received, ViewAnnotation snapshots and presentation conversion succeeded, the Browser process remained alive, and no new Browser crash dump was observed. This evidence does not establish that a fresh, unmodified generated binding is compatible with the same runtime.

## Remediation criteria

Before this ABI incident can be closed:

1. add generator/runtime regression coverage for the target contract;
2. regenerate each entire category with the corrected `actionc` toolchain and retain the output unchanged;
3. verify byte-for-byte generated provenance;
4. repeat build, host tests, package install, Action/View `action-tool` tests, provider-liveness, and crash-dump checks.
