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

#include "state/calendar_interaction_state.hh"

#include <algorithm>
#include <stdexcept>

#include "base/strings.hh"
#include "domain/calendar_search_criteria.hh"

namespace calendar {
namespace state {
namespace {

using ::calendar::base::Trim;

} // namespace

std::optional<std::string> CalendarEditorState::ValidationMessage() const {
  if (Trim(title).empty()) {
    return "Title is required.";
  }
  if (end <= start) {
    return "End time must be after start time.";
  }
  return std::nullopt;
}

CalendarEditorState CalendarEditorState::CreateNew(const base::Date &date) {
  CalendarEditorState state;
  state.event_id = std::nullopt;
  state.title = "";
  // 9 AM in local zone (using naive offset for state creation like C#)
  state.start = base::OffsetDateTime::FromLocalParts(date, 9, 0, 0, 0, 9 * 60);
  state.end = base::OffsetDateTime::FromLocalParts(date, 10, 0, 0, 0, 9 * 60);
  state.location = "";
  state.note = "";
  return state;
}

CalendarEditorState
CalendarEditorState::CreateExisting(const domain::CalendarEvent &event,
                                    const std::vector<int> &offsets) {
  CalendarEditorState state;
  state.event_id = event.id();
  state.title = event.title();
  state.start = event.start();
  state.end = event.end();
  state.location = event.location();
  state.note = event.note();

  std::set<int> allowed = {10, 30, 60, 1440};
  for (int offset : offsets) {
    if (allowed.find(offset) == allowed.end()) {
      throw std::out_of_range("Invalid reminder offset.");
    }
    state.reminder_offsets.insert(offset);
  }

  return state;
}

CalendarEditorState
CalendarEditorState::WithTitle(const std::string &new_title) const {
  CalendarEditorState next = *this;
  next.title = new_title;
  return next;
}

CalendarEditorState
CalendarEditorState::WithLocation(const std::string &new_location) const {
  CalendarEditorState next = *this;
  next.location = new_location;
  return next;
}

CalendarEditorState
CalendarEditorState::WithNote(const std::string &new_note) const {
  CalendarEditorState next = *this;
  next.note = new_note;
  return next;
}

CalendarEditorState
CalendarEditorState::WithRange(const base::OffsetDateTime &new_start,
                               const base::OffsetDateTime &new_end) const {
  CalendarEditorState next = *this;
  next.start = new_start;
  next.end = new_end;
  return next;
}

CalendarEditorState
CalendarEditorState::ToggleReminder(int offset_minutes) const {
  std::set<int> allowed = {10, 30, 60, 1440};
  if (allowed.find(offset_minutes) == allowed.end()) {
    throw std::out_of_range("Invalid reminder offset.");
  }

  CalendarEditorState next = *this;
  if (next.reminder_offsets.find(offset_minutes) !=
      next.reminder_offsets.end()) {
    next.reminder_offsets.erase(offset_minutes);
  } else {
    next.reminder_offsets.insert(offset_minutes);
  }
  return next;
}

std::optional<std::string>
CalendarReminderEditorState::ValidationMessage() const {
  if (Trim(title).empty()) {
    return "Title is required.";
  }
  // Simplified validation: due_at is always valid since it's a value type.
  return std::nullopt;
}

CalendarReminderEditorState CalendarReminderEditorState::CreateNew(
    const base::OffsetDateTime &suggested_due) {
  CalendarReminderEditorState state;
  state.reminder_id = std::nullopt;
  state.title = "";
  state.due_at = suggested_due;
  state.note = "";
  state.is_completed = false;
  return state;
}

CalendarReminderEditorState CalendarReminderEditorState::CreateExisting(
    const domain::CalendarReminder &reminder) {
  CalendarReminderEditorState state;
  state.reminder_id = reminder.id();
  state.title = reminder.title();
  state.due_at = reminder.due_at();
  state.note = reminder.note();
  state.is_completed = reminder.is_completed();
  return state;
}

CalendarReminderEditorState
CalendarReminderEditorState::WithTitle(const std::string &new_title) const {
  CalendarReminderEditorState next = *this;
  next.title = new_title;
  return next;
}

CalendarReminderEditorState CalendarReminderEditorState::WithDueAt(
    const base::OffsetDateTime &new_due_at) const {
  CalendarReminderEditorState next = *this;
  next.due_at = new_due_at;
  return next;
}

CalendarReminderEditorState
CalendarReminderEditorState::WithNote(const std::string &new_note) const {
  CalendarReminderEditorState next = *this;
  next.note = new_note;
  return next;
}

CalendarReminderEditorState
CalendarReminderEditorState::WithCompleted(bool completed) const {
  CalendarReminderEditorState next = *this;
  next.is_completed = completed;
  return next;
}

domain::CalendarReminder
CalendarReminderEditorState::ToDomain(const std::string &stable_id) const {
  domain::CalendarReminder r;
  std::string err;
  if (!domain::CalendarReminder::TryCreate(reminder_id.value_or(stable_id),
                                           title, due_at, note, &r, &err)) {
    throw std::runtime_error("Failed to create domain reminder: " + err);
  }
  return r.WithCompleted(is_completed);
}

bool CalendarSearchState::CanApply() const {
  return end_date_exclusive > start_date &&
         (search_title || search_location || search_note);
}

std::optional<std::string> CalendarSearchState::ValidationMessage() const {
  if (CanApply())
    return std::nullopt;
  if (end_date_exclusive <= start_date) {
    return "Exclusive end date must be after start date.";
  }
  return "Select at least one field: Title, Location, or Notes.";
}

CalendarSearchState
CalendarSearchState::Create(const base::Date &visible_month) {
  CalendarSearchState state;
  state.keyword = "";
  state.start_date = base::Date(visible_month.year(), visible_month.month(), 1);
  state.end_date_exclusive = state.start_date.AddMonths(1);
  state.search_title = true;
  state.search_location = true;
  state.search_note = true;
  state.has_applied = false;
  state.applied_repository_version = -1;
  return state;
}

CalendarSearchState
CalendarSearchState::WithKeyword(const std::string &new_keyword) const {
  CalendarSearchState next = *this;
  next.keyword = Trim(new_keyword);
  next.result_event_ids.clear();
  next.has_applied = false;
  return next;
}

CalendarSearchState
CalendarSearchState::WithPeriod(const base::Date &new_start,
                                const base::Date &new_end) const {
  CalendarSearchState next = *this;
  next.start_date = new_start;
  next.end_date_exclusive = new_end;
  next.result_event_ids.clear();
  next.has_applied = false;
  return next;
}

CalendarSearchState CalendarSearchState::WithFields(bool title, bool location,
                                                    bool note) const {
  CalendarSearchState next = *this;
  next.search_title = title;
  next.search_location = location;
  next.search_note = note;
  next.result_event_ids.clear();
  next.has_applied = false;
  return next;
}

CalendarSearchState
CalendarSearchState::Apply(domain::CalendarEventRepository *repository) const {
  if (!CanApply()) {
    CalendarSearchState next = *this;
    next.result_event_ids.clear();
    next.has_applied = false;
    return next;
  }

  // 00:00:00 in +09:00 for simplicity as C# DateBoundary does.
  base::OffsetDateTime start =
      base::OffsetDateTime::FromLocalParts(start_date, 0, 0, 0, 0, 9 * 60);
  base::OffsetDateTime end_exclusive = base::OffsetDateTime::FromLocalParts(
      end_date_exclusive, 0, 0, 0, 0, 9 * 60);

  domain::CalendarSearchCriteria criteria;
  std::string err;
  if (!domain::CalendarSearchCriteria::TryCreate(
          keyword, start, end_exclusive, 100, search_title, search_location,
          search_note, &criteria, &err)) {
    throw std::runtime_error("Invalid criteria: " + err);
  }

  auto snapshot = repository->SearchWithVersion(criteria);
  CalendarSearchState next = *this;
  next.result_event_ids.clear();
  for (const auto &ev : snapshot.events) {
    next.result_event_ids.push_back(ev.id());
  }
  next.has_applied = true;
  next.applied_repository_version = snapshot.repository_version;
  return next;
}

CalendarInteractionState
CalendarInteractionState::Create(const CalendarUiState &calendar) {
  CalendarInteractionState state;
  state.calendar = calendar;
  state.surface = CalendarSurface::kCalendar;
  return state;
}

CalendarInteractionState CalendarInteractionState::OpenNewEvent() const {
  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kEventEditor;
  next.selected_event_id = std::nullopt;
  next.event_editor = CalendarEditorState::CreateNew(calendar.selected_date());
  return next;
}

CalendarInteractionState
CalendarInteractionState::OpenEventDetail(const std::string &event_id) const {
  if (event_id.empty())
    throw std::invalid_argument("Event ID is required.");

  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kEventDetail;
  next.selected_event_id = event_id;
  next.event_editor = std::nullopt;
  return next;
}

CalendarInteractionState CalendarInteractionState::OpenEventEditor(
    const domain::CalendarEvent &event,
    const std::vector<int> &reminder_offsets) const {
  if (surface != CalendarSurface::kEventDetail ||
      selected_event_id.value_or("") != event.id()) {
    throw std::logic_error(
        "Editing requires the selected event detail to be open.");
  }

  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kEventEditor;
  next.event_editor =
      CalendarEditorState::CreateExisting(event, reminder_offsets);
  return next;
}

CalendarInteractionState CalendarInteractionState::RequestEventDelete() const {
  if (surface != CalendarSurface::kEventDetail ||
      !selected_event_id.has_value()) {
    throw std::logic_error("Event deletion requires an open event detail.");
  }
  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kDeleteEventConfirmation;
  return next;
}

CalendarInteractionState CalendarInteractionState::CancelEventDelete() const {
  if (surface != CalendarSurface::kDeleteEventConfirmation ||
      !selected_event_id.has_value()) {
    throw std::logic_error("No event deletion confirmation is open.");
  }
  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kEventDetail;
  return next;
}

CalendarInteractionState CalendarInteractionState::OpenReminderList() const {
  if (surface != CalendarSurface::kCalendar) {
    throw std::logic_error("Reminders can only be opened from the calendar.");
  }
  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kReminderList;
  next.selected_event_id = std::nullopt;
  next.event_editor = std::nullopt;
  next.selected_reminder_id = std::nullopt;
  next.reminder_editor = std::nullopt;
  return next;
}

CalendarInteractionState CalendarInteractionState::OpenNewReminder(
    const base::OffsetDateTime &suggested_due) const {
  if (surface != CalendarSurface::kReminderList) {
    throw std::logic_error(
        "A new reminder requires the reminder list to be open.");
  }
  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kReminderEditor;
  next.selected_reminder_id = std::nullopt;
  next.reminder_editor = CalendarReminderEditorState::CreateNew(suggested_due);
  return next;
}

CalendarInteractionState CalendarInteractionState::OpenReminderEditor(
    const domain::CalendarReminder &reminder) const {
  if (surface != CalendarSurface::kReminderList ||
      reminder.calendar_event_id().has_value()) {
    throw std::logic_error(
        "Only an independent reminder from the reminder list can be edited.");
  }
  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kReminderEditor;
  next.selected_reminder_id = reminder.id();
  next.reminder_editor = CalendarReminderEditorState::CreateExisting(reminder);
  return next;
}

CalendarInteractionState
CalendarInteractionState::RequestReminderDelete() const {
  if (surface != CalendarSurface::kReminderEditor ||
      !selected_reminder_id.has_value()) {
    throw std::logic_error(
        "Reminder deletion requires an existing reminder editor.");
  }
  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kDeleteReminderConfirmation;
  return next;
}

CalendarInteractionState CalendarInteractionState::OpenSearch() const {
  if (surface != CalendarSurface::kCalendar) {
    throw std::logic_error("Search can only be opened from the calendar.");
  }
  CalendarInteractionState next = *this;
  next.calendar = calendar.FocusHeader(CalendarFocusRegion::kSearch);
  next.surface = CalendarSurface::kSearch;
  next.search = CalendarSearchState::Create(calendar.visible_month());
  next.search_return_event_id = std::nullopt;
  next.selected_event_id = std::nullopt;
  return next;
}

CalendarInteractionState
CalendarInteractionState::OpenSearchResult(const std::string &event_id) const {
  if (surface != CalendarSurface::kSearch || !search.has_value()) {
    throw std::logic_error("A result from the active search is required.");
  }
  bool found = false;
  for (const auto &id : search->result_event_ids) {
    if (id == event_id) {
      found = true;
      break;
    }
  }
  if (!found)
    throw std::logic_error("A result from the active search is required.");

  CalendarInteractionState next = *this;
  next.surface = CalendarSurface::kEventDetail;
  next.selected_event_id = event_id;
  next.event_editor = std::nullopt;
  next.search_return_event_id = event_id;
  return next;
}

CalendarInteractionState CalendarInteractionState::Back() const {
  CalendarInteractionState next = *this;
  switch (surface) {
  case CalendarSurface::kSearch:
    next.surface = CalendarSurface::kCalendar;
    next.search = std::nullopt;
    next.search_return_event_id = std::nullopt;
    next.calendar = calendar.FocusHeader(CalendarFocusRegion::kSearch);
    break;
  case CalendarSurface::kDeleteEventConfirmation:
    return CancelEventDelete();
  case CalendarSurface::kEventEditor:
    if (selected_event_id.has_value()) {
      next.surface = CalendarSurface::kEventDetail;
      next.event_editor = std::nullopt;
    } else {
      next.surface = CalendarSurface::kCalendar;
      next.selected_event_id = std::nullopt;
      next.event_editor = std::nullopt;
    }
    break;
  case CalendarSurface::kEventDetail:
    if (search.has_value()) {
      next.surface = CalendarSurface::kSearch;
      next.selected_event_id = std::nullopt;
      next.event_editor = std::nullopt;
    } else {
      next.surface = CalendarSurface::kCalendar;
      next.selected_event_id = std::nullopt;
      next.event_editor = std::nullopt;
    }
    break;
  case CalendarSurface::kDeleteReminderConfirmation:
    next.surface = CalendarSurface::kReminderEditor;
    break;
  case CalendarSurface::kReminderEditor:
    next.surface = CalendarSurface::kReminderList;
    next.selected_reminder_id = std::nullopt;
    next.reminder_editor = std::nullopt;
    break;
  case CalendarSurface::kReminderList:
    next.surface = CalendarSurface::kCalendar;
    next.selected_reminder_id = std::nullopt;
    next.reminder_editor = std::nullopt;
    break;
  default:
    break;
  }
  return next;
}

} // namespace state
} // namespace calendar
