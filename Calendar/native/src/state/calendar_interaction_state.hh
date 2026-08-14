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

#ifndef CALENDAR_NATIVE_STATE_CALENDAR_INTERACTION_STATE_HH_
#define CALENDAR_NATIVE_STATE_CALENDAR_INTERACTION_STATE_HH_

#include <optional>
#include <set>
#include <string>
#include <vector>

#include "base/offset_date_time.hh"
#include "domain/calendar_event.hh"
#include "domain/calendar_event_repository.hh"
#include "domain/calendar_reminder.hh"
#include "state/calendar_ui_state.hh"

namespace calendar {
namespace state {

enum class CalendarSurface {
  kCalendar,
  kEventDetail,
  kEventEditor,
  kDeleteEventConfirmation,
  kReminderList,
  kReminderEditor,
  kDeleteReminderConfirmation,
  kSearch,
};

struct CalendarEditorState {
  std::optional<std::string> event_id;
  std::string title;
  base::OffsetDateTime start;
  base::OffsetDateTime end;
  std::string location;
  std::string note;
  std::set<int> reminder_offsets;

  bool IsEditing() const { return event_id.has_value(); }
  bool CanSave() const { return !ValidationMessage().has_value(); }
  std::optional<std::string> ValidationMessage() const;

  static CalendarEditorState CreateNew(const base::Date &date);
  static CalendarEditorState CreateExisting(const domain::CalendarEvent &event,
                                            const std::vector<int> &offsets);

  CalendarEditorState WithTitle(const std::string &new_title) const;
  CalendarEditorState WithLocation(const std::string &new_location) const;
  CalendarEditorState WithNote(const std::string &new_note) const;
  CalendarEditorState WithRange(const base::OffsetDateTime &new_start,
                                const base::OffsetDateTime &new_end) const;
  CalendarEditorState ToggleReminder(int offset_minutes) const;
};

struct CalendarReminderEditorState {
  std::optional<std::string> reminder_id;
  std::string title;
  base::OffsetDateTime due_at;
  std::string note;
  bool is_completed;

  bool IsEditing() const { return reminder_id.has_value(); }
  bool CanSave() const { return !ValidationMessage().has_value(); }
  std::optional<std::string> ValidationMessage() const;

  static CalendarReminderEditorState
  CreateNew(const base::OffsetDateTime &suggested_due);
  static CalendarReminderEditorState
  CreateExisting(const domain::CalendarReminder &reminder);

  CalendarReminderEditorState WithTitle(const std::string &new_title) const;
  CalendarReminderEditorState
  WithDueAt(const base::OffsetDateTime &new_due_at) const;
  CalendarReminderEditorState WithNote(const std::string &new_note) const;
  CalendarReminderEditorState WithCompleted(bool completed) const;
  domain::CalendarReminder ToDomain(const std::string &stable_id) const;
};

struct CalendarSearchState {
  std::string keyword;
  base::Date start_date;
  base::Date end_date_exclusive;
  std::vector<std::string> result_event_ids;
  bool search_title;
  bool search_location;
  bool search_note;
  bool has_applied;
  long applied_repository_version;

  bool CanApply() const;
  std::optional<std::string> ValidationMessage() const;

  static CalendarSearchState Create(const base::Date &visible_month);

  CalendarSearchState WithKeyword(const std::string &new_keyword) const;
  CalendarSearchState WithPeriod(const base::Date &new_start,
                                 const base::Date &new_end) const;
  CalendarSearchState WithFields(bool title, bool location, bool note) const;

  CalendarSearchState Apply(domain::CalendarEventRepository *repository) const;
};

class CalendarInteractionState {
public:
  CalendarInteractionState() = default;

  static CalendarInteractionState Create(const CalendarUiState &calendar);

  CalendarUiState calendar;
  CalendarSurface surface = CalendarSurface::kCalendar;
  std::optional<std::string> selected_event_id;
  std::optional<CalendarEditorState> event_editor;
  std::optional<std::string> selected_reminder_id;
  std::optional<CalendarReminderEditorState> reminder_editor;
  std::optional<CalendarSearchState> search;
  std::optional<std::string> search_return_event_id;

  CalendarInteractionState OpenNewEvent() const;
  CalendarInteractionState OpenEventDetail(const std::string &event_id) const;
  CalendarInteractionState
  OpenEventEditor(const domain::CalendarEvent &event,
                  const std::vector<int> &reminder_offsets) const;
  CalendarInteractionState RequestEventDelete() const;
  CalendarInteractionState CancelEventDelete() const;

  CalendarInteractionState OpenReminderList() const;
  CalendarInteractionState
  OpenNewReminder(const base::OffsetDateTime &suggested_due) const;
  CalendarInteractionState
  OpenReminderEditor(const domain::CalendarReminder &reminder) const;
  CalendarInteractionState RequestReminderDelete() const;

  CalendarInteractionState OpenSearch() const;
  CalendarInteractionState OpenSearchResult(const std::string &event_id) const;

  CalendarInteractionState Back() const;
};

} // namespace state
} // namespace calendar

#endif // CALENDAR_NATIVE_STATE_CALENDAR_INTERACTION_STATE_HH_
