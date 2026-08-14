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

#ifndef CALENDAR_NATIVE_DOMAIN_CALENDAR_REMINDER_HH_
#define CALENDAR_NATIVE_DOMAIN_CALENDAR_REMINDER_HH_

#include <optional>
#include <string>
#include <vector>

#include "base/offset_date_time.hh"

namespace calendar {
namespace domain {

// An app-owned immutable reminder. Independent reminders and event-linked
// reminders share this shape; only event-linked reminders carry
// calendar_event_id() and offset_minutes().
class CalendarReminder {
 public:
  CalendarReminder() = default;

  // The reminder offsets an event editor may attach: 10 minutes, 30 minutes,
  // 1 hour, and 1 day.
  static const std::vector<int>& AllowedOffsetMinutes();
  static bool IsAllowedOffset(int offset_minutes);

  static bool TryCreate(const std::string& id, const std::string& title,
                        const base::OffsetDateTime& due_at,
                        const std::string& note, CalendarReminder* created,
                        std::string* error);

  static bool TryCreateForEvent(const std::string& id,
                                const std::string& title,
                                const base::OffsetDateTime& event_start,
                                const std::string& calendar_event_id,
                                int offset_minutes, const std::string& note,
                                CalendarReminder* created, std::string* error);

  const std::string& id() const { return id_; }
  const std::string& title() const { return title_; }
  const base::OffsetDateTime& due_at() const { return due_at_; }
  const std::string& note() const { return note_; }
  bool is_completed() const { return is_completed_; }

  const std::optional<std::string>& calendar_event_id() const {
    return calendar_event_id_;
  }

  const std::optional<int>& offset_minutes() const { return offset_minutes_; }
  const std::optional<int>& alarm_id() const { return alarm_id_; }

  bool IsLinkedToEvent() const { return calendar_event_id_.has_value(); }

  // Non-mutating "with" helpers mirroring the C# record's `with` expressions.
  CalendarReminder WithAlarmId(std::optional<int> alarm_id) const;
  CalendarReminder WithCompleted(bool is_completed) const;
  CalendarReminder WithDueAt(const base::OffsetDateTime& due_at) const;

  friend bool operator==(const CalendarReminder& left,
                         const CalendarReminder& right) {
    return left.id_ == right.id_ && left.title_ == right.title_ &&
           left.due_at_ == right.due_at_ && left.note_ == right.note_ &&
           left.is_completed_ == right.is_completed_ &&
           left.calendar_event_id_ == right.calendar_event_id_ &&
           left.offset_minutes_ == right.offset_minutes_ &&
           left.alarm_id_ == right.alarm_id_;
  }

  friend bool operator!=(const CalendarReminder& left,
                         const CalendarReminder& right) {
    return !(left == right);
  }

 private:
  std::string id_;
  std::string title_;
  base::OffsetDateTime due_at_;
  std::string note_;
  bool is_completed_ = false;
  std::optional<std::string> calendar_event_id_;
  std::optional<int> offset_minutes_;
  std::optional<int> alarm_id_;
};

}  // namespace domain
}  // namespace calendar

#endif  // CALENDAR_NATIVE_DOMAIN_CALENDAR_REMINDER_HH_
