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

#ifndef CALENDAR_NATIVE_USECASES_CALENDAR_COMMAND_SERVICE_HH_
#define CALENDAR_NATIVE_USECASES_CALENDAR_COMMAND_SERVICE_HH_

#include <mutex>
#include <optional>
#include <string>
#include <vector>

#include "domain/calendar_event_repository.hh"
#include "domain/calendar_reminder_repository.hh"
#include "persistence/calendar_json_store.hh"

namespace calendar {
namespace usecases {

struct CalendarCommandResult {
  bool success;
  std::string reason;

  static CalendarCommandResult Succeeded() { return {true, ""}; }
  static CalendarCommandResult Failed(std::string reason) {
    return {false, std::move(reason)};
  }
};

class ReminderAlarmScheduler {
 public:
  virtual ~ReminderAlarmScheduler() = default;
  virtual std::optional<int> Schedule(
      const domain::CalendarReminder& reminder) = 0;
  virtual void Cancel(int alarm_id) = 0;
};

class CalendarCommandService {
 public:
  CalendarCommandService(domain::CalendarEventRepository* events,
                         domain::CalendarReminderRepository* reminders,
                         persistence::CalendarJsonStore* persistence,
                         ReminderAlarmScheduler* alarms);

  CalendarCommandResult CreateEvent(
      const domain::CalendarEvent& calendar_event,
      const std::vector<int>& reminder_offsets);
  CalendarCommandResult Restore();
  CalendarCommandResult UpdateEvent(
      const domain::CalendarEvent& calendar_event,
      const std::vector<int>& reminder_offsets);
  CalendarCommandResult DeleteEvent(const std::string& event_id);

  CalendarCommandResult CreateReminder(
      const domain::CalendarReminder& reminder);
  CalendarCommandResult UpdateReminder(
      const domain::CalendarReminder& reminder);
  CalendarCommandResult SetReminderCompleted(const std::string& reminder_id,
                                             bool is_completed);
  CalendarCommandResult DeleteReminder(const std::string& reminder_id);

 private:
  std::mutex gate_;
  domain::CalendarEventRepository* events_;
  domain::CalendarReminderRepository* reminders_;
  persistence::CalendarJsonStore* persistence_;
  ReminderAlarmScheduler* alarms_;

  static bool NormalizeOffsets(const std::vector<int>& reminder_offsets,
                               std::vector<int>* offsets, std::string* error);
  static std::string LinkedReminderId(const std::string& event_id,
                                      int offset_minutes);
  void TryCancel(int alarm_id);
};

}  // namespace usecases
}  // namespace calendar

#endif  // CALENDAR_NATIVE_USECASES_CALENDAR_COMMAND_SERVICE_HH_
