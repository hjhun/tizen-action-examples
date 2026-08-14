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

#ifndef CALENDAR_NATIVE_STATE_CALENDAR_PROJECTION_HH_
#define CALENDAR_NATIVE_STATE_CALENDAR_PROJECTION_HH_

#include <optional>
#include <string>
#include <vector>

#include "base/date.hh"
#include "domain/calendar_event.hh"
#include "domain/calendar_event_repository.hh"
#include "state/calendar_ui_state.hh"

namespace calendar {
namespace state {

struct CalendarEventGroup {
  base::Date date;
  std::vector<domain::CalendarEvent> events;
};

struct CalendarProjection {
  std::string title;
  base::Date start_date;
  base::Date end_date_exclusive;
  std::vector<CalendarEventGroup> event_groups;
  std::optional<std::string> focused_event_id;

  static CalendarProjection Create(const CalendarUiState &state,
                                   domain::CalendarEventRepository *repository);
};

} // namespace state
} // namespace calendar

#endif // CALENDAR_NATIVE_STATE_CALENDAR_PROJECTION_HH_
