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

#ifndef CALENDAR_NATIVE_PERSISTENCE_CALENDAR_JSON_STORE_HH_
#define CALENDAR_NATIVE_PERSISTENCE_CALENDAR_JSON_STORE_HH_

#include <string>
#include <vector>

#include "domain/calendar_event.hh"
#include "domain/calendar_reminder.hh"

namespace calendar {
namespace persistence {

struct CalendarStoreDocument {
  static constexpr int kCurrentSchemaVersion = 1;

  int schema_version = kCurrentSchemaVersion;
  std::vector<domain::CalendarEvent> events;
  std::vector<domain::CalendarReminder> reminders;
};

class CalendarJsonStore {
 public:
  explicit CalendarJsonStore(std::string path);

  CalendarStoreDocument Load() const;
  bool TrySave(const CalendarStoreDocument& document,
               std::string* error) const;

 private:
  std::string path_;
};

}  // namespace persistence
}  // namespace calendar

#endif  // CALENDAR_NATIVE_PERSISTENCE_CALENDAR_JSON_STORE_HH_
