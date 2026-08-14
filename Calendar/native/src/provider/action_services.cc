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

#include "provider/action_services.hh"

namespace calendar {
namespace provider {
namespace {

namespace calendar_rpc = rpc_port::calendar_action_provider;
namespace schedule_rpc = rpc_port::schedule_action_provider;
namespace view_rpc = rpc_port::view_action_provider;

calendar_rpc::TizenEntityStatus CalendarStatus(const ServiceStatus &status) {
  return {status.success, status.reason};
}

schedule_rpc::TizenEntityStatus ScheduleStatus(const ServiceStatus &status) {
  return {status.success, status.reason};
}

view_rpc::TizenEntityStatus ViewStatus(const ServiceStatus &status) {
  return {status.success, status.reason};
}

CalendarWireEvent FromEntity(const calendar_rpc::TizenEntityCalendar &value) {
  return {value.GetId(),      value.GetTitle(), value.GetStartDate(),
          value.GetEndDate(), value.GetNote(),  value.GetLocation()};
}

calendar_rpc::TizenEntityCalendar ToEntity(const CalendarWireEvent &value) {
  return {value.id,       "",         value.title,   value.start_date,
          value.end_date, value.note, value.location};
}

ReminderWireEntity FromEntity(const schedule_rpc::TizenEntityReminder &value) {
  return {value.GetId(), value.GetTitle(), value.GetDueDate(), value.GetNote(),
          value.GetCompleted()};
}

schedule_rpc::TizenEntityReminder ToEntity(const ReminderWireEntity &value) {
  return {value.id,       "",         value.title,
          value.due_date, value.note, value.completed};
}

view_rpc::TizenEntityView ToEntity(const CalendarAnnotatedView &value) {
  return {value.id,
          "",
          value.type,
          value.description,
          {value.screen_bounds.x, value.screen_bounds.y,
           value.screen_bounds.width, value.screen_bounds.height},
          {value.window_bounds.x, value.window_bounds.y,
           value.window_bounds.width, value.window_bounds.height},
          value.is_focused,
          value.is_enabled,
          {value.annotation.entity_id, value.annotation.entity_type,
           value.annotation.entity_info}};
}

CalendarAnnotatedView FromEntity(const view_rpc::TizenEntityView &value) {
  const auto &screen = value.GetScreenBounds();
  const auto &window = value.GetWindowBounds();
  const auto &annotation = value.GetAnnotation();
  return {value.GetId(),
          value.GetType(),
          value.GetDescription(),
          {screen.GetX(), screen.GetY(), screen.GetWidth(), screen.GetHeight()},
          {window.GetX(), window.GetY(), window.GetWidth(), window.GetHeight()},
          value.GetIsFocused(),
          value.GetIsEnabled(),
          {annotation.GetEntityId(), annotation.GetEntityType(),
           annotation.GetEntityInfo()}};
}

} // namespace

CalendarActionService::CalendarActionService(std::string sender,
                                             std::string instance,
                                             CalendarServiceCore *core)
    : ServiceBase(std::move(sender), std::move(instance)), core_(core) {}

calendar_rpc::TizenEntityStatus
CalendarActionService::AddEvent(calendar_rpc::TizenEntityCalendar value) {
  return CalendarStatus(core_->AddEvent(FromEntity(value)));
}

calendar_rpc::TizenEntityStatus
CalendarActionService::RemoveEvent(calendar_rpc::TizenEntityCalendar value) {
  return CalendarStatus(core_->RemoveEvent(value.GetId()));
}

calendar_rpc::TizenEntityStatus CalendarActionService::Search(
    calendar_rpc::TizenEntityQuery query,
    std::vector<calendar_rpc::TizenEntityCalendar> &result) {
  auto response = core_->Search(query.GetKeyword(), query.GetNumber());
  for (const auto &value : response.events)
    result.push_back(ToEntity(value));
  return CalendarStatus(response.status);
}

calendar_rpc::TizenEntityStatus CalendarActionService::ToPresentation(
    calendar_rpc::TizenEntityCalendar value,
    calendar_rpc::TizenEntityPresentation &result) {
  PresentationData presentation;
  auto status = core_->ToPresentation(FromEntity(value), &presentation);
  if (status.success) {
    result.SetTemplate(presentation.template_value);
    result.SetDocument(presentation.document);
  }
  return CalendarStatus(status);
}

calendar_rpc::TizenEntityStatus
CalendarActionService::UpdateEvent(calendar_rpc::TizenEntityCalendar value) {
  return CalendarStatus(core_->UpdateEvent(FromEntity(value)));
}

calendar_rpc::TizenEntityStatus CalendarActionService::GetEventByIds(
    std::vector<std::string> ids,
    std::vector<calendar_rpc::TizenEntityCalendar> &result,
    std::vector<std::string> &unresolved_ids) {
  auto response = core_->GetEventByIds(ids);
  for (const auto &value : response.events)
    result.push_back(ToEntity(value));
  unresolved_ids = std::move(response.unresolved_ids);
  return CalendarStatus(response.status);
}

calendar_rpc::TizenEntityStatus CalendarActionService::SearchInPeriod(
    calendar_rpc::TizenEntityCalendarSearchQuery query,
    std::vector<calendar_rpc::TizenEntityCalendar> &result) {
  auto response = core_->SearchInPeriod(
      {query.GetKeyword(), query.GetStartDate(), query.GetEndDate(),
       query.GetNumber(), query.GetSearchTitle(), query.GetSearchLocation(),
       query.GetSearchNote()});
  for (const auto &value : response.events)
    result.push_back(ToEntity(value));
  return CalendarStatus(response.status);
}

std::unique_ptr<calendar_rpc::stub::TizenActionCalendar::ServiceBase>
CalendarActionServiceFactory::CreateService(std::string sender,
                                            std::string instance) {
  return std::make_unique<CalendarActionService>(std::move(sender),
                                                 std::move(instance), core_);
}

ScheduleActionService::ScheduleActionService(std::string sender,
                                             std::string instance,
                                             ScheduleServiceCore *core)
    : ServiceBase(std::move(sender), std::move(instance)), core_(core) {}

schedule_rpc::TizenEntityStatus ScheduleActionService::AddRecording(
    schedule_rpc::TizenEntityReservation value) {
  (void)value;
  return ScheduleStatus(core_->UnsupportedReservation());
}

schedule_rpc::TizenEntityStatus
ScheduleActionService::AddViewing(schedule_rpc::TizenEntityReservation value) {
  (void)value;
  return ScheduleStatus(core_->UnsupportedReservation());
}

schedule_rpc::TizenEntityStatus ScheduleActionService::CancelRecording(
    schedule_rpc::TizenEntityReservation value) {
  (void)value;
  return ScheduleStatus(core_->UnsupportedReservation());
}

schedule_rpc::TizenEntityStatus ScheduleActionService::CancelViewing(
    schedule_rpc::TizenEntityReservation value) {
  (void)value;
  return ScheduleStatus(core_->UnsupportedReservation());
}

schedule_rpc::TizenEntityStatus ScheduleActionService::CompleteReminder(
    schedule_rpc::TizenEntityReminder value) {
  return ScheduleStatus(core_->CompleteReminder(value.GetId()));
}

schedule_rpc::TizenEntityStatus
ScheduleActionService::CreateReminder(schedule_rpc::TizenEntityReminder value) {
  return ScheduleStatus(core_->CreateReminder(FromEntity(value)));
}

schedule_rpc::TizenEntityStatus
ScheduleActionService::DeleteReminder(schedule_rpc::TizenEntityReminder value) {
  return ScheduleStatus(core_->DeleteReminder(value.GetId()));
}

schedule_rpc::TizenEntityStatus ScheduleActionService::GetReservations(
    std::vector<schedule_rpc::TizenEntityReservation> &result) {
  result.clear();
  return ScheduleStatus(core_->GetReservations().status);
}

schedule_rpc::TizenEntityStatus ScheduleActionService::SearchReminder(
    schedule_rpc::TizenEntityQuery query,
    std::vector<schedule_rpc::TizenEntityReminder> &result) {
  auto response = core_->SearchReminder(query.GetKeyword(), query.GetNumber());
  for (const auto &value : response.reminders) {
    result.push_back(ToEntity(value));
  }
  return ScheduleStatus(response.status);
}

schedule_rpc::TizenEntityStatus
ScheduleActionService::UpdateReminder(schedule_rpc::TizenEntityReminder value) {
  return ScheduleStatus(core_->UpdateReminder(FromEntity(value)));
}

std::unique_ptr<schedule_rpc::stub::TizenActionSchedule::ServiceBase>
ScheduleActionServiceFactory::CreateService(std::string sender,
                                            std::string instance) {
  return std::make_unique<ScheduleActionService>(std::move(sender),
                                                 std::move(instance), core_);
}

ViewActionService::ViewActionService(std::string sender, std::string instance,
                                     CalendarViewServiceCore *core)
    : ServiceBase(std::move(sender), std::move(instance)), core_(core) {}

view_rpc::TizenEntityStatus
ViewActionService::FindById(std::string id, view_rpc::TizenEntityView &result) {
  CalendarAnnotatedView view;
  auto status = core_->FindById(id, &view);
  if (status.success)
    result = ToEntity(view);
  return ViewStatus(status);
}

view_rpc::TizenEntityStatus ViewActionService::GetAnnotatedViews(
    std::vector<view_rpc::TizenEntityView> &result) {
  for (const auto &view : core_->GetAnnotatedViews()) {
    result.push_back(ToEntity(view));
  }
  return ViewStatus({true, ""});
}

view_rpc::TizenEntityStatus
ViewActionService::GetFocusedView(view_rpc::TizenEntityView &result) {
  CalendarAnnotatedView view;
  auto status = core_->GetFocusedView(&view);
  if (status.success)
    result = ToEntity(view);
  return ViewStatus(status);
}

view_rpc::TizenEntityStatus
ViewActionService::ToPresentation(view_rpc::TizenEntityView value,
                                  view_rpc::TizenEntityPresentation &result) {
  PresentationData presentation;
  auto status = core_->ToPresentation(FromEntity(value), &presentation);
  if (status.success) {
    result.SetTemplate(presentation.template_value);
    result.SetDocument(presentation.document);
  }
  return ViewStatus(status);
}

std::unique_ptr<view_rpc::stub::TizenActionView::ServiceBase>
ViewActionServiceFactory::CreateService(std::string sender,
                                        std::string instance) {
  return std::make_unique<ViewActionService>(std::move(sender),
                                             std::move(instance), core_);
}

} // namespace provider
} // namespace calendar
