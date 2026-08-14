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

#ifndef CALENDAR_NATIVE_DOMAIN_CALENDAR_EVENT_HH_
#define CALENDAR_NATIVE_DOMAIN_CALENDAR_EVENT_HH_

#include <cstdint>
#include <string>

#include "base/offset_date_time.hh"

namespace calendar {
namespace domain {

// An immutable calendar entry with a stable, caller-supplied identifier. The
// identifier never changes after creation, which is what lets the UI restore
// focus and lets ViewAnnotation name a rendered card.
class CalendarEvent {
 public:
  CalendarEvent() = default;

  // Trims the title, note and location and validates the identity and range
  // exactly as CalendarEvent.Create does in the C# reference. Returns false
  // with a matching message instead of throwing.
  static bool TryCreate(const std::string& id, const std::string& title,
                        const base::OffsetDateTime& start,
                        const base::OffsetDateTime& end,
                        const std::string& note, const std::string& location,
                        CalendarEvent* created, std::string* error);

  const std::string& id() const { return id_; }
  const std::string& title() const { return title_; }
  const base::OffsetDateTime& start() const { return start_; }
  const base::OffsetDateTime& end() const { return end_; }
  const std::string& note() const { return note_; }
  const std::string& location() const { return location_; }

  std::int64_t DurationTicks() const {
    return end_.TicksUtc() - start_.TicksUtc();
  }

  // An all-day entry starts and ends at local midnight and spans at least a
  // full day, matching the reference's IsAllDay predicate.
  bool IsAllDay() const;

  friend bool operator==(const CalendarEvent& left,
                         const CalendarEvent& right) {
    return left.id_ == right.id_ && left.title_ == right.title_ &&
           left.start_ == right.start_ && left.end_ == right.end_ &&
           left.note_ == right.note_ && left.location_ == right.location_;
  }

  friend bool operator!=(const CalendarEvent& left,
                         const CalendarEvent& right) {
    return !(left == right);
  }

 private:
  std::string id_;
  std::string title_;
  base::OffsetDateTime start_;
  base::OffsetDateTime end_;
  std::string note_;
  std::string location_;
};

}  // namespace domain
}  // namespace calendar

#endif  // CALENDAR_NATIVE_DOMAIN_CALENDAR_EVENT_HH_
