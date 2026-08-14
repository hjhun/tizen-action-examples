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

#ifndef CALENDAR_NATIVE_DOMAIN_CALENDAR_REMINDER_REPOSITORY_HH_
#define CALENDAR_NATIVE_DOMAIN_CALENDAR_REMINDER_REPOSITORY_HH_

#include <map>
#include <mutex>
#include <optional>
#include <string>
#include <vector>

#include "domain/calendar_reminder.hh"

namespace calendar {
namespace domain {

// Thread-safe store for independent and event-linked reminders. UI callbacks
// and Action provider calls can arrive on different threads, so every read and
// mutation is taken under one lock and every returned collection is a copy.
class CalendarReminderRepository {
 public:
  CalendarReminderRepository() = default;

  CalendarReminderRepository(const CalendarReminderRepository&) = delete;
  CalendarReminderRepository& operator=(const CalendarReminderRepository&) =
      delete;

  static bool TryCreate(const std::vector<CalendarReminder>& reminders,
                        CalendarReminderRepository* repository,
                        std::string* error);

  // Returns a copy, or nullopt when the identifier is blank or absent.
  std::optional<CalendarReminder> Find(const std::string& id) const;

  bool TryAdd(const CalendarReminder& reminder);
  bool TryUpdate(const CalendarReminder& reminder);
  bool TryDelete(const std::string& id);

  // Marks a reminder complete or open. Completing drops the alarm identifier
  // because the corresponding alarm is cancelled.
  bool TryComplete(const std::string& id);
  bool TryReopen(const std::string& id);

  std::vector<CalendarReminder> FindByCalendarEventId(
      const std::string& calendar_event_id) const;

  std::vector<CalendarReminder> Search(const std::string& term) const;

  // Ordered open-first, then by due instant, then by ordinal identifier.
  std::vector<CalendarReminder> Snapshot() const;

  void ReplaceAll(const std::vector<CalendarReminder>& reminders);

 private:
  // Callers must already hold reminders_mutex_.
  std::vector<CalendarReminder> OrderedLocked() const;
  bool TrySetCompletedLocked(const std::string& id, bool is_completed);

  mutable std::mutex reminders_mutex_;
  std::map<std::string, CalendarReminder> reminders_by_id_;
};

}  // namespace domain
}  // namespace calendar

#endif  // CALENDAR_NATIVE_DOMAIN_CALENDAR_REMINDER_REPOSITORY_HH_
