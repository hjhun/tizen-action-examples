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

#include "provider/calendar_service_core.hh"

#include <cstdio>
#include <optional>
#include <string>
#include <unistd.h>
#include <vector>

#include "harness.hh"
#include "testing_factories.hh"

namespace calendar {
namespace {

class NoopAlarmScheduler final : public usecases::ReminderAlarmScheduler {
public:
  std::optional<int>
  Schedule(const domain::CalendarReminder &reminder) override {
    (void)reminder;
    return std::nullopt;
  }

  void Cancel(int alarm_id) override { (void)alarm_id; }
};

std::string StorePath(const std::string &suffix) {
  return "/tmp/calendar-native-provider-" + std::to_string(getpid()) + "-" +
         suffix + ".json";
}

provider::CalendarWireEvent WireEvent(const std::string &id,
                                      const std::string &title) {
  return {id,
          title,
          "2026-08-24T14:00:00+09:00",
          "2026-08-24T15:00:00+09:00",
          "provider note",
          "Studio"};
}

CALENDAR_TEST(calendar_service_core_mutates_the_shared_repository) {
  const std::string path = StorePath("calendar");
  std::remove(path.c_str());
  domain::CalendarEventRepository events;
  domain::CalendarReminderRepository reminders;
  persistence::CalendarJsonStore store(path);
  NoopAlarmScheduler alarms;
  usecases::CalendarCommandService commands(&events, &reminders, &store,
                                            &alarms);
  provider::CalendarServiceCore service(&events, &commands);

  auto added = service.AddEvent(WireEvent("event-2", "Provider event"));
  EXPECT_TRUE(added.success);
  EXPECT_EQ(testing::Ids(events.ResolveByIds({"event-2"}).events), "event-2");

  auto search = service.SearchInPeriod({"provider", "2026-08-24T00:00:00+09:00",
                                        "2026-08-25T00:00:00+09:00", 20, true,
                                        false, false});
  EXPECT_TRUE(search.status.success);
  EXPECT_EQ(search.events.size(), static_cast<std::size_t>(1));
  EXPECT_EQ(search.events[0].id, "event-2");

  auto resolved = service.GetEventByIds({"event-2", "missing", "event-2"});
  EXPECT_TRUE(resolved.status.success);
  EXPECT_EQ(resolved.events.size(), static_cast<std::size_t>(2));
  EXPECT_EQ(resolved.unresolved_ids.size(), static_cast<std::size_t>(1));
  EXPECT_EQ(resolved.unresolved_ids[0], "missing");

  provider::PresentationData presentation;
  auto presented = service.ToPresentation(
      WireEvent("event-2", "Provider event"), &presentation);
  EXPECT_TRUE(presented.success);
  EXPECT_EQ(presentation.template_value, "calendar-event-card-v1");
  EXPECT_TRUE(presentation.document.find("\"id\":\"event-2\"") !=
              std::string::npos);
  std::remove(path.c_str());
}

CALENDAR_TEST(calendar_service_core_rejects_unavailable_mutation_and_bounds) {
  domain::CalendarEventRepository events;
  provider::CalendarServiceCore read_only(&events, nullptr);

  EXPECT_FALSE(read_only.AddEvent(WireEvent("event-1", "Event")).success);
  EXPECT_FALSE(read_only.GetEventByIds(std::vector<std::string>(101, "id"))
                   .status.success);
  EXPECT_FALSE(
      read_only.AddEvent(WireEvent(std::string(257, 'x'), "Event")).success);
  EXPECT_TRUE(read_only.Search("", 20).status.success);
  EXPECT_FALSE(read_only.Search(std::string(513, 'x'), 20).status.success);
  EXPECT_TRUE(read_only.Search("event", 1000).status.success);
}

CALENDAR_TEST(schedule_service_core_crud_uses_the_shared_command_service) {
  const std::string path = StorePath("schedule");
  std::remove(path.c_str());
  domain::CalendarEventRepository events;
  domain::CalendarReminderRepository reminders;
  persistence::CalendarJsonStore store(path);
  NoopAlarmScheduler alarms;
  usecases::CalendarCommandService commands(&events, &reminders, &store,
                                            &alarms);
  provider::ScheduleServiceCore service(&reminders, &commands);
  provider::ReminderWireEntity reminder{"reminder-1", "Call the team",
                                        "2026-08-24T16:00:00+09:00",
                                        "Bring the report", false};

  EXPECT_TRUE(service.CreateReminder(reminder).success);
  auto found = service.SearchReminder("team", 20);
  EXPECT_TRUE(found.status.success);
  EXPECT_EQ(found.reminders.size(), static_cast<std::size_t>(1));
  EXPECT_TRUE(service.CompleteReminder("reminder-1").success);
  EXPECT_TRUE(reminders.Find("reminder-1")->is_completed());
  EXPECT_TRUE(service.DeleteReminder("reminder-1").success);
  EXPECT_FALSE(reminders.Find("reminder-1").has_value());
  EXPECT_FALSE(service.GetReservations().status.success);
  std::remove(path.c_str());
}

} // namespace
} // namespace calendar
