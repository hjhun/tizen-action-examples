# ActionC binding generation

## Rule

All application-side code for Tizen Action categories—including ordinary Action and View Action provider bindings—must be generated with `actionc`. Do not create, copy, or hand-author TIDL-generated Action binding source.

### Temporary `tidlc` C# UDS compatibility exception

`actionc → action2tidl → tidlc` currently emits
`HasPrivilegeLocal(b.Sender, item)` in C# UDS `CheckPrivilege()`. Public Tizen
10.1 RPCPort does not supply that API, so the generated provider can compile but
terminate at Action dispatch. Until the framework fixes `tidlc`, post-generation
editing is explicitly required for every generated C# binding containing this
call:

```csharp
// has = HasPrivilegeLocal(b.Sender, item);
// Disabled for compatibility with runtimes that omit StubBase.HasPrivilegeLocal.
has = false;
```

Keep the surrounding no-privilege fast path intact. Declared-privilege methods
must remain **fail-closed**; never replace the call with `true` or dispatch a
privileged Action without validation. Apply this exception after each full
category generation, record every affected binding, and remove it only after a
fixed generator plus generator/runtime regression coverage is available.

## Inputs and ABI

`actionc` consumes authoritative `.action` and `.entity` catalog files, converts them through `action2tidl`, and invokes `tidlc`. Generate the entire category so its `action.seq` order and wire method IDs remain compatible.

Use the framework catalog as the data directory:

```sh
actionc -a <category> -l C# \
  -d /home/hjhun/samba/workspace/appfw/tizen-action/default-actions \
  -o <output-base>
```

For Browser:

```sh
actionc -a Tizen.Action.Browser -l C# -d "$ACTIONC_DATA_DIR" -o TizenActionBrowser
actionc -a Tizen.Action.View -l C# -d "$ACTIONC_DATA_DIR" -o TizenInternalActionView
```

## Required verification

1. Confirm the generated output is byte-for-byte the checked-in binding before applying the documented `HasPrivilegeLocal` compatibility exception (apart from an explicitly documented generator version banner, if applicable).
2. For every C# UDS binding, inspect and apply the fail-closed exception when `HasPrivilegeLocal(b.Sender, item)` is emitted; record the affected files.
3. Build the provider and application.
4. Package, install, launch, and call advertised Actions with target `action-tool`; confirm provider liveness and no new crash dump.
5. Keep handwritten provider implementations separate from generated bindings; update only their namespace/type references if a pure `actionc` output changes them.
