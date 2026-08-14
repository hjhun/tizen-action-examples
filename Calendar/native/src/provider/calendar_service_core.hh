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

#ifndef CALENDAR_NATIVE_PROVIDER_CALENDAR_SERVICE_CORE_HH_
#define CALENDAR_NATIVE_PROVIDER_CALENDAR_SERVICE_CORE_HH_

#include <string>
#include <vector>

#include "domain/calendar_event_repository.hh"
#include "domain/calendar_reminder_repository.hh"
#include "usecases/calendar_command_service.hh"

namespace calendar {
namespace provider {

struct ServiceStatus {
  bool success = false;
  std::string reason;
};

struct CalendarWireEvent {
  std::string id;
  std::string title;
  std::string start_date;
  std::string end_date;
  std::string note;
  std::string location;
};

struct CalendarWireSearch {
  std::string keyword;
  std::string start_date;
  std::string end_date;
  int number = 20;
  bool search_title = true;
  bool search_location = true;
  bool search_note = true;
};

struct CalendarSearchResult {
  ServiceStatus status;
  std::vector<CalendarWireEvent> events;
};

struct CalendarResolveResult : CalendarSearchResult {
  std::vector<std::string> unresolved_ids;
};

struct PresentationData {
  std::string template_value;
  std::string document;
};

class CalendarServiceCore {
public:
  CalendarServiceCore(domain::CalendarEventRepository *repository,
                      usecases::CalendarCommandService *commands);

  ServiceStatus AddEvent(const CalendarWireEvent &event);
  ServiceStatus UpdateEvent(const CalendarWireEvent &event);
  ServiceStatus RemoveEvent(const std::string &id);
  CalendarSearchResult Search(const std::string &keyword, int number) const;
  CalendarSearchResult SearchInPeriod(const CalendarWireSearch &query) const;
  CalendarResolveResult
  GetEventByIds(const std::vector<std::string> &ids) const;
  ServiceStatus ToPresentation(const CalendarWireEvent &event,
                               PresentationData *presentation) const;

private:
  domain::CalendarEventRepository *repository_;
  usecases::CalendarCommandService *commands_;
};

struct ReminderWireEntity {
  std::string id;
  std::string title;
  std::string due_date;
  std::string note;
  bool completed = false;
};

struct ReminderSearchResult {
  ServiceStatus status;
  std::vector<ReminderWireEntity> reminders;
};

struct ReservationResult {
  ServiceStatus status;
};

class ScheduleServiceCore {
public:
  ScheduleServiceCore(domain::CalendarReminderRepository *repository,
                      usecases::CalendarCommandService *commands);

  ServiceStatus CreateReminder(const ReminderWireEntity &reminder);
  ServiceStatus UpdateReminder(const ReminderWireEntity &reminder);
  ServiceStatus CompleteReminder(const std::string &id);
  ServiceStatus DeleteReminder(const std::string &id);
  ReminderSearchResult SearchReminder(const std::string &keyword,
                                      int number) const;
  ReservationResult GetReservations() const;
  ServiceStatus UnsupportedReservation() const;

private:
  domain::CalendarReminderRepository *repository_;
  usecases::CalendarCommandService *commands_;
};

} // namespace provider
} // namespace calendar

#endif // CALENDAR_NATIVE_PROVIDER_CALENDAR_SERVICE_CORE_HH_
