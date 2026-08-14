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

#ifndef CALENDAR_NATIVE_STATE_CALENDAR_UI_STATE_HH_
#define CALENDAR_NATIVE_STATE_CALENDAR_UI_STATE_HH_

#include <optional>
#include <string>
#include <vector>
#include <variant>

#include "base/date.hh"

namespace calendar {
namespace state {

enum class CalendarBackResult {
  kCloseAgenda,
  kExitApplication,
};

enum class CalendarFocusRegion {
  kMonthGrid,
  kAgendaEvents,
  kAgendaEmptyState,
  kAgendaAdd,
  kAgendaReminders,
  kPeriodEvents,
  kPeriodEmptyState,
  kToday,
  kPreviousPeriod,
  kNextPeriod,
  kSearch,
  kMonthMode,
  kWeekMode,
  kDayMode,
  kAgendaMode,
};

enum class CalendarViewMode {
  kMonth,
  kWeek,
  kDay,
  kAgenda,
};

struct CalendarMonthCell {
  base::Date date;
  bool is_in_visible_month;
};

class CalendarUiState {
 public:
  CalendarUiState() = default;

  static CalendarUiState Create(const base::Date& selected_date);

  const base::Date& visible_month() const { return visible_month_; }
  const base::Date& selected_date() const { return selected_date_; }
  bool is_agenda_open() const { return is_agenda_open_; }
  CalendarFocusRegion focus_region() const { return focus_region_; }
  std::optional<int> focused_agenda_index() const { return focused_agenda_index_; }
  CalendarViewMode view_mode() const { return view_mode_; }
  const std::optional<std::string>& focused_event_id() const { return focused_event_id_; }

  CalendarUiState MoveDays(int days) const;
  CalendarUiState MovePeriod(int periods) const;
  CalendarUiState ChangeViewMode(CalendarViewMode view_mode) const;
  CalendarUiState FocusHeader(CalendarFocusRegion focus_region) const;
  bool IsHeaderFocused() const;
  CalendarUiState MoveHeaderFocus(int delta) const;

  CalendarUiState EnterAgenda(int event_count) const;
  CalendarUiState MoveAgenda(int delta, int event_count) const;
  CalendarUiState MoveAgendaFocus(int delta, int event_count) const;
  CalendarUiState ReturnToMonth() const;

  CalendarUiState FocusPeriodEvent(const std::string& event_id) const;
  CalendarUiState FocusPeriodEmptyState() const;
  CalendarUiState FocusTodayControl() const;
  CalendarUiState ActivateToday(const base::Date& today) const;
  CalendarUiState WithFocusedEventId(const std::string& event_id) const;

  CalendarUiState OpenAgenda() const;
  CalendarUiState CloseAgenda() const;

  CalendarBackResult HandleBack() const;
  std::vector<CalendarMonthCell> BuildMonthCells() const;

 private:
  static bool IsHeaderRegion(CalendarFocusRegion focus_region);

  base::Date visible_month_ = base::Date(1, 1, 1);
  base::Date selected_date_ = base::Date(1, 1, 1);
  bool is_agenda_open_ = false;
  CalendarFocusRegion focus_region_ = CalendarFocusRegion::kMonthGrid;
  std::optional<int> focused_agenda_index_;
  CalendarViewMode view_mode_ = CalendarViewMode::kMonth;
  std::optional<std::string> focused_event_id_;
};

struct CalendarUiCommand {
  struct SelectDate { base::Date date; };
  struct SelectAgendaEvent { int index; };
  struct ActivateToday {};
  struct ShowPreviousPeriod {};
  struct ShowNextPeriod {};
  struct ChangeViewMode { CalendarViewMode view_mode; };
  struct OpenEvent { std::string event_id; };

  std::variant<SelectDate, SelectAgendaEvent, ActivateToday, ShowPreviousPeriod,
               ShowNextPeriod, ChangeViewMode, OpenEvent> value;
};

class CalendarUiReducer {
 public:
  static CalendarUiState Reduce(const CalendarUiState& state,
                                const CalendarUiCommand& command,
                                const base::Date& today,
                                int selected_date_event_count);
};

}  // namespace state
}  // namespace calendar

#endif  // CALENDAR_NATIVE_STATE_CALENDAR_UI_STATE_HH_
