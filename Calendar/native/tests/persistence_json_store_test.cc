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

#include "persistence/calendar_json_store.hh"

#include <cstdio>
#include <fstream>

#include "harness.hh"

namespace calendar {
namespace persistence {
namespace {

using ::calendar::base::OffsetDateTime;
using ::calendar::domain::CalendarEvent;
using ::calendar::domain::CalendarReminder;

OffsetDateTime At(int year, int month, int day, int hour, int minute) {
  return OffsetDateTime::FromLocalParts(base::Date(year, month, day), hour,
                                        minute, 0, 0, 9 * 60);
}

CalendarEvent MakeEvent(const std::string& id, const std::string& title) {
  CalendarEvent ev;
  std::string err;
  EXPECT_TRUE(CalendarEvent::TryCreate(
      id, title, At(2026, 8, 14, 9, 0), At(2026, 8, 14, 10, 0), "Note",
      "Location", &ev, &err));
  return ev;
}

CalendarReminder MakeReminder(const std::string& id, const std::string& title) {
  CalendarReminder rem;
  std::string err;
  EXPECT_TRUE(CalendarReminder::TryCreate(
      id, title, At(2026, 8, 14, 8, 0), "Note", &rem, &err));
  return rem;
}

CALENDAR_TEST(json_store_loads_empty_when_file_missing) {
  CalendarJsonStore store("missing.json");
  CalendarStoreDocument doc = store.Load();
  EXPECT_EQ(doc.schema_version, 1);
  EXPECT_TRUE(doc.events.empty());
  EXPECT_TRUE(doc.reminders.empty());
}

CALENDAR_TEST(json_store_round_trips_document) {
  const char* path = "test_store.json";
  std::remove(path);

  CalendarJsonStore store(path);
  CalendarStoreDocument doc;
  doc.events.push_back(MakeEvent("e1", "Event 1"));
  doc.reminders.push_back(MakeReminder("r1", "Reminder 1"));

  std::string err;
  EXPECT_TRUE(store.TrySave(doc, &err));

  CalendarStoreDocument loaded = store.Load();
  EXPECT_EQ(loaded.schema_version, 1);
  EXPECT_EQ(loaded.events.size(), 1u);
  EXPECT_EQ(loaded.events[0].id(), std::string("e1"));
  EXPECT_EQ(loaded.events[0].title(), std::string("Event 1"));

  EXPECT_EQ(loaded.reminders.size(), 1u);
  EXPECT_EQ(loaded.reminders[0].id(), std::string("r1"));
  EXPECT_EQ(loaded.reminders[0].title(), std::string("Reminder 1"));

  std::remove(path);
}

CALENDAR_TEST(json_store_ignores_unsupported_schema) {
  const char* path = "test_store.json";
  std::ofstream out(path);
  out << "{\"SchemaVersion\": 99}";
  out.close();

  CalendarJsonStore store(path);
  CalendarStoreDocument loaded = store.Load();
  EXPECT_EQ(loaded.schema_version, 1);
  EXPECT_TRUE(loaded.events.empty());

  std::remove(path);
}

}  // namespace
}  // namespace persistence
}  // namespace calendar
