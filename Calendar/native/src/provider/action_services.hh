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

#ifndef CALENDAR_NATIVE_PROVIDER_ACTION_SERVICES_HH_
#define CALENDAR_NATIVE_PROVIDER_ACTION_SERVICES_HH_

#include <memory>
#include <string>

#include "provider/calendar_action_provider.h"
#include "provider/calendar_service_core.hh"
#include "provider/calendar_view_registry.hh"
#include "provider/schedule_action_provider.h"
#include "provider/view_action_provider.h"

namespace calendar {
namespace provider {

class CalendarActionService final : public rpc_port::calendar_action_provider::
                                        stub::TizenActionCalendar::ServiceBase {
public:
  CalendarActionService(std::string sender, std::string instance,
                        CalendarServiceCore *core);
  void OnCreate() override {}
  void OnTerminate() override {}

  rpc_port::calendar_action_provider::TizenEntityStatus AddEvent(
      rpc_port::calendar_action_provider::TizenEntityCalendar value) override;
  rpc_port::calendar_action_provider::TizenEntityStatus RemoveEvent(
      rpc_port::calendar_action_provider::TizenEntityCalendar value) override;
  rpc_port::calendar_action_provider::TizenEntityStatus
  Search(rpc_port::calendar_action_provider::TizenEntityQuery query,
         std::vector<rpc_port::calendar_action_provider::TizenEntityCalendar>
             &result) override;
  rpc_port::calendar_action_provider::TizenEntityStatus
  ToPresentation(rpc_port::calendar_action_provider::TizenEntityCalendar value,
                 rpc_port::calendar_action_provider::TizenEntityPresentation
                     &result) override;
  rpc_port::calendar_action_provider::TizenEntityStatus UpdateEvent(
      rpc_port::calendar_action_provider::TizenEntityCalendar value) override;
  rpc_port::calendar_action_provider::TizenEntityStatus GetEventByIds(
      std::vector<std::string> ids,
      std::vector<rpc_port::calendar_action_provider::TizenEntityCalendar>
          &result,
      std::vector<std::string> &unresolved_ids) override;
  rpc_port::calendar_action_provider::TizenEntityStatus SearchInPeriod(
      rpc_port::calendar_action_provider::TizenEntityCalendarSearchQuery query,
      std::vector<rpc_port::calendar_action_provider::TizenEntityCalendar>
          &result) override;

private:
  CalendarServiceCore *core_;
};

class CalendarActionServiceFactory final
    : public rpc_port::calendar_action_provider::stub::TizenActionCalendar::
          ServiceBase::Factory {
public:
  explicit CalendarActionServiceFactory(CalendarServiceCore *core)
      : core_(core) {}
  std::unique_ptr<rpc_port::calendar_action_provider::stub::
                      TizenActionCalendar::ServiceBase>
  CreateService(std::string sender, std::string instance) override;

private:
  CalendarServiceCore *core_;
};

class ScheduleActionService final : public rpc_port::schedule_action_provider::
                                        stub::TizenActionSchedule::ServiceBase {
public:
  ScheduleActionService(std::string sender, std::string instance,
                        ScheduleServiceCore *core);
  void OnCreate() override {}
  void OnTerminate() override {}

  rpc_port::schedule_action_provider::TizenEntityStatus
  AddRecording(rpc_port::schedule_action_provider::TizenEntityReservation value)
      override;
  rpc_port::schedule_action_provider::TizenEntityStatus
  AddViewing(rpc_port::schedule_action_provider::TizenEntityReservation value)
      override;
  rpc_port::schedule_action_provider::TizenEntityStatus CancelRecording(
      rpc_port::schedule_action_provider::TizenEntityReservation value)
      override;
  rpc_port::schedule_action_provider::TizenEntityStatus CancelViewing(
      rpc_port::schedule_action_provider::TizenEntityReservation value)
      override;
  rpc_port::schedule_action_provider::TizenEntityStatus CompleteReminder(
      rpc_port::schedule_action_provider::TizenEntityReminder value) override;
  rpc_port::schedule_action_provider::TizenEntityStatus CreateReminder(
      rpc_port::schedule_action_provider::TizenEntityReminder value) override;
  rpc_port::schedule_action_provider::TizenEntityStatus DeleteReminder(
      rpc_port::schedule_action_provider::TizenEntityReminder value) override;
  rpc_port::schedule_action_provider::TizenEntityStatus GetReservations(
      std::vector<rpc_port::schedule_action_provider::TizenEntityReservation>
          &result) override;
  rpc_port::schedule_action_provider::TizenEntityStatus SearchReminder(
      rpc_port::schedule_action_provider::TizenEntityQuery query,
      std::vector<rpc_port::schedule_action_provider::TizenEntityReminder>
          &result) override;
  rpc_port::schedule_action_provider::TizenEntityStatus UpdateReminder(
      rpc_port::schedule_action_provider::TizenEntityReminder value) override;

private:
  ScheduleServiceCore *core_;
};

class ScheduleActionServiceFactory final
    : public rpc_port::schedule_action_provider::stub::TizenActionSchedule::
          ServiceBase::Factory {
public:
  explicit ScheduleActionServiceFactory(ScheduleServiceCore *core)
      : core_(core) {}
  std::unique_ptr<rpc_port::schedule_action_provider::stub::
                      TizenActionSchedule::ServiceBase>
  CreateService(std::string sender, std::string instance) override;

private:
  ScheduleServiceCore *core_;
};

class ViewActionService final : public rpc_port::view_action_provider::stub::
                                    TizenActionView::ServiceBase {
public:
  ViewActionService(std::string sender, std::string instance,
                    CalendarViewServiceCore *core);
  void OnCreate() override {}
  void OnTerminate() override {}

  rpc_port::view_action_provider::TizenEntityStatus
  FindById(std::string id,
           rpc_port::view_action_provider::TizenEntityView &result) override;
  rpc_port::view_action_provider::TizenEntityStatus GetAnnotatedViews(
      std::vector<rpc_port::view_action_provider::TizenEntityView> &result)
      override;
  rpc_port::view_action_provider::TizenEntityStatus GetFocusedView(
      rpc_port::view_action_provider::TizenEntityView &result) override;
  rpc_port::view_action_provider::TizenEntityStatus ToPresentation(
      rpc_port::view_action_provider::TizenEntityView value,
      rpc_port::view_action_provider::TizenEntityPresentation &result) override;

private:
  CalendarViewServiceCore *core_;
};

class ViewActionServiceFactory final
    : public rpc_port::view_action_provider::stub::TizenActionView::
          ServiceBase::Factory {
public:
  explicit ViewActionServiceFactory(CalendarViewServiceCore *core)
      : core_(core) {}
  std::unique_ptr<
      rpc_port::view_action_provider::stub::TizenActionView::ServiceBase>
  CreateService(std::string sender, std::string instance) override;

private:
  CalendarViewServiceCore *core_;
};

} // namespace provider
} // namespace calendar

#endif // CALENDAR_NATIVE_PROVIDER_ACTION_SERVICES_HH_
