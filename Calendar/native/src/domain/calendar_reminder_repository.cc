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

#include "domain/calendar_reminder_repository.hh"

#include <algorithm>

#include "base/strings.hh"

namespace calendar {
namespace domain {
namespace {

// Open reminders first, then soonest due, then ordinal identifier.
bool IsBeforeInDisplayOrder(const CalendarReminder& left,
                            const CalendarReminder& right) {
  if (left.is_completed() != right.is_completed()) {
    return !left.is_completed();
  }
  if (left.due_at() != right.due_at()) return left.due_at() < right.due_at();
  return left.id() < right.id();
}

bool Matches(const CalendarReminder& reminder, const std::string& term) {
  return base::ContainsIgnoreCase(reminder.title(), term) ||
         base::ContainsIgnoreCase(reminder.note(), term);
}

}  // namespace

bool CalendarReminderRepository::TryCreate(
    const std::vector<CalendarReminder>& reminders,
    CalendarReminderRepository* repository, std::string* error) {
  std::map<std::string, CalendarReminder> reminders_by_id;
  for (const CalendarReminder& reminder : reminders) {
    if (!reminders_by_id.emplace(reminder.id(), reminder).second) {
      *error = "Duplicate reminder ID: " + reminder.id();
      return false;
    }
  }

  std::lock_guard<std::mutex> guard(repository->reminders_mutex_);
  repository->reminders_by_id_ = std::move(reminders_by_id);
  error->clear();
  return true;
}

std::optional<CalendarReminder> CalendarReminderRepository::Find(
    const std::string& id) const {
  if (base::IsBlank(id)) return std::nullopt;
  std::lock_guard<std::mutex> guard(reminders_mutex_);
  const auto found = reminders_by_id_.find(id);
  if (found == reminders_by_id_.end()) return std::nullopt;
  return found->second;
}

bool CalendarReminderRepository::TryAdd(const CalendarReminder& reminder) {
  std::lock_guard<std::mutex> guard(reminders_mutex_);
  return reminders_by_id_.emplace(reminder.id(), reminder).second;
}

bool CalendarReminderRepository::TryUpdate(const CalendarReminder& reminder) {
  std::lock_guard<std::mutex> guard(reminders_mutex_);
  const auto existing = reminders_by_id_.find(reminder.id());
  if (existing == reminders_by_id_.end()) return false;
  existing->second = reminder;
  return true;
}

bool CalendarReminderRepository::TryDelete(const std::string& id) {
  if (base::IsBlank(id)) return false;
  std::lock_guard<std::mutex> guard(reminders_mutex_);
  return reminders_by_id_.erase(id) != 0;
}

bool CalendarReminderRepository::TrySetCompletedLocked(const std::string& id,
                                                       bool is_completed) {
  const auto existing = reminders_by_id_.find(id);
  if (existing == reminders_by_id_.end()) return false;
  existing->second =
      existing->second.WithCompleted(is_completed).WithAlarmId(std::nullopt);
  return true;
}

bool CalendarReminderRepository::TryComplete(const std::string& id) {
  if (base::IsBlank(id)) return false;
  std::lock_guard<std::mutex> guard(reminders_mutex_);
  return TrySetCompletedLocked(id, /*is_completed=*/true);
}

bool CalendarReminderRepository::TryReopen(const std::string& id) {
  if (base::IsBlank(id)) return false;
  std::lock_guard<std::mutex> guard(reminders_mutex_);
  return TrySetCompletedLocked(id, /*is_completed=*/false);
}

std::vector<CalendarReminder> CalendarReminderRepository::OrderedLocked()
    const {
  std::vector<CalendarReminder> ordered;
  ordered.reserve(reminders_by_id_.size());
  for (const auto& entry : reminders_by_id_) ordered.push_back(entry.second);
  std::stable_sort(ordered.begin(), ordered.end(), IsBeforeInDisplayOrder);
  return ordered;
}

std::vector<CalendarReminder> CalendarReminderRepository::FindByCalendarEventId(
    const std::string& calendar_event_id) const {
  if (base::IsBlank(calendar_event_id)) return {};

  std::lock_guard<std::mutex> guard(reminders_mutex_);
  std::vector<CalendarReminder> linked;
  for (const CalendarReminder& reminder : OrderedLocked()) {
    if (reminder.calendar_event_id().has_value() &&
        *reminder.calendar_event_id() == calendar_event_id) {
      linked.push_back(reminder);
    }
  }
  return linked;
}

std::vector<CalendarReminder> CalendarReminderRepository::Search(
    const std::string& term) const {
  const std::string trimmed = base::Trim(term);

  std::lock_guard<std::mutex> guard(reminders_mutex_);
  std::vector<CalendarReminder> matches;
  for (const CalendarReminder& reminder : OrderedLocked()) {
    if (trimmed.empty() || Matches(reminder, trimmed)) {
      matches.push_back(reminder);
    }
  }
  return matches;
}

std::vector<CalendarReminder> CalendarReminderRepository::Snapshot() const {
  std::lock_guard<std::mutex> guard(reminders_mutex_);
  return OrderedLocked();
}

void CalendarReminderRepository::ReplaceAll(
    const std::vector<CalendarReminder>& reminders) {
  std::map<std::string, CalendarReminder> replacement;
  for (const CalendarReminder& reminder : reminders) {
    replacement.emplace(reminder.id(), reminder);
  }

  std::lock_guard<std::mutex> guard(reminders_mutex_);
  reminders_by_id_ = std::move(replacement);
}

}  // namespace domain
}  // namespace calendar
