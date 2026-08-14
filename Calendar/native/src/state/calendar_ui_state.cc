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

#include "state/calendar_ui_state.hh"

#include <algorithm>

namespace calendar {
namespace state {

CalendarUiState CalendarUiState::Create(const base::Date& selected_date) {
  CalendarUiState state;
  state.visible_month_ = base::Date(selected_date.year(), selected_date.month(), 1);
  state.selected_date_ = selected_date;
  state.is_agenda_open_ = false;
  state.focus_region_ = CalendarFocusRegion::kMonthGrid;
  state.focused_agenda_index_ = std::nullopt;
  return state;
}

CalendarUiState CalendarUiState::MoveDays(int days) const {
  CalendarUiState next = *this;
  next.selected_date_ = selected_date_.AddDays(days);
  next.visible_month_ = base::Date(next.selected_date_.year(), next.selected_date_.month(), 1);
  next.is_agenda_open_ = false;
  next.focus_region_ = CalendarFocusRegion::kMonthGrid;
  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = std::nullopt;
  return next;
}

CalendarUiState CalendarUiState::MovePeriod(int periods) const {
  if (periods == 0) return *this;

  CalendarUiState next = *this;
  if (view_mode_ == CalendarViewMode::kMonth || view_mode_ == CalendarViewMode::kAgenda) {
    base::Date target_month = visible_month_.AddMonths(periods);
    int target_day = std::min(selected_date_.day(), base::Date::DaysInMonth(target_month.year(), target_month.month()));
    next.selected_date_ = base::Date(target_month.year(), target_month.month(), target_day);
  } else {
    next.selected_date_ = selected_date_.AddDays(view_mode_ == CalendarViewMode::kWeek ? periods * 7 : periods);
  }

  CalendarFocusRegion content_anchor = (view_mode_ == CalendarViewMode::kMonth)
      ? CalendarFocusRegion::kMonthGrid
      : CalendarFocusRegion::kPeriodEmptyState;

  next.visible_month_ = base::Date(next.selected_date_.year(), next.selected_date_.month(), 1);
  next.is_agenda_open_ = false;
  next.focus_region_ = IsHeaderFocused() ? focus_region_ : content_anchor;
  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = std::nullopt;
  return next;
}

CalendarUiState CalendarUiState::ChangeViewMode(CalendarViewMode view_mode) const {
  CalendarUiState next = *this;
  next.view_mode_ = view_mode;
  next.visible_month_ = base::Date(selected_date_.year(), selected_date_.month(), 1);
  next.is_agenda_open_ = false;

  switch (view_mode) {
    case CalendarViewMode::kMonth: next.focus_region_ = CalendarFocusRegion::kMonthMode; break;
    case CalendarViewMode::kWeek: next.focus_region_ = CalendarFocusRegion::kWeekMode; break;
    case CalendarViewMode::kDay: next.focus_region_ = CalendarFocusRegion::kDayMode; break;
    case CalendarViewMode::kAgenda: next.focus_region_ = CalendarFocusRegion::kAgendaMode; break;
    default: next.focus_region_ = CalendarFocusRegion::kMonthGrid; break;
  }

  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = std::nullopt;
  return next;
}

CalendarUiState CalendarUiState::FocusHeader(CalendarFocusRegion focus_region) const {
  if (!IsHeaderRegion(focus_region)) return *this;

  CalendarUiState next = *this;
  next.is_agenda_open_ = false;
  next.focus_region_ = focus_region;
  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = std::nullopt;
  return next;
}

bool CalendarUiState::IsHeaderFocused() const {
  return IsHeaderRegion(focus_region_);
}

CalendarUiState CalendarUiState::MoveHeaderFocus(int delta) const {
  if (!IsHeaderFocused() || delta == 0) return *this;

  CalendarFocusRegion regions[] = {
      CalendarFocusRegion::kPreviousPeriod,
      CalendarFocusRegion::kToday,
      CalendarFocusRegion::kNextPeriod,
      CalendarFocusRegion::kMonthMode,
      CalendarFocusRegion::kWeekMode,
      CalendarFocusRegion::kDayMode,
      CalendarFocusRegion::kAgendaMode,
      CalendarFocusRegion::kSearch,
  };

  int current = 0;
  int count = sizeof(regions) / sizeof(regions[0]);
  for (int i = 0; i < count; ++i) {
    if (regions[i] == focus_region_) {
      current = i;
      break;
    }
  }

  int target = std::clamp(current + delta, 0, count - 1);
  return FocusHeader(regions[target]);
}

bool CalendarUiState::IsHeaderRegion(CalendarFocusRegion focus_region) {
  return focus_region == CalendarFocusRegion::kPreviousPeriod ||
         focus_region == CalendarFocusRegion::kToday ||
         focus_region == CalendarFocusRegion::kNextPeriod ||
         focus_region == CalendarFocusRegion::kSearch ||
         focus_region == CalendarFocusRegion::kMonthMode ||
         focus_region == CalendarFocusRegion::kWeekMode ||
         focus_region == CalendarFocusRegion::kDayMode ||
         focus_region == CalendarFocusRegion::kAgendaMode;
}

CalendarUiState CalendarUiState::EnterAgenda(int event_count) const {
  if (event_count < 0) return *this;

  CalendarUiState next = *this;
  next.is_agenda_open_ = true;
  next.focus_region_ = (event_count == 0) ? CalendarFocusRegion::kAgendaEmptyState : CalendarFocusRegion::kAgendaEvents;
  if (event_count == 0) {
    next.focused_agenda_index_ = std::nullopt;
  } else {
    next.focused_agenda_index_ = 0;
  }
  next.focused_event_id_ = std::nullopt;
  return next;
}

CalendarUiState CalendarUiState::MoveAgenda(int delta, int event_count) const {
  if (event_count <= 0 || focus_region_ != CalendarFocusRegion::kAgendaEvents) return *this;

  int current_index = focused_agenda_index_.value_or(0);
  CalendarUiState next = *this;
  next.focused_agenda_index_ = std::clamp(current_index + delta, 0, event_count - 1);
  return next;
}

CalendarUiState CalendarUiState::MoveAgendaFocus(int delta, int event_count) const {
  if (event_count < 0) return *this;

  if (focus_region_ == CalendarFocusRegion::kAgendaAdd) {
    CalendarUiState next = *this;
    if (delta >= 0) {
      next.focus_region_ = CalendarFocusRegion::kAgendaReminders;
      next.focused_agenda_index_ = std::nullopt;
      return next;
    }
    next.focus_region_ = (event_count == 0) ? CalendarFocusRegion::kAgendaEmptyState : CalendarFocusRegion::kAgendaEvents;
    if (event_count == 0) next.focused_agenda_index_ = std::nullopt;
    else next.focused_agenda_index_ = event_count - 1;
    return next;
  }

  if (focus_region_ == CalendarFocusRegion::kAgendaReminders) {
    if (delta < 0) {
      CalendarUiState next = *this;
      next.focus_region_ = CalendarFocusRegion::kAgendaAdd;
      next.focused_agenda_index_ = std::nullopt;
      return next;
    }
    return *this;
  }

  if (delta > 0 && (focus_region_ == CalendarFocusRegion::kAgendaEmptyState ||
                    (focus_region_ == CalendarFocusRegion::kAgendaEvents &&
                     focused_agenda_index_.value_or(0) == event_count - 1))) {
    CalendarUiState next = *this;
    next.focus_region_ = CalendarFocusRegion::kAgendaAdd;
    next.focused_agenda_index_ = std::nullopt;
    return next;
  }

  return MoveAgenda(delta, event_count);
}

CalendarUiState CalendarUiState::ReturnToMonth() const {
  CalendarUiState next = *this;
  next.is_agenda_open_ = false;
  next.focus_region_ = CalendarFocusRegion::kMonthGrid;
  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = std::nullopt;
  return next;
}

CalendarUiState CalendarUiState::FocusPeriodEvent(const std::string& event_id) const {
  if (event_id.empty()) return *this;
  CalendarUiState next = *this;
  next.is_agenda_open_ = false;
  next.focus_region_ = CalendarFocusRegion::kPeriodEvents;
  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = event_id;
  return next;
}

CalendarUiState CalendarUiState::FocusPeriodEmptyState() const {
  CalendarUiState next = *this;
  next.is_agenda_open_ = false;
  next.focus_region_ = CalendarFocusRegion::kPeriodEmptyState;
  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = std::nullopt;
  return next;
}

CalendarUiState CalendarUiState::FocusTodayControl() const {
  CalendarUiState next = *this;
  next.is_agenda_open_ = false;
  next.focus_region_ = CalendarFocusRegion::kToday;
  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = std::nullopt;
  return next;
}

CalendarUiState CalendarUiState::ActivateToday(const base::Date& today) const {
  CalendarUiState next = *this;
  next.visible_month_ = base::Date(today.year(), today.month(), 1);
  next.selected_date_ = today;
  next.is_agenda_open_ = false;
  next.focus_region_ = CalendarFocusRegion::kMonthGrid;
  next.focused_agenda_index_ = std::nullopt;
  next.focused_event_id_ = std::nullopt;
  return next;
}

CalendarUiState CalendarUiState::WithFocusedEventId(const std::string& event_id) const {
  CalendarUiState next = *this;
  next.focused_event_id_ = event_id;
  return next;
}

CalendarUiState CalendarUiState::OpenAgenda() const {
  return EnterAgenda(0);
}

CalendarUiState CalendarUiState::CloseAgenda() const {
  return ReturnToMonth();
}

CalendarBackResult CalendarUiState::HandleBack() const {
  if (focus_region_ == CalendarFocusRegion::kAgendaEvents ||
      focus_region_ == CalendarFocusRegion::kAgendaEmptyState ||
      focus_region_ == CalendarFocusRegion::kAgendaAdd ||
      focus_region_ == CalendarFocusRegion::kAgendaReminders) {
    return CalendarBackResult::kCloseAgenda;
  }
  return CalendarBackResult::kExitApplication;
}

std::vector<CalendarMonthCell> CalendarUiState::BuildMonthCells() const {
  base::Date grid_start = visible_month_.AddDays(-visible_month_.DayOfWeek());
  std::vector<CalendarMonthCell> cells;
  cells.reserve(42);
  for (int i = 0; i < 42; ++i) {
    base::Date date = grid_start.AddDays(i);
    bool is_in = (date.month() == visible_month_.month() && date.year() == visible_month_.year());
    cells.push_back({date, is_in});
  }
  return cells;
}

// ----------------------------------------------------------------------------
// Reducer
// ----------------------------------------------------------------------------

struct ReducerVisitor {
  CalendarUiState state;
  base::Date today;
  int event_count;

  CalendarUiState operator()(const CalendarUiCommand::SelectDate& cmd) const {
    CalendarUiState next = state;
    next = next.ActivateToday(cmd.date); // just use fields manually
    // Wait, ActivateToday sets it to Today control, but we want MonthGrid
    CalendarUiState res = state;
    // from C#: state with { SelectedDate = selectDate.Date, VisibleMonth = ..., IsAgendaOpen = false, FocusRegion = MonthGrid, FocusedAgendaIndex = null }
    // Using ActivateToday gets close, but FocusRegion is MonthGrid in ActivateToday, so it's actually correct.
    res = res.ActivateToday(cmd.date);
    return res;
  }

  CalendarUiState operator()(const CalendarUiCommand::SelectAgendaEvent& cmd) const {
    if (event_count > 0 && cmd.index >= 0 && cmd.index < event_count) {
      CalendarUiState next = state.EnterAgenda(event_count);
      // Hack to set focused_agenda_index
      // EnterAgenda returns a new state, we can move the index
      next = next.MoveAgenda(cmd.index - next.focused_agenda_index().value_or(0), event_count);
      return next;
    }
    return state;
  }

  CalendarUiState operator()(const CalendarUiCommand::ActivateToday&) const {
    return state.ActivateToday(today).FocusHeader(CalendarFocusRegion::kToday);
  }

  CalendarUiState operator()(const CalendarUiCommand::ShowPreviousPeriod&) const {
    return state.FocusHeader(CalendarFocusRegion::kPreviousPeriod).MovePeriod(-1);
  }

  CalendarUiState operator()(const CalendarUiCommand::ShowNextPeriod&) const {
    return state.FocusHeader(CalendarFocusRegion::kNextPeriod).MovePeriod(1);
  }

  CalendarUiState operator()(const CalendarUiCommand::ChangeViewMode& cmd) const {
    return state.ChangeViewMode(cmd.view_mode);
  }

  CalendarUiState operator()(const CalendarUiCommand::OpenEvent& cmd) const {
    if (state.view_mode() == CalendarViewMode::kMonth) {
      return state.WithFocusedEventId(cmd.event_id);
    }
    return state.FocusPeriodEvent(cmd.event_id);
  }
};

CalendarUiState CalendarUiReducer::Reduce(const CalendarUiState& state,
                                          const CalendarUiCommand& command,
                                          const base::Date& today,
                                          int selected_date_event_count) {
  ReducerVisitor visitor{state, today, selected_date_event_count};
  return std::visit(visitor, command.value);
}

}  // namespace state
}  // namespace calendar
