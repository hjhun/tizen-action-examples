#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TARGET="${APP_DIR}/src/Reminder.App/Reminder.App.csproj"
ACTION_BINDINGS=(
    "Tizen.Action.Schedule|ReminderScheduleActionProvider|src/Reminder.ScheduleActionProvider/Generated/ReminderScheduleActionProvider.cs"
    "Tizen.Action.View|ReminderViewActionProvider|src/Reminder.ViewActionProvider/Generated/ReminderViewActionProvider.cs"
)

# shellcheck source=../scripts/app-build-common.sh
source "${APP_DIR}/../scripts/app-build-common.sh"
run_app_build "$@"
