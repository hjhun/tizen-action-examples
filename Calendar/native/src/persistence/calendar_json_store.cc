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

#include <fstream>
#include <sstream>

#include "base/json.hh"
#include "base/offset_date_time.hh"

namespace calendar {
namespace persistence {

CalendarJsonStore::CalendarJsonStore(std::string path) : path_(std::move(path)) {}

CalendarStoreDocument CalendarJsonStore::Load() const {
  CalendarStoreDocument doc;
  std::ifstream file(path_);
  if (!file.is_open()) return doc;

  std::ostringstream ss;
  ss << file.rdbuf();
  std::string json_text = ss.str();
  if (json_text.empty()) return doc;

  base::JsonValue root;
  if (!base::JsonValue::Parse(json_text, &root) || !root.IsObject()) {
    return doc;
  }

  const base::JsonValue* schema_node = root.Find("SchemaVersion");
  if (!schema_node || !schema_node->IsNumber() ||
      schema_node->AsDouble() != CalendarStoreDocument::kCurrentSchemaVersion) {
    return doc;
  }

  doc.schema_version = static_cast<int>(schema_node->AsDouble());

  const base::JsonValue* events_node = root.Find("Events");
  if (events_node && events_node->IsArray()) {
    for (const auto& elem : events_node->Elements()) {
      if (!elem.IsObject()) continue;
      std::string id = elem.StringOr("Id", "");
      std::string title = elem.StringOr("Title", "");
      std::string start_str = elem.StringOr("Start", "");
      std::string end_str = elem.StringOr("End", "");
      std::string note = elem.StringOr("Note", "");
      std::string location = elem.StringOr("Location", "");

      base::OffsetDateTime start, end;
      if (!base::OffsetDateTime::TryParseFlexible(start_str, &start) ||
          !base::OffsetDateTime::TryParseFlexible(end_str, &end)) {
        continue;
      }

      domain::CalendarEvent event;
      std::string err;
      if (domain::CalendarEvent::TryCreate(id, title, start, end, note,
                                           location, &event, &err)) {
        doc.events.push_back(event);
      }
    }
  }

  const base::JsonValue* rem_node = root.Find("Reminders");
  if (rem_node && rem_node->IsArray()) {
    for (const auto& elem : rem_node->Elements()) {
      if (!elem.IsObject()) continue;
      std::string id = elem.StringOr("Id", "");
      std::string title = elem.StringOr("Title", "");
      std::string due_at_str = elem.StringOr("DueAt", "");
      std::string note = elem.StringOr("Note", "");

      const base::JsonValue* completed_node = elem.Find("IsCompleted");
      bool is_completed = completed_node && completed_node->AsBool();

      base::OffsetDateTime due_at;
      if (!base::OffsetDateTime::TryParseFlexible(due_at_str, &due_at)) {
        continue;
      }

      domain::CalendarReminder reminder;
      std::string err;

      const base::JsonValue* event_id_node = elem.Find("CalendarEventId");
      if (event_id_node && event_id_node->IsString()) {
        std::string event_id = event_id_node->AsString();
        int offset = 0;
        const base::JsonValue* offset_node = elem.Find("OffsetMinutes");
        if (offset_node && offset_node->IsNumber()) {
          offset = static_cast<int>(offset_node->AsDouble());
        }

        base::OffsetDateTime event_start = due_at.AddMinutes(offset);
        if (!domain::CalendarReminder::TryCreateForEvent(
                id, title, event_start, event_id, offset, note, &reminder,
                &err)) {
          continue;
        }
      } else {
        if (!domain::CalendarReminder::TryCreate(id, title, due_at, note,
                                                 &reminder, &err)) {
          continue;
        }
      }

      if (is_completed) {
        reminder = reminder.WithCompleted(true);
      }

      const base::JsonValue* alarm_node = elem.Find("AlarmId");
      if (alarm_node && alarm_node->IsNumber()) {
        reminder = reminder.WithAlarmId(static_cast<int>(alarm_node->AsDouble()));
      }

      doc.reminders.push_back(reminder);
    }
  }
  return doc;
}

bool CalendarJsonStore::TrySave(const CalendarStoreDocument& document,
                                std::string* error) const {
  if (document.schema_version != CalendarStoreDocument::kCurrentSchemaVersion) {
    if (error) *error = "Unsupported schema version.";
    return false;
  }

  base::JsonValue root = base::JsonValue::Object();
  root.Set("SchemaVersion", base::JsonValue::Number(document.schema_version));

  base::JsonValue events_array = base::JsonValue::Array();
  for (const auto& ev : document.events) {
    base::JsonValue obj = base::JsonValue::Object();
    obj.Set("Id", base::JsonValue::String(ev.id()));
    obj.Set("Title", base::JsonValue::String(ev.title()));
    obj.Set("Start", base::JsonValue::String(ev.start().ToRoundTripString()));
    obj.Set("End", base::JsonValue::String(ev.end().ToRoundTripString()));
    if (!ev.note().empty()) {
      obj.Set("Note", base::JsonValue::String(ev.note()));
    } else {
      obj.Set("Note", base::JsonValue::Null());
    }
    if (!ev.location().empty()) {
      obj.Set("Location", base::JsonValue::String(ev.location()));
    } else {
      obj.Set("Location", base::JsonValue::Null());
    }
    events_array.Append(obj);
  }
  root.Set("Events", events_array);

  base::JsonValue rem_array = base::JsonValue::Array();
  for (const auto& rem : document.reminders) {
    base::JsonValue obj = base::JsonValue::Object();
    obj.Set("Id", base::JsonValue::String(rem.id()));
    if (rem.calendar_event_id().has_value()) {
      obj.Set("CalendarEventId", base::JsonValue::String(*rem.calendar_event_id()));
    } else {
      obj.Set("CalendarEventId", base::JsonValue::Null());
    }
    obj.Set("Title", base::JsonValue::String(rem.title()));
    obj.Set("DueAt", base::JsonValue::String(rem.due_at().ToRoundTripString()));
    if (rem.offset_minutes().has_value()) {
      obj.Set("OffsetMinutes", base::JsonValue::Number(*rem.offset_minutes()));
    } else {
      obj.Set("OffsetMinutes", base::JsonValue::Null());
    }
    obj.Set("IsCompleted", base::JsonValue::Bool(rem.is_completed()));
    if (rem.alarm_id().has_value()) {
      obj.Set("AlarmId", base::JsonValue::Number(*rem.alarm_id()));
    } else {
      obj.Set("AlarmId", base::JsonValue::Null());
    }
    if (!rem.note().empty()) {
      obj.Set("Note", base::JsonValue::String(rem.note()));
    } else {
      obj.Set("Note", base::JsonValue::Null());
    }
    rem_array.Append(obj);
  }
  root.Set("Reminders", rem_array);

  std::string json_text = root.ToString();

  std::string tmp_path = path_ + ".tmp";
  std::ofstream out(tmp_path);
  if (!out.is_open()) {
    if (error) *error = "Failed to open temporary file for writing.";
    return false;
  }
  out << json_text;
  out.close();

  if (std::rename(tmp_path.c_str(), path_.c_str()) != 0) {
    if (error) *error = "Failed to rename temporary file.";
    return false;
  }

  if (error) error->clear();
  return true;
}

}  // namespace persistence
}  // namespace calendar
