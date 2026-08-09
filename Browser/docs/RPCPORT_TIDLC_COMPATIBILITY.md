# tidlc `HasPrivilegeLocal` compatibility workaround

## Status

This is a temporary application-side workaround for a known `tidlc` C# UDS-stub
bug. `actionc` generates the call through `action2tidl` and `tidlc`; Browser
application code did not introduce it.

The Public Tizen 10.1 Common Emulator provides an RPCPort runtime that does not
contain `StubBase.HasPrivilegeLocal(string, string)`. A generated binding built
against a newer Tizen.NET package can therefore compile but terminate its
provider with `MissingMethodException` at Action dispatch time.

The framework generator is expected to be fixed. Until then, keep the generator
input and public Action ABI unchanged and apply the documented post-generation
workaround to every generated C# UDS binding that contains the call.

## Temporary procedure

1. Generate the complete Action category with `actionc`; do not generate an
   individual Action because the category sequence defines method IDs.
2. Inspect every generated C# binding for `HasPrivilegeLocal(b.Sender, item)`.
3. Comment out the generated direct call and make the guarded branch fail closed:

   ```csharp
   // has = HasPrivilegeLocal(b.Sender, item);
   // Disabled for compatibility with runtimes that omit StubBase.HasPrivilegeLocal.
   has = false;
   ```

4. Preserve the existing early return for methods that have no declared
   privileges. For a method with declared privileges, return denial instead of
   dispatching without validation.
5. Build the provider/application, run its host regression tests, package and
   install it on the Common Emulator, then use `action-tool` to exercise a
   positive Action and a meaningful bounded-negative Action. Confirm that the
   provider process remains alive and that no new application crash dump is
   created.

This is a **fail-closed** compatibility rule. Do not replace the call with an
unconditional `true`, and do not allow a privileged method to run because the
runtime cannot validate it.

## Current application coverage

The workaround is applied after generation to these bindings:

- Browser: `Tizen.Action.Browser`, `Tizen.Internal.Action.View`
- Calendar: `Tizen.Action.Calendar`, `Tizen.Action.ScheduleReminder`,
  `Tizen.Internal.Action.View`
- Reminder: `Tizen.Action.ScheduleReminder`, `Tizen.Internal.Action.View`
- DisplayPresentation: `Tizen.Action.Display`, `Tizen.Internal.Action.View`
- PhotoGallery: `Tizen.Action.Photo`

Browser Common Emulator evidence after the workaround covers all five advertised
Browser Actions and all four View Actions. Typed Action responses were received,
ViewAnnotation snapshots and presentation conversion succeeded, the Browser
process remained alive, and no new Browser crash dump was observed.

## Removal criteria

When the `tidlc` fix is available:

1. add generator-level regression coverage for the target runtime contract;
2. regenerate each entire category with the fixed `actionc` toolchain;
3. remove this post-generation workaround from every binding;
4. repeat build, host tests, package install, Action/View `action-tool` tests,
   provider-liveness, and crash-dump checks.

Do not retain this workaround after generated output can safely call a runtime
capability-aware helper supplied by the framework.
