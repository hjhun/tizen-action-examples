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

#include "ui/calendar_app.hh"

#include <app_alarm.h>
#include <app_control.h>
#include <dlog.h>
#include <efl_extension.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <limits>
#include <sstream>

#include "domain/calendar_date_boundary.hh"

namespace calendar {
namespace ui {

namespace calendar_rpc = rpc_port::calendar_action_provider;
namespace schedule_rpc = rpc_port::schedule_action_provider;
namespace view_rpc = rpc_port::view_action_provider;

namespace {

constexpr char kAppId[] = "org.tizen.actionexamples.calendar";
constexpr char kAlarmReminderId[] = "calendar.reminder.id";
constexpr char kAlarmReminderTitle[] = "calendar.reminder.title";

const std::array<const char *, 7> kWeekdays = {
    "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"};
const std::array<const char *, 12> kMonths = {
    "January", "February", "March",     "April",   "May",      "June",
    "July",    "August",   "September", "October", "November", "December"};

std::string EntryText(Evas_Object *entry) {
  if (entry == nullptr)
    return "";
  const char *text = elm_object_text_get(entry);
  if (text == nullptr)
    return "";
  char *plain = elm_entry_markup_to_utf8(text);
  std::string result = plain == nullptr ? "" : plain;
  std::free(plain);
  return result;
}

std::string Markup(const std::string &text) {
  char *markup = elm_entry_utf8_to_markup(text.c_str());
  std::string result = markup == nullptr ? "" : markup;
  std::free(markup);
  return result;
}

std::string HourMinute(const base::OffsetDateTime &value) {
  char buffer[16];
  std::snprintf(buffer, sizeof(buffer), "%02d:%02d", value.LocalHour(),
                value.LocalMinute());
  return buffer;
}

std::string CompactMonthTitle(const std::string &title) {
  constexpr std::size_t kMaximumCharacters = 12;
  if (title.size() <= kMaximumCharacters)
    return title;
  return title.substr(0, kMaximumCharacters - 3) + "...";
}

class TizenReminderAlarmScheduler final
    : public usecases::ReminderAlarmScheduler {
public:
  std::optional<int>
  Schedule(const domain::CalendarReminder &reminder) override {
    app_control_h control = nullptr;
    if (app_control_create(&control) != APP_CONTROL_ERROR_NONE) {
      return std::nullopt;
    }
    const int app_result = app_control_set_app_id(control, kAppId);
    const int id_result = app_control_add_extra_data(control, kAlarmReminderId,
                                                     reminder.id().c_str());
    const int title_result = app_control_add_extra_data(
        control, kAlarmReminderTitle, reminder.title().c_str());
    if (app_result != APP_CONTROL_ERROR_NONE ||
        id_result != APP_CONTROL_ERROR_NONE ||
        title_result != APP_CONTROL_ERROR_NONE) {
      app_control_destroy(control);
      return std::nullopt;
    }

    const base::OffsetDateTime &due = reminder.due_at();
    struct tm date = {};
    const base::Date local_date = due.LocalDate();
    date.tm_year = local_date.year() - 1900;
    date.tm_mon = local_date.month() - 1;
    date.tm_mday = local_date.day();
    date.tm_hour = due.LocalHour();
    date.tm_min = due.LocalMinute();
    date.tm_sec = due.LocalSecond();
    date.tm_isdst = -1;

    int alarm_id = -1;
    const int result = alarm_schedule_once_at_date(control, &date, &alarm_id);
    app_control_destroy(control);
    return result == ALARM_ERROR_NONE ? std::optional<int>(alarm_id)
                                      : std::nullopt;
  }

  void Cancel(int alarm_id) override { alarm_cancel(alarm_id); }
};

} // namespace

bool CalendarApp::Create(void *data) {
  (void)data;
  elm_config_accel_preference_set("opengl");
  BuildWindow();
  if (win_ == nullptr)
    return false;

  events_ = std::make_unique<domain::CalendarEventRepository>();
  reminders_ = std::make_unique<domain::CalendarReminderRepository>();
  char *data_path = app_get_data_path();
  const std::string store_path =
      data_path == nullptr ? "calendar-data.json"
                           : std::string(data_path) + "calendar-data.json";
  std::free(data_path);
  persistence_ = std::make_unique<persistence::CalendarJsonStore>(store_path);
  alarms_ = std::make_unique<TizenReminderAlarmScheduler>();
  commands_ = std::make_unique<usecases::CalendarCommandService>(
      events_.get(), reminders_.get(), persistence_.get(), alarms_.get());
  const auto restored = commands_->Restore();
  if (!restored.success)
    status_message_ = restored.reason;

  calendar_core_ = std::make_unique<provider::CalendarServiceCore>(
      events_.get(), commands_.get());
  schedule_core_ = std::make_unique<provider::ScheduleServiceCore>(
      reminders_.get(), commands_.get());
  view_registry_ = std::make_unique<provider::CalendarViewRegistry>();
  view_core_ =
      std::make_unique<provider::CalendarViewServiceCore>(view_registry_.get());
  StartProviders();

  today_ = Today();
  interaction_ = state::CalendarInteractionState::Create(
      state::CalendarUiState::Create(today_));
  Render();
  evas_object_show(win_);
  ecore_idler_add(PublishIdle, this);
  return true;
}

void CalendarApp::StartProviders() {
  calendar_provider_ =
      std::make_unique<calendar_rpc::stub::TizenActionCalendar>();
  calendar_provider_->Listen(
      std::make_shared<provider::CalendarActionServiceFactory>(
          calendar_core_.get()));
  schedule_provider_ =
      std::make_unique<schedule_rpc::stub::TizenActionSchedule>();
  schedule_provider_->Listen(
      std::make_shared<provider::ScheduleActionServiceFactory>(
          schedule_core_.get()));
  view_provider_ = std::make_unique<view_rpc::stub::TizenActionView>();
  view_provider_->Listen(
      std::make_shared<provider::ViewActionServiceFactory>(view_core_.get()));
}

void CalendarApp::BuildWindow() {
  char *resource_path = app_get_resource_path();
  if (resource_path != nullptr) {
    theme_path_ = std::string(resource_path) + "calendar-theme.edj";
    dlog_print(DLOG_INFO, "CalendarNative", "theme=%s",
               theme_path_.c_str());
  }
  std::free(resource_path);

  win_ = elm_win_util_standard_add("calendar-native", "Calendar");
  if (win_ == nullptr)
    return;
  elm_win_autodel_set(win_, EINA_TRUE);
  elm_win_fullscreen_set(win_, EINA_TRUE);
  evas_object_smart_callback_add(win_, "delete,request", OnWindowDelete, this);
  eext_object_event_callback_add(win_, EEXT_CALLBACK_BACK, OnWindowBack, this);
  evas_object_event_callback_add(win_, EVAS_CALLBACK_KEY_DOWN, OnKeyDown, this);

  Evas_Object *background = elm_bg_add(win_);
  elm_bg_color_set(background, 247, 248, 251);
  evas_object_size_hint_weight_set(background, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  elm_win_resize_object_add(win_, background);
  evas_object_show(background);

  conformant_ = elm_conformant_add(win_);
  evas_object_size_hint_weight_set(conformant_, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  elm_win_resize_object_add(win_, conformant_);
  evas_object_show(conformant_);
}

void CalendarApp::Render() {
  focus_restore_event_id_ = interaction_.selected_event_id;
  if (root_ != nullptr)
    evas_object_del(root_);
  callbacks_.clear();
  rendered_events_.clear();
  reminder_offset_checks_.clear();
  title_entry_ = nullptr;
  start_entry_ = nullptr;
  end_entry_ = nullptr;
  location_entry_ = nullptr;
  note_entry_ = nullptr;
  keyword_entry_ = nullptr;
  search_start_entry_ = nullptr;
  search_end_entry_ = nullptr;
  reminder_title_entry_ = nullptr;
  reminder_due_entry_ = nullptr;
  reminder_note_entry_ = nullptr;
  reminder_completed_check_ = nullptr;

  root_ = elm_grid_add(conformant_);
  elm_grid_size_set(root_, 1920, 1080);
  evas_object_size_hint_weight_set(root_, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(root_, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_object_content_set(conformant_, root_);
  evas_object_show(root_);

  Evas_Object *content = elm_box_add(root_);
  elm_box_horizontal_set(content, EINA_FALSE);
  elm_box_padding_set(content, 12, 12);
  evas_object_size_hint_weight_set(content, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(content, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_grid_pack(root_, content, 64, 44, 1792, 992);
  evas_object_show(content);

  RenderCommandBar(content);
  switch (interaction_.surface) {
  case state::CalendarSurface::kCalendar:
    RenderCalendar(content);
    break;
  case state::CalendarSurface::kSearch:
    RenderSearch(content);
    break;
  case state::CalendarSurface::kEventDetail:
    RenderEventDetail(content);
    break;
  case state::CalendarSurface::kEventEditor:
    RenderEventEditor(content);
    break;
  case state::CalendarSurface::kDeleteEventConfirmation:
    RenderDeleteEventConfirmation(content);
    break;
  case state::CalendarSurface::kReminderList:
    RenderReminderList(content);
    break;
  case state::CalendarSurface::kReminderEditor:
    RenderReminderEditor(content);
    break;
  case state::CalendarSurface::kDeleteReminderConfirmation:
    RenderDeleteReminderConfirmation(content);
    break;
  }
  status_label_ = AddLabel(content, status_message_, EVAS_HINT_EXPAND);
  evas_object_size_hint_min_set(status_label_, 0, 34);
  ecore_idler_add(PublishIdle, this);
}

void CalendarApp::RenderCommandBar(Evas_Object *parent) {
  Evas_Object *holder = elm_table_add(parent);
  evas_object_size_hint_weight_set(holder, EVAS_HINT_EXPAND, 0.0);
  evas_object_size_hint_align_set(holder, EVAS_HINT_FILL, EVAS_HINT_FILL);
  evas_object_size_hint_min_set(holder, 0, 82);
  elm_box_pack_end(parent, holder);

  Evas_Object *surface =
      evas_object_rectangle_add(evas_object_evas_get(holder));
  evas_object_color_set(surface, 241, 243, 247, 255);
  evas_object_size_hint_weight_set(surface, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(surface, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_table_pack(holder, surface, 0, 0, 1, 1);
  evas_object_show(surface);

  Evas_Object *bar = elm_box_add(holder);
  elm_box_horizontal_set(bar, EINA_TRUE);
  elm_box_padding_set(bar, 12, 0);
  elm_box_align_set(bar, 0.0, 0.5);
  evas_object_size_hint_weight_set(bar, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(bar, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_table_pack(holder, bar, 0, 0, 1, 1);
  evas_object_show(bar);
  evas_object_show(holder);
  AddButton(bar, "Prev", Action::kPrevious);
  AddButton(bar, "Today", Action::kToday);
  AddButton(bar, "Next", Action::kNext);
  const auto projection =
      state::CalendarProjection::Create(interaction_.calendar, events_.get());
  Evas_Object *title = AddLabel(bar, projection.title, EVAS_HINT_EXPAND);
  evas_object_size_hint_weight_set(title, 0.0, 0.0);
  evas_object_size_hint_min_set(title, 330, 72);
  AddButton(bar, "Month", Action::kMonth);
  AddButton(bar, "Week", Action::kWeek);
  AddButton(bar, "Day", Action::kDay);
  AddButton(bar, "Agenda", Action::kAgenda);
  AddButton(bar, "Search", Action::kSearch);
}

void CalendarApp::RenderCalendar(Evas_Object *parent) {
  const auto projection =
      state::CalendarProjection::Create(interaction_.calendar, events_.get());
  switch (interaction_.calendar.view_mode()) {
  case state::CalendarViewMode::kMonth:
    RenderMonth(parent, projection);
    break;
  case state::CalendarViewMode::kWeek:
    RenderWeek(parent, projection);
    break;
  case state::CalendarViewMode::kDay:
    RenderDay(parent, projection);
    break;
  case state::CalendarViewMode::kAgenda:
    RenderAgenda(parent, projection);
    break;
  }
}

void CalendarApp::RenderMonth(Evas_Object *parent,
                              const state::CalendarProjection &projection) {
  (void)projection;
  Evas_Object *body = elm_panes_add(parent);
  elm_panes_horizontal_set(body, EINA_FALSE);
  elm_panes_fixed_set(body, EINA_TRUE);
  elm_panes_content_left_size_set(body, 0.68);
  evas_object_size_hint_weight_set(body, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(body, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_box_pack_end(parent, body);
  evas_object_show(body);

  Evas_Object *left = elm_box_add(body);
  elm_box_horizontal_set(left, EINA_FALSE);
  elm_box_padding_set(left, 12, 12);
  evas_object_size_hint_weight_set(left, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(left, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_object_part_content_set(body, "left", left);
  evas_object_show(left);
  Evas_Object *weekdays = elm_grid_add(left);
  elm_grid_size_set(weekdays, 7, 1);
  evas_object_size_hint_weight_set(weekdays, EVAS_HINT_EXPAND, 0.0);
  evas_object_size_hint_align_set(weekdays, EVAS_HINT_FILL, EVAS_HINT_FILL);
  evas_object_size_hint_min_set(weekdays, 0, 44);
  elm_box_pack_end(left, weekdays);
  evas_object_show(weekdays);
  for (std::size_t index = 0; index < kWeekdays.size(); ++index) {
    Evas_Object *weekday =
        evas_object_text_add(evas_object_evas_get(weekdays));
    evas_object_text_font_set(weekday, "Sans", 24);
    evas_object_text_text_set(weekday, kWeekdays[index]);
    evas_object_color_set(weekday, 20, 20, 24, 255);
    evas_object_size_hint_weight_set(weekday, EVAS_HINT_EXPAND,
                                     EVAS_HINT_EXPAND);
    evas_object_size_hint_align_set(weekday, 0.5, 0.5);
    elm_grid_pack(weekdays, weekday, static_cast<int>(index), 0, 1, 1);
    evas_object_show(weekday);
  }

  Evas_Object *table = elm_grid_add(left);
  elm_grid_size_set(table, 7, 6);
  evas_object_size_hint_weight_set(table, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(table, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_box_pack_end(left, table);
  evas_object_show(table);
  const auto cells = interaction_.calendar.BuildMonthCells();
  for (std::size_t index = 0; index < cells.size(); ++index) {
    std::vector<domain::CalendarEvent> events;
    events_->TryGetEventsOverlapping(
        domain::CalendarDateBoundary::AtStartOfDay(cells[index].date),
        domain::CalendarDateBoundary::AtStartOfDay(
            cells[index].date.AddDays(1)),
        &events);
    std::string text = std::to_string(cells[index].date.day());
    if (!events.empty())
      text += "\n" + CompactMonthTitle(events.front().title());
    Evas_Object *button =
        AddButton(table, text, Action::kSelectDate, "", cells[index].date);
    if (cells[index].date == interaction_.calendar.selected_date()) {
      evas_object_focus_set(button, EINA_TRUE);
      edje_object_signal_emit(button, "calendar,state,focused", "calendar");
    }
    elm_grid_pack(table, button, static_cast<int>(index % 7),
                  static_cast<int>(index / 7), 1, 1);
  }

  Evas_Object *pane = elm_box_add(body);
  elm_box_horizontal_set(pane, EINA_FALSE);
  elm_box_padding_set(pane, 12, 12);
  evas_object_size_hint_weight_set(pane, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(pane, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_object_part_content_set(body, "right", pane);
  evas_object_show(pane);
  const base::Date selected_date = interaction_.calendar.selected_date();
  Evas_Object *date_row = AddBox(pane, true, EVAS_HINT_EXPAND, 0.0);
  Evas_Object *day = AddLabel(date_row, std::to_string(selected_date.day()), 0.0);
  const std::string day_markup = "<font_size=32>" +
                                 std::to_string(selected_date.day()) +
                                 "</font_size>";
  elm_object_text_set(day, day_markup.c_str());
  evas_object_size_hint_min_set(day, 92, 78);
  std::string weekday = kWeekdays[selected_date.DayOfWeek()];
  std::transform(weekday.begin(), weekday.end(), weekday.begin(),
                 [](unsigned char value) {
                   return static_cast<char>(std::toupper(value));
                 });
  Evas_Object *weekday_label = AddLabel(date_row, weekday, 0.0);
  const std::string weekday_markup =
      "<font_size=14>" + weekday + "</font_size>";
  elm_object_text_set(weekday_label, weekday_markup.c_str());
  evas_object_size_hint_min_set(weekday_label, 100, 78);
  Evas_Object *month_label =
      AddLabel(pane, std::string(kMonths[selected_date.month() - 1]) + " " +
                         std::to_string(selected_date.year()),
               EVAS_HINT_EXPAND);
  const std::string month_markup =
      "<font_size=12>" + std::string(kMonths[selected_date.month() - 1]) +
      " " + std::to_string(selected_date.year()) + "</font_size>";
  elm_object_text_set(month_label, month_markup.c_str());
  evas_object_size_hint_min_set(month_label, 0, 42);
  auto selected = SelectedEvents();
  if (selected.empty()) {
    AddLabel(pane, "No events for this day", EVAS_HINT_EXPAND);
  } else {
    for (const auto &event : selected)
      AddEventButton(pane, event, false);
  }
  AddSpacer(pane, 20);
  AddButton(pane, "Add event  +", Action::kNewEvent);
  AddButton(pane, "Reminders  >", Action::kOpenReminders);
}

void CalendarApp::RenderWeek(Evas_Object *parent,
                             const state::CalendarProjection &projection) {
  Evas_Object *columns = elm_table_add(parent);
  elm_table_padding_set(columns, 12, 0);
  evas_object_size_hint_weight_set(columns, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(columns, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_box_pack_end(parent, columns);
  evas_object_show(columns);
  for (std::size_t index = 0; index < projection.event_groups.size(); ++index) {
    const auto &group = projection.event_groups[index];
    Evas_Object *holder = elm_table_add(columns);
    evas_object_size_hint_weight_set(holder, EVAS_HINT_EXPAND,
                                     EVAS_HINT_EXPAND);
    evas_object_size_hint_align_set(holder, EVAS_HINT_FILL, EVAS_HINT_FILL);
    elm_table_pack(columns, holder, static_cast<int>(index), 0, 1, 1);
    evas_object_show(holder);

    const bool selected = group.date == interaction_.calendar.selected_date();
    Evas_Object *focus =
        evas_object_rectangle_add(evas_object_evas_get(holder));
    evas_object_color_set(focus, 28, 29, 34, selected ? 255 : 0);
    evas_object_size_hint_weight_set(focus, EVAS_HINT_EXPAND,
                                     EVAS_HINT_EXPAND);
    evas_object_size_hint_align_set(focus, EVAS_HINT_FILL, EVAS_HINT_FILL);
    elm_table_pack(holder, focus, 0, 0, 1, 1);
    evas_object_show(focus);

    Evas_Object *surface =
        evas_object_rectangle_add(evas_object_evas_get(holder));
    evas_object_color_set(surface, selected ? 225 : 242, selected ? 234 : 244,
                          selected ? 250 : 247, 255);
    evas_object_size_hint_weight_set(surface, EVAS_HINT_EXPAND,
                                     EVAS_HINT_EXPAND);
    evas_object_size_hint_align_set(surface, EVAS_HINT_FILL, EVAS_HINT_FILL);
    if (selected)
      evas_object_size_hint_padding_set(surface, 4, 4, 4, 4);
    elm_table_pack(holder, surface, 0, 0, 1, 1);
    evas_object_show(surface);

    Evas_Object *column = elm_box_add(holder);
    elm_box_horizontal_set(column, EINA_FALSE);
    elm_box_padding_set(column, 8, 8);
    elm_box_align_set(column, 0.5, 0.0);
    evas_object_size_hint_weight_set(column, EVAS_HINT_EXPAND,
                                     EVAS_HINT_EXPAND);
    evas_object_size_hint_align_set(column, EVAS_HINT_FILL, EVAS_HINT_FILL);
    elm_table_pack(holder, column, 0, 0, 1, 1);
    evas_object_show(column);
    Evas_Object *heading = AddLabel(column, DateHeading(group.date), 0.0);
    evas_object_size_hint_min_set(heading, 0, 78);
    for (const auto &event : group.events) {
      AddEventButton(column, event, false);
    }
  }
}

void CalendarApp::RenderDay(Evas_Object *parent,
                            const state::CalendarProjection &projection) {
  Evas_Object *content = AddBox(parent, false);
  elm_box_align_set(content, 0.5, 0.0);
  AddLabel(content, DateHeading(projection.start_date), EVAS_HINT_EXPAND);
  if (projection.event_groups.front().events.empty()) {
    AddLabel(content, "No events. Create the first event for this day.",
             EVAS_HINT_EXPAND);
    AddButton(content, "Add event  +", Action::kNewEvent);
  } else {
    for (const auto &event : projection.event_groups.front().events) {
      AddEventButton(content, event, false);
    }
  }
}

void CalendarApp::RenderAgenda(Evas_Object *parent,
                               const state::CalendarProjection &projection) {
  Evas_Object *list = AddBox(parent, false);
  elm_box_align_set(list, 0.5, 0.0);
  if (projection.event_groups.empty()) {
    AddLabel(list, "No events in this month", EVAS_HINT_EXPAND);
    AddButton(list, "Add event  +", Action::kNewEvent);
  }
  for (const auto &group : projection.event_groups) {
    Evas_Object *row = AddBox(list, true, EVAS_HINT_EXPAND, 0.0);
    evas_object_size_hint_min_set(row, 0, 90);
    AddLabel(row, DateHeading(group.date), 0.6);
    Evas_Object *cards = AddBox(row, false, 1.4, EVAS_HINT_EXPAND);
    for (const auto &event : group.events) {
      AddEventButton(cards, event, false);
    }
  }
}

void CalendarApp::RenderSearch(Evas_Object *parent) {
  if (!interaction_.search.has_value())
    return;
  const state::CalendarSearchState &search = *interaction_.search;
  Evas_Object *holder = elm_grid_add(parent);
  elm_grid_size_set(holder, 1792, 880);
  evas_object_size_hint_weight_set(holder, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(holder, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_box_pack_end(parent, holder);
  evas_object_show(holder);

  Evas_Object *underlay = elm_box_add(holder);
  elm_box_horizontal_set(underlay, EINA_FALSE);
  evas_object_size_hint_weight_set(underlay, EVAS_HINT_EXPAND,
                                    EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(underlay, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_grid_pack(holder, underlay, 0, 0, 1792, 880);
  evas_object_show(underlay);
  RenderCalendar(underlay);

  Evas_Object *panel_holder = elm_table_add(holder);
  evas_object_size_hint_weight_set(panel_holder, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(panel_holder, EVAS_HINT_FILL,
                                  EVAS_HINT_FILL);
  elm_grid_pack(holder, panel_holder, 1092, 0, 700, 880);
  evas_object_show(panel_holder);

  Evas_Object *surface =
      evas_object_rectangle_add(evas_object_evas_get(panel_holder));
  evas_object_color_set(surface, 241, 243, 247, 255);
  evas_object_size_hint_weight_set(surface, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(surface, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_table_pack(panel_holder, surface, 0, 0, 1, 1);
  evas_object_show(surface);

  Evas_Object *panel = elm_box_add(panel_holder);
  elm_box_horizontal_set(panel, EINA_FALSE);
  elm_box_padding_set(panel, 14, 14);
  elm_box_align_set(panel, 0.5, 0.0);
  evas_object_size_hint_weight_set(panel, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(panel, EVAS_HINT_FILL, EVAS_HINT_FILL);
  evas_object_size_hint_padding_set(panel, 36, 36, 28, 28);
  elm_table_pack(panel_holder, panel, 0, 0, 1, 1);
  evas_object_show(panel);

  Evas_Object *close_row = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  elm_box_align_set(close_row, 0.0, 0.5);
  Evas_Object *close = AddButton(close_row, "Close", Action::kClose);
  evas_object_size_hint_weight_set(close, 0.0, 0.0);
  evas_object_size_hint_min_set(close, 130, 64);
  Evas_Object *title = AddLabel(panel, "Advanced search", EVAS_HINT_EXPAND);
  elm_object_text_set(title, "<font_size=28>Advanced search</font_size>");
  evas_object_size_hint_min_set(title, 0, 72);
  keyword_entry_ = AddEntry(panel, "Title, location, or note", search.keyword);
  Evas_Object *dates = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  search_start_entry_ = AddEntry(dates, "Start date (inclusive)",
                                 search.start_date.ToIsoString());
  search_end_entry_ = AddEntry(dates, "End date (exclusive)",
                               search.end_date_exclusive.ToIsoString());
  Evas_Object *fields = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddButton(fields, search.search_title ? "Title [x]" : "Title [ ]",
            Action::kToggleSearchTitle);
  AddButton(fields, search.search_location ? "Location [x]" : "Location [ ]",
            Action::kToggleSearchLocation);
  AddButton(fields, search.search_note ? "Notes [x]" : "Notes [ ]",
            Action::kToggleSearchNote);
  Evas_Object *search_row = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  elm_box_align_set(search_row, 1.0, 0.5);
  Evas_Object *search_button =
      AddButton(search_row, "Search", Action::kApplySearch);
  evas_object_size_hint_weight_set(search_button, 0.0, 0.0);
  evas_object_size_hint_min_set(search_button, 170, 64);
  if (search.has_applied && search.result_event_ids.empty()) {
    AddLabel(panel, "No matching events", EVAS_HINT_EXPAND);
  }
  for (const auto &id : search.result_event_ids) {
    auto resolved = events_->ResolveByIds({id});
    if (!resolved.events.empty()) {
      AddEventButton(panel, resolved.events.front(), true);
    }
  }
}

Evas_Object *CalendarApp::CreateOverlayPanel(Evas_Object *parent) {
  Evas_Object *holder = elm_grid_add(parent);
  elm_grid_size_set(holder, 1792, 880);
  evas_object_size_hint_weight_set(holder, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(holder, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_box_pack_end(parent, holder);
  evas_object_show(holder);

  Evas_Object *underlay = elm_box_add(holder);
  elm_box_horizontal_set(underlay, EINA_FALSE);
  evas_object_size_hint_weight_set(underlay, EVAS_HINT_EXPAND,
                                    EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(underlay, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_grid_pack(holder, underlay, 0, 0, 1792, 880);
  evas_object_show(underlay);
  RenderCalendar(underlay);

  Evas_Object *panel_holder = elm_table_add(holder);
  evas_object_size_hint_weight_set(panel_holder, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(panel_holder, EVAS_HINT_FILL,
                                  EVAS_HINT_FILL);
  elm_grid_pack(holder, panel_holder, 1092, 0, 700, 880);
  evas_object_show(panel_holder);

  Evas_Object *surface =
      evas_object_rectangle_add(evas_object_evas_get(panel_holder));
  evas_object_color_set(surface, 241, 243, 247, 255);
  evas_object_size_hint_weight_set(surface, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(surface, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_table_pack(panel_holder, surface, 0, 0, 1, 1);
  evas_object_show(surface);

  Evas_Object *panel = elm_box_add(panel_holder);
  elm_box_horizontal_set(panel, EINA_FALSE);
  elm_box_padding_set(panel, 14, 14);
  elm_box_align_set(panel, 0.5, 0.0);
  evas_object_size_hint_weight_set(panel, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(panel, EVAS_HINT_FILL, EVAS_HINT_FILL);
  evas_object_size_hint_padding_set(panel, 36, 36, 28, 28);
  elm_table_pack(panel_holder, panel, 0, 0, 1, 1);
  evas_object_show(panel);
  return panel;
}

void CalendarApp::RenderEventDetail(Evas_Object *parent) {
  const auto event = SelectedEvent();
  if (!event.has_value()) {
    AddLabel(parent, "This event is no longer available", EVAS_HINT_EXPAND);
    AddButton(parent, "Close", Action::kClose);
    return;
  }
  Evas_Object *panel = CreateOverlayPanel(parent);
  Evas_Object *top = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddLabel(top, event->title(), EVAS_HINT_EXPAND);
  AddButton(top, "Close", Action::kClose);
  AddLabel(panel, "When\n" + EventTimeText(*event, true), EVAS_HINT_EXPAND);
  AddLabel(panel,
           "Location\n" + (event->location().empty() ? "-" : event->location()),
           EVAS_HINT_EXPAND);
  AddLabel(panel, "Note\n" + (event->note().empty() ? "-" : event->note()),
           EVAS_HINT_EXPAND);
  AddSpacer(panel, 120);
  Evas_Object *actions = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddButton(actions, "Edit", Action::kEditEvent);
  AddButton(actions, "Delete", Action::kRequestDeleteEvent);
}

void CalendarApp::RenderEventEditor(Evas_Object *parent) {
  if (!interaction_.event_editor.has_value())
    return;
  const auto &editor = *interaction_.event_editor;
  Evas_Object *panel = CreateOverlayPanel(parent);
  Evas_Object *top = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddLabel(top, editor.IsEditing() ? "Edit event" : "Create event",
           EVAS_HINT_EXPAND);
  AddButton(top, "Cancel", Action::kClose);
  title_entry_ = AddEntry(panel, "Title", editor.title);
  Evas_Object *range = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  start_entry_ = AddEntry(range, "Start", editor.start.ToRoundTripString());
  end_entry_ = AddEntry(range, "End", editor.end.ToRoundTripString());
  location_entry_ = AddEntry(panel, "Location", editor.location);
  Evas_Object *note_section = AddBox(panel, false, EVAS_HINT_EXPAND, 0.0);
  evas_object_size_hint_min_set(note_section, 0, 150);
  Evas_Object *note_label = AddLabel(note_section, "Notes", 0.0);
  evas_object_size_hint_min_set(note_label, 0, 30);
  note_entry_ = AddEntry(note_section, "Add notes", editor.note, false);
  Evas_Object *reminders = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  for (int offset : domain::CalendarReminder::AllowedOffsetMinutes()) {
    Evas_Object *check = elm_check_add(reminders);
    elm_object_text_set(check, (std::to_string(offset) + " min").c_str());
    elm_check_state_set(check, editor.reminder_offsets.find(offset) !=
                                   editor.reminder_offsets.end());
    evas_object_data_set(
        check, "offset",
        reinterpret_cast<void *>(static_cast<intptr_t>(offset)));
    elm_box_pack_end(reminders, check);
    evas_object_show(check);
    reminder_offset_checks_.push_back(check);
  }
  AddButton(panel, "Save event", Action::kSaveEvent);
}

void CalendarApp::RenderDeleteEventConfirmation(Evas_Object *parent) {
  const auto event = SelectedEvent();
  Evas_Object *panel = CreateOverlayPanel(parent);
  AddLabel(panel, "Delete event?", EVAS_HINT_EXPAND);
  AddLabel(panel,
           event.has_value()
               ? event->title() + "\n" + EventTimeText(*event, true)
               : "The selected event is unavailable.",
           EVAS_HINT_EXPAND);
  Evas_Object *actions = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddButton(actions, "Cancel", Action::kCancelDeleteEvent);
  AddButton(actions, "Delete", Action::kConfirmDeleteEvent);
}

void CalendarApp::RenderReminderList(Evas_Object *parent) {
  Evas_Object *panel = CreateOverlayPanel(parent);
  Evas_Object *top = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddLabel(top, "Reminders", EVAS_HINT_EXPAND);
  AddButton(top, "Close", Action::kClose);
  auto reminders = reminders_->Snapshot();
  bool found = false;
  for (const auto &reminder : reminders) {
    if (reminder.calendar_event_id().has_value())
      continue;
    found = true;
    Evas_Object *row = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
    const std::string text = (reminder.is_completed() ? "[done] " : "[open] ") +
                             reminder.title() + "  " +
                             reminder.due_at().ToRoundTripString();
    AddButton(row, text, Action::kEditReminder, reminder.id());
    if (!reminder.is_completed()) {
      AddButton(row, "Complete", Action::kCompleteReminder, reminder.id());
    }
  }
  if (!found)
    AddLabel(panel, "No independent reminders", EVAS_HINT_EXPAND);
  AddButton(panel, "New reminder  +", Action::kNewReminder);
}

void CalendarApp::RenderReminderEditor(Evas_Object *parent) {
  if (!interaction_.reminder_editor.has_value())
    return;
  const auto &editor = *interaction_.reminder_editor;
  Evas_Object *panel = CreateOverlayPanel(parent);
  Evas_Object *top = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddLabel(top, editor.IsEditing() ? "Edit reminder" : "Create reminder",
           EVAS_HINT_EXPAND);
  AddButton(top, "Cancel", Action::kClose);
  reminder_title_entry_ = AddEntry(panel, "Title", editor.title);
  reminder_due_entry_ =
      AddEntry(panel, "Due date and time", editor.due_at.ToRoundTripString());
  reminder_note_entry_ = AddEntry(panel, "Notes", editor.note, true);
  reminder_completed_check_ = elm_check_add(panel);
  elm_object_text_set(reminder_completed_check_, "Completed");
  elm_check_state_set(reminder_completed_check_, editor.is_completed);
  elm_box_pack_end(panel, reminder_completed_check_);
  evas_object_show(reminder_completed_check_);
  Evas_Object *actions = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddButton(actions, "Save reminder", Action::kSaveReminder);
  if (editor.IsEditing()) {
    AddButton(actions, "Delete", Action::kRequestDeleteReminder);
  }
}

void CalendarApp::RenderDeleteReminderConfirmation(Evas_Object *parent) {
  const auto reminder =
      interaction_.selected_reminder_id.has_value()
          ? reminders_->Find(*interaction_.selected_reminder_id)
          : std::nullopt;
  Evas_Object *panel = CreateOverlayPanel(parent);
  AddLabel(panel, "Delete reminder?", EVAS_HINT_EXPAND);
  AddLabel(panel,
           reminder.has_value() ? reminder->title()
                                : "The selected reminder is unavailable.",
           EVAS_HINT_EXPAND);
  Evas_Object *actions = AddBox(panel, true, EVAS_HINT_EXPAND, 0.0);
  AddButton(actions, "Cancel", Action::kCancelDeleteReminder);
  AddButton(actions, "Delete", Action::kConfirmDeleteReminder);
}

Evas_Object *CalendarApp::AddBox(Evas_Object *parent, bool horizontal,
                                 double weight_x, double weight_y) {
  Evas_Object *box = elm_box_add(parent);
  elm_box_horizontal_set(box, horizontal ? EINA_TRUE : EINA_FALSE);
  elm_box_padding_set(box, 12, 12);
  evas_object_size_hint_weight_set(box, weight_x, weight_y);
  evas_object_size_hint_align_set(box, EVAS_HINT_FILL, EVAS_HINT_FILL);
  if (parent != conformant_)
    elm_box_pack_end(parent, box);
  evas_object_show(box);
  return box;
}

Evas_Object *CalendarApp::AddLabel(Evas_Object *parent, const std::string &text,
                                   double weight_x) {
  Evas_Object *label = elm_label_add(parent);
  const std::string safe = Markup(text);
  elm_object_text_set(label, safe.c_str());
  elm_label_line_wrap_set(label, ELM_WRAP_WORD);
  evas_object_size_hint_weight_set(label, weight_x, 0.0);
  evas_object_size_hint_align_set(label, EVAS_HINT_FILL, 0.5);
  evas_object_size_hint_min_set(label, 0, 42);
  elm_box_pack_end(parent, label);
  evas_object_show(label);
  return label;
}

Evas_Object *CalendarApp::AddButton(Evas_Object *parent,
                                    const std::string &text, Action action,
                                    const std::string &id,
                                    const base::Date &date) {
  Evas_Object *button = edje_object_add(evas_object_evas_get(parent));
  const char *style = "calendar_action";
  switch (action) {
  case Action::kPrevious:
  case Action::kToday:
  case Action::kNext:
  case Action::kSearch:
    style = "calendar_command";
    break;
  case Action::kMonth:
    style = interaction_.calendar.view_mode() == state::CalendarViewMode::kMonth
                ? "calendar_selected"
                : "calendar_command";
    break;
  case Action::kWeek:
    style = interaction_.calendar.view_mode() == state::CalendarViewMode::kWeek
                ? "calendar_selected"
                : "calendar_command";
    break;
  case Action::kDay:
    style = interaction_.calendar.view_mode() == state::CalendarViewMode::kDay
                ? "calendar_selected"
                : "calendar_command";
    break;
  case Action::kAgenda:
    style = interaction_.calendar.view_mode() == state::CalendarViewMode::kAgenda
                ? "calendar_selected"
                : "calendar_command";
    break;
  case Action::kSelectDate:
    if (date == interaction_.calendar.selected_date()) {
      style = "calendar_cell_selected";
    } else if (date.year() != interaction_.calendar.selected_date().year() ||
               date.month() != interaction_.calendar.selected_date().month()) {
      style = "calendar_cell_muted";
    } else {
      style = "calendar_cell";
    }
    break;
  case Action::kOpenEvent:
    style = "calendar_event";
    break;
  case Action::kClose:
  case Action::kSaveEvent:
  case Action::kApplySearch:
  case Action::kSaveReminder:
    style = "calendar_accent";
    break;
  case Action::kToggleSearchTitle:
  case Action::kToggleSearchLocation:
  case Action::kToggleSearchNote:
    style = "calendar_toggle";
    break;
  case Action::kConfirmDeleteEvent:
  case Action::kConfirmDeleteReminder:
    style = "calendar_danger";
    break;
  default:
    break;
  }
  const std::string group = std::string("calendar/button/") + style;
  if (!edje_object_file_set(button, theme_path_.c_str(), group.c_str())) {
    dlog_print(DLOG_ERROR, "CalendarNative", "edje group failed: %s",
               group.c_str());
  }
  if (action == Action::kSelectDate) {
    const std::size_t separator = text.find('\n');
    const std::string day = text.substr(0, separator);
    const std::string event = separator == std::string::npos
                                  ? std::string()
                                  : text.substr(separator + 1);
    edje_object_part_text_set(button, "calendar.day", day.c_str());
    edje_object_part_text_set(button, "calendar.event", event.c_str());
    if (!event.empty()) {
      edje_object_signal_emit(button, "calendar,state,event,visible",
                              "calendar");
    }
  } else {
    edje_object_part_text_set(button, "calendar.text", text.c_str());
  }
  int minimum_width = 0;
  double weight_x = EVAS_HINT_EXPAND;
  switch (action) {
  case Action::kPrevious:
  case Action::kToday:
  case Action::kNext:
    minimum_width = 100;
    weight_x = 0.0;
    break;
  case Action::kMonth:
  case Action::kWeek:
  case Action::kDay:
    minimum_width = 110;
    weight_x = 0.0;
    break;
  case Action::kAgenda:
  case Action::kSearch:
    minimum_width = 120;
    weight_x = 0.0;
    break;
  default:
    break;
  }
  evas_object_size_hint_weight_set(button, weight_x, 0.0);
  evas_object_size_hint_align_set(button, EVAS_HINT_FILL, EVAS_HINT_FILL);
  evas_object_size_hint_min_set(button, minimum_width, 64);
  auto context = std::make_unique<ActionContext>();
  context->app = this;
  context->action = action;
  context->id = id;
  context->date = date;
  evas_object_event_callback_add(button, EVAS_CALLBACK_MOUSE_UP,
                                 OnButtonMouseUp, context.get());
  evas_object_event_callback_add(button, EVAS_CALLBACK_FOCUS_IN,
                                 OnButtonFocusIn, context.get());
  evas_object_event_callback_add(button, EVAS_CALLBACK_FOCUS_OUT,
                                 OnButtonFocusOut, context.get());
  callbacks_.push_back(std::move(context));
  const char *parent_type = elm_object_widget_type_get(parent);
  const bool is_positioned_container =
      parent_type != nullptr &&
      (std::strcmp(parent_type, "Elm_Table") == 0 ||
       std::strcmp(parent_type, "Elm_Grid") == 0);
  if (!is_positioned_container) {
    elm_box_pack_end(parent, button);
  }
  evas_object_show(button);
  return button;
}

Evas_Object *CalendarApp::AddEntry(Evas_Object *parent,
                                   const std::string &guide,
                                   const std::string &text, bool multiline) {
  Evas_Object *field = elm_table_add(parent);
  evas_object_size_hint_weight_set(field, EVAS_HINT_EXPAND, 0.0);
  evas_object_size_hint_align_set(field, EVAS_HINT_FILL, EVAS_HINT_FILL);
  evas_object_size_hint_min_set(field, 0, multiline ? 112 : 70);
  elm_box_pack_end(parent, field);
  evas_object_show(field);

  Evas_Object *border =
      evas_object_rectangle_add(evas_object_evas_get(field));
  evas_object_color_set(border, 207, 211, 219, 255);
  evas_object_size_hint_weight_set(border, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(border, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_table_pack(field, border, 0, 0, 1, 1);
  evas_object_show(border);

  Evas_Object *surface =
      evas_object_rectangle_add(evas_object_evas_get(field));
  evas_object_color_set(surface, 250, 250, 251, 255);
  evas_object_size_hint_weight_set(surface, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(surface, EVAS_HINT_FILL, EVAS_HINT_FILL);
  evas_object_size_hint_padding_set(surface, 2, 2, 2, 2);
  elm_table_pack(field, surface, 0, 0, 1, 1);
  evas_object_show(surface);

  Evas_Object *entry = elm_entry_add(field);
  elm_entry_single_line_set(entry, multiline ? EINA_FALSE : EINA_TRUE);
  elm_entry_scrollable_set(entry, EINA_TRUE);
  elm_object_part_text_set(entry, "guide", guide.c_str());
  const std::string safe = Markup(text);
  elm_object_text_set(entry, safe.c_str());
  evas_object_size_hint_weight_set(entry, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(entry, EVAS_HINT_FILL, EVAS_HINT_FILL);
  evas_object_size_hint_padding_set(entry, 12, 12, 6, 6);
  elm_table_pack(field, entry, 0, 0, 1, 1);
  evas_object_show(entry);
  return entry;
}

Evas_Object *CalendarApp::AddScroller(Evas_Object *parent,
                                      Evas_Object *content) {
  Evas_Object *scroller = elm_scroller_add(parent);
  elm_object_content_set(scroller, content);
  evas_object_size_hint_weight_set(scroller, EVAS_HINT_EXPAND,
                                   EVAS_HINT_EXPAND);
  evas_object_size_hint_align_set(scroller, EVAS_HINT_FILL, EVAS_HINT_FILL);
  elm_box_pack_end(parent, scroller);
  evas_object_show(scroller);
  return scroller;
}

Evas_Object *CalendarApp::AddEventButton(Evas_Object *parent,
                                         const domain::CalendarEvent &event,
                                         bool include_date) {
  const std::string text =
      EventTimeText(event, include_date) + "  " + event.title() +
      (event.location().empty() ? "" : "  ·  " + event.location());
  Evas_Object *button = AddButton(parent, text, Action::kOpenEvent, event.id());
  evas_object_smart_callback_add(button, "focused", OnEventFocused, this);
  rendered_events_.push_back({button, event});
  return button;
}

void CalendarApp::AddSpacer(Evas_Object *parent, int minimum_height) {
  Evas_Object *spacer = evas_object_rectangle_add(evas_object_evas_get(parent));
  evas_object_color_set(spacer, 0, 0, 0, 0);
  evas_object_size_hint_weight_set(spacer, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
  evas_object_size_hint_min_set(spacer, 0, minimum_height);
  elm_box_pack_end(parent, spacer);
  evas_object_show(spacer);
}

void CalendarApp::SetStatus(const std::string &message) {
  status_message_ = message;
}

void CalendarApp::HandleAction(const ActionContext &context) {
  auto &calendar = interaction_.calendar;
  switch (context.action) {
  case Action::kPrevious:
    calendar = calendar.MovePeriod(-1);
    break;
  case Action::kToday:
    today_ = Today();
    calendar = calendar.ActivateToday(today_);
    break;
  case Action::kNext:
    calendar = calendar.MovePeriod(1);
    break;
  case Action::kMonth:
    calendar = calendar.ChangeViewMode(state::CalendarViewMode::kMonth);
    break;
  case Action::kWeek:
    calendar = calendar.ChangeViewMode(state::CalendarViewMode::kWeek);
    break;
  case Action::kDay:
    calendar = calendar.ChangeViewMode(state::CalendarViewMode::kDay);
    break;
  case Action::kAgenda:
    calendar = calendar.ChangeViewMode(state::CalendarViewMode::kAgenda);
    break;
  case Action::kSearch:
    interaction_ = interaction_.OpenSearch();
    break;
  case Action::kSelectDate: {
    state::CalendarUiCommand command;
    command.value = state::CalendarUiCommand::SelectDate{context.date};
    calendar = state::CalendarUiReducer::Reduce(calendar, command, today_,
                                                SelectedEvents().size());
    break;
  }
  case Action::kOpenEvent:
    focus_restore_event_id_ = context.id;
    interaction_ = interaction_.surface == state::CalendarSurface::kSearch
                       ? interaction_.OpenSearchResult(context.id)
                       : interaction_.OpenEventDetail(context.id);
    break;
  case Action::kNewEvent:
    interaction_ = interaction_.OpenNewEvent();
    break;
  case Action::kOpenReminders:
    interaction_ = interaction_.OpenReminderList();
    break;
  case Action::kClose:
    interaction_ = interaction_.Back();
    break;
  case Action::kEditEvent: {
    auto event = SelectedEvent();
    if (event.has_value()) {
      std::vector<int> offsets;
      for (const auto &reminder :
           reminders_->FindByCalendarEventId(event->id())) {
        if (reminder.offset_minutes().has_value()) {
          offsets.push_back(*reminder.offset_minutes());
        }
      }
      interaction_ = interaction_.OpenEventEditor(*event, offsets);
    }
    break;
  }
  case Action::kRequestDeleteEvent:
    interaction_ = interaction_.RequestEventDelete();
    break;
  case Action::kConfirmDeleteEvent:
    if (interaction_.selected_event_id.has_value()) {
      auto result = commands_->DeleteEvent(*interaction_.selected_event_id);
      SetStatus(result.reason);
      if (result.success) {
        interaction_.surface = state::CalendarSurface::kCalendar;
        interaction_.selected_event_id = std::nullopt;
      }
    }
    break;
  case Action::kCancelDeleteEvent:
    interaction_ = interaction_.CancelEventDelete();
    break;
  case Action::kSaveEvent:
    SaveEventFromEditor();
    break;
  case Action::kApplySearch:
    ApplySearchFromEditor();
    break;
  case Action::kToggleSearchTitle:
  case Action::kToggleSearchLocation:
  case Action::kToggleSearchNote: {
    auto search = *interaction_.search;
    search = search.WithKeyword(EntryText(keyword_entry_));
    base::Date start;
    base::Date end;
    if (TryParseDate(EntryText(search_start_entry_), &start) &&
        TryParseDate(EntryText(search_end_entry_), &end)) {
      search = search.WithPeriod(start, end);
    }
    bool title = search.search_title;
    bool location = search.search_location;
    bool note = search.search_note;
    if (context.action == Action::kToggleSearchTitle)
      title = !title;
    if (context.action == Action::kToggleSearchLocation)
      location = !location;
    if (context.action == Action::kToggleSearchNote)
      note = !note;
    interaction_.search = search.WithFields(title, location, note);
    break;
  }
  case Action::kNewReminder: {
    const auto due =
        domain::CalendarDateBoundary::AtStartOfDay(today_).AddHours(16);
    interaction_ = interaction_.OpenNewReminder(due);
    break;
  }
  case Action::kEditReminder: {
    auto reminder = reminders_->Find(context.id);
    if (reminder.has_value()) {
      interaction_ = interaction_.OpenReminderEditor(*reminder);
    }
    break;
  }
  case Action::kCompleteReminder: {
    auto result = commands_->SetReminderCompleted(context.id, true);
    SetStatus(result.reason);
    break;
  }
  case Action::kSaveReminder:
    SaveReminderFromEditor();
    break;
  case Action::kRequestDeleteReminder:
    interaction_ = interaction_.RequestReminderDelete();
    break;
  case Action::kConfirmDeleteReminder:
    if (interaction_.selected_reminder_id.has_value()) {
      auto result =
          commands_->DeleteReminder(*interaction_.selected_reminder_id);
      SetStatus(result.reason);
      if (result.success) {
        interaction_.surface = state::CalendarSurface::kReminderList;
        interaction_.selected_reminder_id = std::nullopt;
        interaction_.reminder_editor = std::nullopt;
      }
    }
    break;
  case Action::kCancelDeleteReminder:
    interaction_ = interaction_.Back();
    break;
  }
  Render();
}

void CalendarApp::HandleBack() {
  if (interaction_.surface != state::CalendarSurface::kCalendar) {
    interaction_ = interaction_.Back();
    Render();
    return;
  }
  if (interaction_.calendar.HandleBack() ==
      state::CalendarBackResult::kCloseAgenda) {
    interaction_.calendar = interaction_.calendar.ReturnToMonth();
    Render();
    return;
  }
  ui_app_exit();
}

void CalendarApp::SaveEventFromEditor() {
  if (!interaction_.event_editor.has_value())
    return;
  base::OffsetDateTime start;
  base::OffsetDateTime end;
  if (!base::OffsetDateTime::TryParseFlexible(EntryText(start_entry_),
                                              &start) ||
      !base::OffsetDateTime::TryParseFlexible(EntryText(end_entry_), &end)) {
    SetStatus("Start and end must be valid ISO 8601 date-times.");
    return;
  }
  auto editor = interaction_.event_editor->WithTitle(EntryText(title_entry_))
                    .WithRange(start, end)
                    .WithLocation(EntryText(location_entry_))
                    .WithNote(EntryText(note_entry_));
  editor.reminder_offsets.clear();
  for (Evas_Object *check : reminder_offset_checks_) {
    if (!elm_check_state_get(check))
      continue;
    const auto value =
        reinterpret_cast<intptr_t>(evas_object_data_get(check, "offset"));
    editor.reminder_offsets.insert(static_cast<int>(value));
  }
  if (!editor.CanSave()) {
    SetStatus(editor.ValidationMessage().value_or("Invalid event."));
    return;
  }
  domain::CalendarEvent event;
  std::string error;
  const std::string id = editor.event_id.value_or(UniqueId("event"));
  if (!domain::CalendarEvent::TryCreate(id, editor.title, editor.start,
                                        editor.end, editor.note,
                                        editor.location, &event, &error)) {
    SetStatus(error);
    return;
  }
  std::vector<int> offsets(editor.reminder_offsets.begin(),
                           editor.reminder_offsets.end());
  auto result = editor.IsEditing() ? commands_->UpdateEvent(event, offsets)
                                   : commands_->CreateEvent(event, offsets);
  SetStatus(result.reason);
  if (!result.success)
    return;
  interaction_.calendar =
      interaction_.calendar.ActivateToday(event.start().LocalDate())
          .WithFocusedEventId(event.id());
  interaction_.surface = state::CalendarSurface::kEventDetail;
  interaction_.selected_event_id = event.id();
  interaction_.event_editor = std::nullopt;
}

void CalendarApp::ApplySearchFromEditor() {
  if (!interaction_.search.has_value())
    return;
  base::Date start;
  base::Date end;
  if (!TryParseDate(EntryText(search_start_entry_), &start) ||
      !TryParseDate(EntryText(search_end_entry_), &end)) {
    SetStatus("Search dates must use yyyy-MM-dd.");
    return;
  }
  auto search = interaction_.search->WithKeyword(EntryText(keyword_entry_))
                    .WithPeriod(start, end);
  if (!search.CanApply()) {
    SetStatus(search.ValidationMessage().value_or("Invalid search."));
    interaction_.search = search;
    return;
  }
  interaction_.search = search.Apply(events_.get());
  SetStatus("Search complete.");
}

void CalendarApp::SaveReminderFromEditor() {
  if (!interaction_.reminder_editor.has_value())
    return;
  base::OffsetDateTime due_at;
  if (!base::OffsetDateTime::TryParseFlexible(EntryText(reminder_due_entry_),
                                              &due_at)) {
    SetStatus("Due date must be a valid ISO 8601 date-time.");
    return;
  }
  auto editor =
      interaction_.reminder_editor->WithTitle(EntryText(reminder_title_entry_))
          .WithDueAt(due_at)
          .WithNote(EntryText(reminder_note_entry_))
          .WithCompleted(elm_check_state_get(reminder_completed_check_));
  if (!editor.CanSave()) {
    SetStatus(editor.ValidationMessage().value_or("Invalid reminder."));
    return;
  }
  const std::string id = editor.reminder_id.value_or(UniqueId("reminder"));
  domain::CalendarReminder reminder = editor.ToDomain(id);
  auto result = editor.IsEditing() ? commands_->UpdateReminder(reminder)
                                   : commands_->CreateReminder(reminder);
  SetStatus(result.reason);
  if (!result.success)
    return;
  interaction_.surface = state::CalendarSurface::kReminderList;
  interaction_.selected_reminder_id = std::nullopt;
  interaction_.reminder_editor = std::nullopt;
}

void CalendarApp::PublishVisibleEvents() {
  if (view_registry_ == nullptr)
    return;
  std::vector<provider::VisibleEventView> views;
  for (const auto &rendered : rendered_events_) {
    if (rendered.object == nullptr ||
        !evas_object_visible_get(rendered.object)) {
      continue;
    }
    Evas_Coord x = 0;
    Evas_Coord y = 0;
    Evas_Coord width = 0;
    Evas_Coord height = 0;
    evas_object_geometry_get(rendered.object, &x, &y, &width, &height);
    if (width <= 0 || height <= 0)
      continue;
    calendar_rpc::TizenEntityCalendar entity(
        rendered.event.id(), "", rendered.event.title(),
        rendered.event.start().ToRoundTripString(),
        rendered.event.end().ToRoundTripString(), rendered.event.note(),
        rendered.event.location());
    views.push_back({rendered.event,
                     {static_cast<double>(x), static_cast<double>(y),
                      static_cast<double>(width), static_cast<double>(height)},
                     {static_cast<double>(x), static_cast<double>(y),
                      static_cast<double>(width), static_cast<double>(height)},
                     elm_object_focus_get(rendered.object) == EINA_TRUE,
                     entity.ToJson()});
  }
  view_registry_->Publish(views);
}

void CalendarApp::RestoreFocus() {
  if (!focus_restore_event_id_.has_value())
    return;
  for (const auto &rendered : rendered_events_) {
    if (rendered.event.id() == *focus_restore_event_id_) {
      evas_object_focus_set(rendered.object, EINA_TRUE);
      return;
    }
  }
}

std::vector<domain::CalendarEvent> CalendarApp::SelectedEvents() const {
  std::vector<domain::CalendarEvent> events;
  const base::Date &date = interaction_.calendar.selected_date();
  events_->TryGetEventsOverlapping(
      domain::CalendarDateBoundary::AtStartOfDay(date),
      domain::CalendarDateBoundary::AtStartOfDay(date.AddDays(1)), &events);
  return events;
}

std::optional<domain::CalendarEvent> CalendarApp::SelectedEvent() const {
  if (!interaction_.selected_event_id.has_value())
    return std::nullopt;
  auto result = events_->ResolveByIds({*interaction_.selected_event_id});
  return result.events.empty()
             ? std::nullopt
             : std::optional<domain::CalendarEvent>(result.events.front());
}

base::Date CalendarApp::Today() {
  const std::time_t now = std::time(nullptr);
  struct tm local = {};
  localtime_r(&now, &local);
  return base::Date(local.tm_year + 1900, local.tm_mon + 1, local.tm_mday);
}

bool CalendarApp::TryParseDate(const std::string &text, base::Date *date) {
  int year = 0;
  int month = 0;
  int day = 0;
  char tail = '\0';
  if (std::sscanf(text.c_str(), "%d-%d-%d%c", &year, &month, &day, &tail) !=
      3) {
    return false;
  }
  if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 ||
      day > base::Date::DaysInMonth(year, month)) {
    return false;
  }
  *date = base::Date(year, month, day);
  return true;
}

std::string CalendarApp::EventTimeText(const domain::CalendarEvent &event,
                                       bool include_date) {
  std::string text;
  if (include_date)
    text = event.start().LocalDate().ToIsoString() + "  ";
  if (event.IsAllDay())
    return text + "All day";
  return text + HourMinute(event.start()) + "–" + HourMinute(event.end());
}

std::string CalendarApp::DateHeading(const base::Date &date) {
  return std::string(kWeekdays[date.DayOfWeek()]) + ", " +
         kMonths[date.month() - 1] + " " + std::to_string(date.day());
}

std::string CalendarApp::UniqueId(const char *prefix) {
  static std::atomic<unsigned long long> counter{0};
  return std::string(prefix) + "-" +
         std::to_string(static_cast<long long>(std::time(nullptr))) + "-" +
         std::to_string(++counter);
}

void CalendarApp::OnAction(void *data, Evas_Object *object, void *event_info) {
  (void)object;
  (void)event_info;
  auto *context = static_cast<ActionContext *>(data);
  context->app->HandleAction(*context);
}

void CalendarApp::OnButtonMouseUp(void *data, Evas *canvas,
                                  Evas_Object *object, void *event_info) {
  (void)canvas;
  (void)object;
  const auto *mouse = static_cast<Evas_Event_Mouse_Up *>(event_info);
  if (mouse != nullptr && mouse->button != 1)
    return;
  auto *context = static_cast<ActionContext *>(data);
  context->app->HandleAction(*context);
}

void CalendarApp::OnButtonFocusIn(void *data, Evas *canvas,
                                  Evas_Object *object, void *event_info) {
  (void)data;
  (void)canvas;
  (void)event_info;
  edje_object_signal_emit(object, "calendar,state,focused", "calendar");
  evas_object_smart_callback_call(object, "focused", nullptr);
}

void CalendarApp::OnButtonFocusOut(void *data, Evas *canvas,
                                   Evas_Object *object, void *event_info) {
  (void)data;
  (void)canvas;
  (void)event_info;
  edje_object_signal_emit(object, "calendar,state,unfocused", "calendar");
}

void CalendarApp::OnEventFocused(void *data, Evas_Object *object,
                                 void *event_info) {
  (void)object;
  (void)event_info;
  static_cast<CalendarApp *>(data)->PublishVisibleEvents();
}

void CalendarApp::OnWindowDelete(void *data, Evas_Object *object,
                                 void *event_info) {
  (void)data;
  (void)object;
  (void)event_info;
  ui_app_exit();
}

void CalendarApp::OnWindowBack(void *data, Evas_Object *object,
                               void *event_info) {
  (void)object;
  (void)event_info;
  static_cast<CalendarApp *>(data)->HandleBack();
}

void CalendarApp::OnKeyDown(void *data, Evas *canvas, Evas_Object *object,
                            void *event_info) {
  (void)canvas;
  (void)object;
  auto *event = static_cast<Evas_Event_Key_Down *>(event_info);
  if (event == nullptr || event->keyname == nullptr)
    return;
  const std::string key = event->keyname;
  if (key == "Escape" || key == "XF86Back") {
    static_cast<CalendarApp *>(data)->HandleBack();
  }
}

Eina_Bool CalendarApp::PublishIdle(void *data) {
  auto *app = static_cast<CalendarApp *>(data);
  app->RestoreFocus();
  app->PublishVisibleEvents();
  return ECORE_CALLBACK_CANCEL;
}

void CalendarApp::Control(app_control_h app_control, void *data) {
  (void)app_control;
  (void)data;
  Render();
}

void CalendarApp::Pause(void *data) {
  (void)data;
  if (view_registry_ != nullptr)
    view_registry_->Clear();
}

void CalendarApp::Resume(void *data) {
  (void)data;
  today_ = Today();
  Render();
}

void CalendarApp::Terminate(void *data) {
  (void)data;
  if (view_registry_ != nullptr)
    view_registry_->Clear();
  rendered_events_.clear();
  callbacks_.clear();
  calendar_provider_.reset();
  schedule_provider_.reset();
  view_provider_.reset();
}

bool CalendarApp::AppCreateCb(void *data) {
  return static_cast<CalendarApp *>(data)->Create(data);
}

void CalendarApp::AppControlCb(app_control_h app_control, void *data) {
  static_cast<CalendarApp *>(data)->Control(app_control, data);
}

void CalendarApp::AppPauseCb(void *data) {
  static_cast<CalendarApp *>(data)->Pause(data);
}

void CalendarApp::AppResumeCb(void *data) {
  static_cast<CalendarApp *>(data)->Resume(data);
}

void CalendarApp::AppTerminateCb(void *data) {
  static_cast<CalendarApp *>(data)->Terminate(data);
}

} // namespace ui
} // namespace calendar
