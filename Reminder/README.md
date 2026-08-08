# Reminder

Production-style Tizen NUI example for the complete `Tizen.Action.Schedule` category.

- **Product shell:** approved B Focused Workspace — smart navigation, bounded list, detail/editor
- **App ID:** `org.tizen.actionexamples.reminder`
- **Target:** Tizen 10.1 Common Emulator compatible package, Tizen.NET 13
- **Actions:** all 10 Schedule methods
- **View:** current `ScreenBounds` / `WindowBounds` / `Annotation.EntityInfo` contract
- **Common behavior:** viewing/recording reservations are deterministic app-owned simulations, not TV tuner operations

Architecture keeps `Reminder.Domain`, `Reminder.Persistence`, and `Reminder.UseCases` free of Tizen runtime dependencies. NUI, Schedule RPC, and View RPC are adapters around the same `ScheduleService` instance.

See [approved requirements](docs/REQUIREMENTS_DRAFT.md), [architecture review](docs/REQUIREMENTS_ARCHITECTURE_REVIEW.md), and [build/E2E guide](docs/BUILD_E2E_GUIDE.md).
