// Copyright (c) 2026 Samsung Electronics Co., Ltd. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#include "base/offset_date_time.hh"
#include "domain/calendar_reminder.hh"
#include "harness.hh"
#include "state/calendar_interaction_state.hh"
#include "state/calendar_ui_state.hh"
#include "testing_factories.hh"

using namespace calendar;

namespace {

CALENDAR_TEST(view_mode_change_reducer) {
  // Setup initial state
  base::Date today(2026, 8, 14);
  state::CalendarUiState ui_state = state::CalendarUiState::Create(today);

  // Initial mode should be Month
  EXPECT_EQ(static_cast<int>(ui_state.view_mode()),
            static_cast<int>(state::CalendarViewMode::kMonth));

  // Apply change view mode command
  state::CalendarUiCommand cmd;
  cmd.value =
      state::CalendarUiCommand::ChangeViewMode{state::CalendarViewMode::kWeek};

  ui_state = state::CalendarUiReducer::Reduce(ui_state, cmd, today, 0);

  // Mode should now be Week
  EXPECT_EQ(static_cast<int>(ui_state.view_mode()),
            static_cast<int>(state::CalendarViewMode::kWeek));
}

CALENDAR_TEST(navigation_reducer_previous_period) {
  base::Date today(2026, 8, 14);
  state::CalendarUiState ui_state = state::CalendarUiState::Create(today);

  // Month mode, previous period should go back a month (July)
  state::CalendarUiCommand cmd;
  cmd.value = state::CalendarUiCommand::ShowPreviousPeriod{};

  ui_state = state::CalendarUiReducer::Reduce(ui_state, cmd, today, 0);

  EXPECT_EQ(ui_state.visible_month().year(), 2026);
  EXPECT_EQ(ui_state.visible_month().month(), 7);
}

CALENDAR_TEST(navigation_reducer_activate_today) {
  base::Date today(2026, 8, 14);
  state::CalendarUiState ui_state = state::CalendarUiState::Create(today);

  // Navigate away
  ui_state = ui_state.MovePeriod(-2);
  EXPECT_EQ(ui_state.visible_month().month(), 6);

  // Apply ActivateToday command
  state::CalendarUiCommand cmd;
  cmd.value = state::CalendarUiCommand::ActivateToday{};
  ui_state = state::CalendarUiReducer::Reduce(ui_state, cmd, today, 0);

  // Should be back to August
  EXPECT_EQ(ui_state.visible_month().month(), 8);
  EXPECT_EQ(ui_state.selected_date().day(), 14);
}

CALENDAR_TEST(month_surface_selected_day_agenda) {
  base::Date today(2026, 8, 14);
  state::CalendarUiState ui_state = state::CalendarUiState::Create(today);

  EXPECT_EQ(static_cast<int>(ui_state.view_mode()),
            static_cast<int>(state::CalendarViewMode::kMonth));
  EXPECT_FALSE(ui_state.is_agenda_open());

  ui_state = ui_state.EnterAgenda(2);
  EXPECT_TRUE(ui_state.is_agenda_open());
  EXPECT_EQ(static_cast<int>(ui_state.focus_region()),
            static_cast<int>(state::CalendarFocusRegion::kAgendaEvents));

  EXPECT_EQ(static_cast<int>(ui_state.HandleBack()),
            static_cast<int>(state::CalendarBackResult::kCloseAgenda));
  ui_state = ui_state.ReturnToMonth();
  EXPECT_FALSE(ui_state.is_agenda_open());
}

CALENDAR_TEST(search_overlay_toggles) {
  base::Date today(2026, 8, 14);
  state::CalendarSearchState search = state::CalendarSearchState::Create(today);
  EXPECT_TRUE(search.search_title);

  search = search.WithKeyword("test");
  search = search.WithFields(false, true, false);

  EXPECT_EQ(search.keyword, "test");
  EXPECT_FALSE(search.search_title);
  EXPECT_TRUE(search.search_location);
  EXPECT_FALSE(search.search_note);
}

CALENDAR_TEST(event_detail_create_edit_delete) {
  base::Date today(2026, 8, 14);
  state::CalendarEditorState editor =
      state::CalendarEditorState::CreateNew(today);
  EXPECT_FALSE(editor.IsEditing());

  editor = editor.WithTitle("New Event");
  EXPECT_EQ(editor.title, "New Event");
  EXPECT_TRUE(editor.CanSave());

  state::CalendarInteractionState interaction =
      state::CalendarInteractionState::Create(
          state::CalendarUiState::Create(today));
  interaction = interaction.OpenEventDetail("event-1");
  interaction = interaction.RequestEventDelete();
  EXPECT_EQ(static_cast<int>(interaction.surface),
            static_cast<int>(state::CalendarSurface::kDeleteEventConfirmation));
}

CALENDAR_TEST(reminder_list_create_edit) {
  base::OffsetDateTime due;
  state::CalendarReminderEditorState editor =
      state::CalendarReminderEditorState::CreateNew(due);
  EXPECT_FALSE(editor.IsEditing());

  editor = editor.WithTitle("New Reminder");
  EXPECT_EQ(editor.title, "New Reminder");
  EXPECT_TRUE(editor.CanSave());

  domain::CalendarReminder reminder = editor.ToDomain("rem-1");
  EXPECT_EQ(reminder.title(), "New Reminder");
}

CALENDAR_TEST(editor_states_update_every_user_editable_field) {
  base::Date today(2026, 8, 14);
  auto event = state::CalendarEditorState::CreateNew(today)
                   .WithTitle("Planning")
                   .WithLocation("Studio")
                   .WithNote("Bring the agenda")
                   .ToggleReminder(30);
  EXPECT_EQ(event.location, "Studio");
  EXPECT_EQ(event.note, "Bring the agenda");
  EXPECT_TRUE(event.reminder_offsets.find(30) != event.reminder_offsets.end());

  auto due = base::OffsetDateTime::FromLocalParts(today, 16, 0, 0, 0, 540);
  auto reminder = state::CalendarReminderEditorState::CreateNew(due)
                      .WithTitle("Call")
                      .WithNote("Customer")
                      .WithDueAt(due.AddHours(1))
                      .WithCompleted(true);
  EXPECT_EQ(reminder.note, "Customer");
  EXPECT_EQ(reminder.due_at, due.AddHours(1));
  EXPECT_TRUE(reminder.is_completed);
}

CALENDAR_TEST(back_restores_search_detail_editor_and_confirmation_hierarchy) {
  base::Date today(2026, 8, 14);
  domain::CalendarEventRepository repository;
  repository.TryAdd(testing::MakeEvent("event-1", "Search result",
                                       testing::At(2026, 8, 14, 14, 0)));
  auto interaction = state::CalendarInteractionState::Create(
      state::CalendarUiState::Create(today));
  interaction = interaction.OpenSearch();
  interaction.search =
      interaction.search->WithKeyword("result").Apply(&repository);
  interaction = interaction.OpenSearchResult("event-1");
  EXPECT_EQ(static_cast<int>(interaction.Back().surface),
            static_cast<int>(state::CalendarSurface::kSearch));

  interaction = interaction.OpenEventEditor(
      repository.ResolveByIds({"event-1"}).events[0], {});
  EXPECT_EQ(static_cast<int>(interaction.Back().surface),
            static_cast<int>(state::CalendarSurface::kEventDetail));
  interaction = interaction.Back().RequestEventDelete();
  EXPECT_EQ(static_cast<int>(interaction.Back().surface),
            static_cast<int>(state::CalendarSurface::kEventDetail));
}

} // namespace
