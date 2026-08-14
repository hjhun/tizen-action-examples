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

#ifndef CALENDAR_NATIVE_UI_CALENDAR_APP_HH_
#define CALENDAR_NATIVE_UI_CALENDAR_APP_HH_

#include <Elementary.h>
#include <app.h>

#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include "provider/action_services.hh"
#include "provider/calendar_service_core.hh"
#include "provider/calendar_view_registry.hh"
#include "state/calendar_interaction_state.hh"
#include "state/calendar_projection.hh"

namespace calendar {
namespace ui {

class CalendarApp {
public:
  CalendarApp() = default;
  ~CalendarApp() = default;

  bool Create(void *data);
  void Control(app_control_h app_control, void *data);
  void Pause(void *data);
  void Resume(void *data);
  void Terminate(void *data);

  static bool AppCreateCb(void *data);
  static void AppControlCb(app_control_h app_control, void *data);
  static void AppPauseCb(void *data);
  static void AppResumeCb(void *data);
  static void AppTerminateCb(void *data);

private:
  enum class Action {
    kPrevious,
    kToday,
    kNext,
    kMonth,
    kWeek,
    kDay,
    kAgenda,
    kSearch,
    kSelectDate,
    kOpenEvent,
    kNewEvent,
    kOpenReminders,
    kClose,
    kEditEvent,
    kRequestDeleteEvent,
    kConfirmDeleteEvent,
    kCancelDeleteEvent,
    kSaveEvent,
    kApplySearch,
    kToggleSearchTitle,
    kToggleSearchLocation,
    kToggleSearchNote,
    kNewReminder,
    kEditReminder,
    kCompleteReminder,
    kSaveReminder,
    kRequestDeleteReminder,
    kConfirmDeleteReminder,
    kCancelDeleteReminder,
  };

  struct ActionContext {
    CalendarApp *app = nullptr;
    Action action = Action::kClose;
    std::string id;
    base::Date date;
  };

  struct RenderedEvent {
    Evas_Object *object = nullptr;
    domain::CalendarEvent event;
  };

  void StartProviders();
  void BuildWindow();
  void Render();
  void RenderCommandBar(Evas_Object *parent);
  void RenderCalendar(Evas_Object *parent);
  void RenderMonth(Evas_Object *parent,
                   const state::CalendarProjection &projection);
  void RenderWeek(Evas_Object *parent,
                  const state::CalendarProjection &projection);
  void RenderDay(Evas_Object *parent,
                 const state::CalendarProjection &projection);
  void RenderAgenda(Evas_Object *parent,
                    const state::CalendarProjection &projection);
  void RenderSearch(Evas_Object *parent);
  void RenderEventDetail(Evas_Object *parent);
  void RenderEventEditor(Evas_Object *parent);
  void RenderDeleteEventConfirmation(Evas_Object *parent);
  void RenderReminderList(Evas_Object *parent);
  void RenderReminderEditor(Evas_Object *parent);
  void RenderDeleteReminderConfirmation(Evas_Object *parent);
  Evas_Object *CreateOverlayPanel(Evas_Object *parent);

  Evas_Object *AddBox(Evas_Object *parent, bool horizontal,
                      double weight_x = EVAS_HINT_EXPAND,
                      double weight_y = EVAS_HINT_EXPAND);
  Evas_Object *AddLabel(Evas_Object *parent, const std::string &text,
                        double weight_x = 0.0);
  Evas_Object *AddButton(Evas_Object *parent, const std::string &text,
                         Action action, const std::string &id = "",
                         const base::Date &date = base::Date());
  Evas_Object *AddEntry(Evas_Object *parent, const std::string &guide,
                        const std::string &text, bool multiline = false);
  Evas_Object *AddScroller(Evas_Object *parent, Evas_Object *content);
  Evas_Object *AddEventButton(Evas_Object *parent,
                              const domain::CalendarEvent &event,
                              bool include_date);
  void AddSpacer(Evas_Object *parent, int minimum_height);
  void SetStatus(const std::string &message);
  void HandleAction(const ActionContext &context);
  void HandleBack();
  void SaveEventFromEditor();
  void ApplySearchFromEditor();
  void SaveReminderFromEditor();
  void PublishVisibleEvents();
  void RestoreFocus();
  std::vector<domain::CalendarEvent> SelectedEvents() const;
  std::optional<domain::CalendarEvent> SelectedEvent() const;
  static base::Date Today();
  static bool TryParseDate(const std::string &text, base::Date *date);
  static std::string EventTimeText(const domain::CalendarEvent &event,
                                   bool include_date);
  static std::string DateHeading(const base::Date &date);
  static std::string UniqueId(const char *prefix);

  static void OnAction(void *data, Evas_Object *object, void *event_info);
  static void OnButtonMouseUp(void *data, Evas *canvas, Evas_Object *object,
                              void *event_info);
  static void OnButtonFocusIn(void *data, Evas *canvas, Evas_Object *object,
                              void *event_info);
  static void OnButtonFocusOut(void *data, Evas *canvas, Evas_Object *object,
                               void *event_info);
  static void OnEventFocused(void *data, Evas_Object *object, void *event_info);
  static void OnWindowDelete(void *data, Evas_Object *object, void *event_info);
  static void OnWindowBack(void *data, Evas_Object *object, void *event_info);
  static void OnKeyDown(void *data, Evas *canvas, Evas_Object *object,
                        void *event_info);
  static Eina_Bool PublishIdle(void *data);

  Evas_Object *win_ = nullptr;
  std::string theme_path_;
  Evas_Object *conformant_ = nullptr;
  Evas_Object *root_ = nullptr;
  Evas_Object *status_label_ = nullptr;
  Evas_Object *title_entry_ = nullptr;
  Evas_Object *start_entry_ = nullptr;
  Evas_Object *end_entry_ = nullptr;
  Evas_Object *location_entry_ = nullptr;
  Evas_Object *note_entry_ = nullptr;
  Evas_Object *keyword_entry_ = nullptr;
  Evas_Object *search_start_entry_ = nullptr;
  Evas_Object *search_end_entry_ = nullptr;
  Evas_Object *reminder_title_entry_ = nullptr;
  Evas_Object *reminder_due_entry_ = nullptr;
  Evas_Object *reminder_note_entry_ = nullptr;
  Evas_Object *reminder_completed_check_ = nullptr;
  std::vector<Evas_Object *> reminder_offset_checks_;

  std::vector<std::unique_ptr<ActionContext>> callbacks_;
  std::vector<RenderedEvent> rendered_events_;
  std::optional<std::string> focus_restore_event_id_;
  std::string status_message_;

  std::unique_ptr<domain::CalendarEventRepository> events_;
  std::unique_ptr<domain::CalendarReminderRepository> reminders_;
  std::unique_ptr<persistence::CalendarJsonStore> persistence_;
  std::unique_ptr<usecases::ReminderAlarmScheduler> alarms_;
  std::unique_ptr<usecases::CalendarCommandService> commands_;
  std::unique_ptr<provider::CalendarServiceCore> calendar_core_;
  std::unique_ptr<provider::ScheduleServiceCore> schedule_core_;
  std::unique_ptr<provider::CalendarViewRegistry> view_registry_;
  std::unique_ptr<provider::CalendarViewServiceCore> view_core_;

  std::unique_ptr<rpc_port::calendar_action_provider::stub::TizenActionCalendar>
      calendar_provider_;
  std::unique_ptr<rpc_port::schedule_action_provider::stub::TizenActionSchedule>
      schedule_provider_;
  std::unique_ptr<rpc_port::view_action_provider::stub::TizenActionView>
      view_provider_;

  base::Date today_;
  state::CalendarInteractionState interaction_;
};

} // namespace ui
} // namespace calendar

#endif // CALENDAR_NATIVE_UI_CALENDAR_APP_HH_
