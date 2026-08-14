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

#include "domain/calendar_event.hh"

#include "base/strings.hh"

namespace calendar {
namespace domain {

bool CalendarEvent::TryCreate(const std::string& id, const std::string& title,
                              const base::OffsetDateTime& start,
                              const base::OffsetDateTime& end,
                              const std::string& note,
                              const std::string& location,
                              CalendarEvent* created, std::string* error) {
  if (base::IsBlank(id)) {
    *error = "An event ID is required.";
    return false;
  }
  if (base::IsBlank(title)) {
    *error = "An event title is required.";
    return false;
  }
  if (end <= start) {
    *error = "An event must end after it starts.";
    return false;
  }

  CalendarEvent candidate;
  candidate.id_ = id;
  candidate.title_ = base::Trim(title);
  candidate.start_ = start;
  candidate.end_ = end;
  candidate.note_ = base::Trim(note);
  candidate.location_ = base::Trim(location);
  *created = candidate;
  error->clear();
  return true;
}

bool CalendarEvent::IsAllDay() const {
  return start_.LocalTimeOfDayTicks() == 0 && end_.LocalTimeOfDayTicks() == 0 &&
         DurationTicks() >= base::kTicksPerDay;
}

}  // namespace domain
}  // namespace calendar
