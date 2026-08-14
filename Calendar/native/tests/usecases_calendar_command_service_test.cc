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

#include "usecases/calendar_command_service.hh"

#include <cstdio>

#include "harness.hh"

namespace calendar {
namespace usecases {
namespace {

using ::calendar::base::OffsetDateTime;
using ::calendar::domain::CalendarEvent;
using ::calendar::domain::CalendarEventRepository;
using ::calendar::domain::CalendarReminder;
using ::calendar::domain::CalendarReminderRepository;
using ::calendar::persistence::CalendarJsonStore;
using ::calendar::persistence::CalendarStoreDocument;

OffsetDateTime At(int year, int month, int day, int hour, int minute) {
  return OffsetDateTime::FromLocalParts(base::Date(year, month, day), hour,
                                        minute, 0, 0, 9 * 60);
}

CalendarEvent MakeEvent(const std::string& id, const std::string& title) {
  CalendarEvent ev;
  std::string err;
  EXPECT_TRUE(CalendarEvent::TryCreate(id, title, At(2026, 8, 14, 9, 0),
                                       At(2026, 8, 14, 10, 0), "", "", &ev,
                                       &err));
  return ev;
}

class FakeAlarmScheduler : public ReminderAlarmScheduler {
 public:
  std::optional<int> Schedule(const CalendarReminder& reminder) override {
    scheduled_reminders.push_back(reminder.id());
    return ++next_alarm_id;
  }
  void Cancel(int alarm_id) override { cancelled_alarms.push_back(alarm_id); }

  int next_alarm_id = 0;
  std::vector<std::string> scheduled_reminders;
  std::vector<int> cancelled_alarms;
};

CALENDAR_TEST(command_service_create_event_saves_and_publishes) {
  const char* path = "test_store_cmd.json";
  std::remove(path);

  CalendarEventRepository events;
  CalendarReminderRepository reminders;
  CalendarJsonStore store(path);
  FakeAlarmScheduler alarms;
  CalendarCommandService service(&events, &reminders, &store, &alarms);

  CalendarEvent ev = MakeEvent("e1", "Sync");
  CalendarCommandResult res = service.CreateEvent(ev, {10, 30});
  EXPECT_TRUE(res.success);

  EXPECT_EQ(events.Snapshot().size(), 1u);
  EXPECT_EQ(reminders.Snapshot().size(), 2u);

  CalendarStoreDocument loaded = store.Load();
  EXPECT_EQ(loaded.events.size(), 1u);
  EXPECT_EQ(loaded.reminders.size(), 2u);

  EXPECT_EQ(alarms.scheduled_reminders.size(), 2u);
  EXPECT_TRUE(alarms.cancelled_alarms.empty());

  std::remove(path);
}

CALENDAR_TEST(command_service_delete_event_cleans_reminders_and_alarms) {
  const char* path = "test_store_cmd2.json";
  std::remove(path);

  CalendarEventRepository events;
  CalendarReminderRepository reminders;
  CalendarJsonStore store(path);
  FakeAlarmScheduler alarms;
  CalendarCommandService service(&events, &reminders, &store, &alarms);

  EXPECT_TRUE(service.CreateEvent(MakeEvent("e1", "Sync"), {10}).success);
  EXPECT_EQ(reminders.Snapshot().size(), 1u);
  EXPECT_EQ(alarms.scheduled_reminders.size(), 1u);
  EXPECT_TRUE(alarms.cancelled_alarms.empty());

  EXPECT_TRUE(service.DeleteEvent("e1").success);

  EXPECT_TRUE(events.Snapshot().empty());
  EXPECT_TRUE(reminders.Snapshot().empty());
  EXPECT_EQ(alarms.cancelled_alarms.size(), 1u);

  CalendarStoreDocument loaded = store.Load();
  EXPECT_TRUE(loaded.events.empty());
  EXPECT_TRUE(loaded.reminders.empty());

  std::remove(path);
}

}  // namespace
}  // namespace usecases
}  // namespace calendar
