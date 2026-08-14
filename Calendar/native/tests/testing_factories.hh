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

#ifndef CALENDAR_NATIVE_TESTS_TESTING_FACTORIES_HH_
#define CALENDAR_NATIVE_TESTS_TESTING_FACTORIES_HH_

#include <string>
#include <vector>

#include "base/date.hh"
#include "base/offset_date_time.hh"
#include "domain/calendar_event.hh"
#include "domain/calendar_reminder.hh"
#include "harness.hh"

namespace calendar {
namespace testing {

// Every fixture uses a fixed +09:00 offset so a host running in any zone
// produces identical instants and identical round-trip text.
constexpr int kFixtureOffsetMinutes = 540;

inline base::OffsetDateTime At(int year, int month, int day, int hour,
                               int minute) {
  return base::OffsetDateTime::FromLocalParts(base::Date(year, month, day),
                                              hour, minute, 0, 0,
                                              kFixtureOffsetMinutes);
}

// Builds an event or fails the current case; fixtures are never invalid, so a
// failure here is a bug in the test rather than an expected outcome.
inline domain::CalendarEvent MakeEvent(const std::string& id,
                                       const std::string& title,
                                       const base::OffsetDateTime& start,
                                       const base::OffsetDateTime& end,
                                       const std::string& note = "",
                                       const std::string& location = "") {
  domain::CalendarEvent created;
  std::string error;
  if (!domain::CalendarEvent::TryCreate(id, title, start, end, note, location,
                                        &created, &error)) {
    FAIL_TEST("fixture event '" + id + "' is invalid: " + error);
  }
  return created;
}

inline domain::CalendarEvent MakeEvent(const std::string& id,
                                       const std::string& title,
                                       const base::OffsetDateTime& start) {
  return MakeEvent(id, title, start, start.AddHours(1));
}

inline domain::CalendarReminder MakeReminder(
    const std::string& id, const std::string& title,
    const base::OffsetDateTime& due_at, const std::string& note = "") {
  domain::CalendarReminder created;
  std::string error;
  if (!domain::CalendarReminder::TryCreate(id, title, due_at, note, &created,
                                           &error)) {
    FAIL_TEST("fixture reminder '" + id + "' is invalid: " + error);
  }
  return created;
}

// Comma-joined identifiers, which makes an ordering expectation readable as a
// single string comparison.
template <typename Item>
std::string Ids(const std::vector<Item>& items) {
  std::string joined;
  for (std::size_t index = 0; index < items.size(); ++index) {
    if (index != 0) joined += ",";
    joined += items[index].id();
  }
  return joined;
}

inline std::string Ids(const std::vector<std::string>& ids) {
  std::string joined;
  for (std::size_t index = 0; index < ids.size(); ++index) {
    if (index != 0) joined += ",";
    joined += ids[index];
  }
  return joined;
}

}  // namespace testing
}  // namespace calendar

#endif  // CALENDAR_NATIVE_TESTS_TESTING_FACTORIES_HH_
