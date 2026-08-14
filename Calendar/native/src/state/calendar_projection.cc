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

#include "state/calendar_projection.hh"

#include <array>

#include "domain/calendar_date_boundary.hh"

namespace calendar {
namespace state {
namespace {

const std::array<const char *, 12> kMonthNames = {
    "January", "February", "March",     "April",   "May",      "June",
    "July",    "August",   "September", "October", "November", "December"};

const std::array<const char *, 12> kShortMonthNames = {
    "Jan", "Feb", "Mar", "Apr", "May", "Jun",
    "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"};

std::string MonthYear(const base::Date &date) {
  return std::string(kMonthNames[date.month() - 1]) + " " +
         std::to_string(date.year());
}

std::string ShortDate(const base::Date &date) {
  return std::string(kShortMonthNames[date.month() - 1]) + " " +
         std::to_string(date.day());
}

std::vector<domain::CalendarEvent>
EventsForDay(const base::Date &date,
             domain::CalendarEventRepository *repository) {
  std::vector<domain::CalendarEvent> events;
  repository->TryGetEventsOverlapping(
      domain::CalendarDateBoundary::AtStartOfDay(date),
      domain::CalendarDateBoundary::AtStartOfDay(date.AddDays(1)), &events);
  return events;
}

} // namespace

CalendarProjection
CalendarProjection::Create(const CalendarUiState &state,
                           domain::CalendarEventRepository *repository) {
  CalendarProjection projection;
  projection.focused_event_id = state.focused_event_id();

  if (state.view_mode() == CalendarViewMode::kWeek) {
    projection.start_date =
        state.selected_date().AddDays(-state.selected_date().DayOfWeek());
    projection.end_date_exclusive = projection.start_date.AddDays(7);
    projection.title =
        ShortDate(projection.start_date) + " - " +
        ShortDate(projection.end_date_exclusive.AddDays(-1)) + ", " +
        std::to_string(projection.end_date_exclusive.AddDays(-1).year());
    for (int day = 0; day < 7; ++day) {
      const base::Date date = projection.start_date.AddDays(day);
      projection.event_groups.push_back({date, EventsForDay(date, repository)});
    }
    return projection;
  }

  if (state.view_mode() == CalendarViewMode::kDay) {
    projection.start_date = state.selected_date();
    projection.end_date_exclusive = projection.start_date.AddDays(1);
    projection.title = ShortDate(projection.start_date) + ", " +
                       std::to_string(projection.start_date.year());
    projection.event_groups.push_back(
        {projection.start_date,
         EventsForDay(projection.start_date, repository)});
    return projection;
  }

  projection.start_date = state.visible_month();
  projection.end_date_exclusive = projection.start_date.AddMonths(1);
  projection.title = MonthYear(projection.start_date);
  if (state.view_mode() == CalendarViewMode::kAgenda) {
    projection.title += " agenda";
    for (base::Date date = projection.start_date;
         date < projection.end_date_exclusive; date = date.AddDays(1)) {
      auto events = EventsForDay(date, repository);
      if (!events.empty()) {
        projection.event_groups.push_back({date, std::move(events)});
      }
    }
  } else {
    projection.event_groups.push_back(
        {state.selected_date(),
         EventsForDay(state.selected_date(), repository)});
  }
  return projection;
}

} // namespace state
} // namespace calendar
