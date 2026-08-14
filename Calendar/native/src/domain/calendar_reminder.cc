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

#include "domain/calendar_reminder.hh"

#include <algorithm>

#include "base/strings.hh"

namespace calendar {
namespace domain {

const std::vector<int>& CalendarReminder::AllowedOffsetMinutes() {
  static const std::vector<int> kAllowed = {10, 30, 60, 1440};
  return kAllowed;
}

bool CalendarReminder::IsAllowedOffset(int offset_minutes) {
  const std::vector<int>& allowed = AllowedOffsetMinutes();
  return std::find(allowed.begin(), allowed.end(), offset_minutes) !=
         allowed.end();
}

bool CalendarReminder::TryCreate(const std::string& id,
                                 const std::string& title,
                                 const base::OffsetDateTime& due_at,
                                 const std::string& note,
                                 CalendarReminder* created,
                                 std::string* error) {
  if (base::IsBlank(id)) {
    *error = "A reminder ID is required.";
    return false;
  }
  if (base::IsBlank(title)) {
    *error = "A reminder title is required.";
    return false;
  }

  CalendarReminder candidate;
  candidate.id_ = id;
  candidate.title_ = base::Trim(title);
  candidate.due_at_ = due_at;
  candidate.note_ = base::Trim(note);
  *created = candidate;
  error->clear();
  return true;
}

bool CalendarReminder::TryCreateForEvent(
    const std::string& id, const std::string& title,
    const base::OffsetDateTime& event_start,
    const std::string& calendar_event_id, int offset_minutes,
    const std::string& note, CalendarReminder* created, std::string* error) {
  if (base::IsBlank(calendar_event_id)) {
    *error = "A linked calendar event ID is required.";
    return false;
  }
  if (!IsAllowedOffset(offset_minutes)) {
    *error = "An event-linked reminder offset must be one of " +
             base::JoinInts(AllowedOffsetMinutes(), ", ") + " minutes.";
    return false;
  }

  CalendarReminder candidate;
  if (!TryCreate(id, title, event_start.AddMinutes(-offset_minutes), note,
                 &candidate, error)) {
    return false;
  }

  candidate.calendar_event_id_ = calendar_event_id;
  candidate.offset_minutes_ = offset_minutes;
  *created = candidate;
  return true;
}

CalendarReminder CalendarReminder::WithAlarmId(
    std::optional<int> alarm_id) const {
  CalendarReminder copy = *this;
  copy.alarm_id_ = alarm_id;
  return copy;
}

CalendarReminder CalendarReminder::WithCompleted(bool is_completed) const {
  CalendarReminder copy = *this;
  copy.is_completed_ = is_completed;
  return copy;
}

CalendarReminder CalendarReminder::WithDueAt(
    const base::OffsetDateTime& due_at) const {
  CalendarReminder copy = *this;
  copy.due_at_ = due_at;
  return copy;
}

}  // namespace domain
}  // namespace calendar
