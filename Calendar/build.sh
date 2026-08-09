#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TARGET="${APP_DIR}/src/Calendar.App/Calendar.App.csproj"
ACTION_BINDINGS=(
    "Tizen.Action.Calendar|CalendarActionProvider|src/Calendar.ActionProvider/Generated/CalendarActionProvider.cs"
    "Tizen.Action.Schedule|ScheduleReminderActionProvider|src/Calendar.ScheduleActionProvider/Generated/ScheduleReminderActionProvider.cs"
    "Tizen.Internal.Action.View|CalendarViewActionProvider|src/Calendar.ViewActionProvider/Generated/CalendarViewActionProvider.cs"
)

# shellcheck source=../scripts/app-build-common.sh
source "${APP_DIR}/../scripts/app-build-common.sh"
run_app_build "$@"
