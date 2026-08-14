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

#include "base/json.hh"

#include <string>

#include "harness.hh"

namespace {

using ::calendar::base::JsonValue;

CALENDAR_TEST(json_parses_nested_object) {
  JsonValue parsed;
  EXPECT_TRUE(JsonValue::Parse(
      R"({"TizenEntityCalendar":{"Id":"event-001","Number":7,"Ok":true}})",
      &parsed));
  EXPECT_TRUE(parsed.IsObject());

  const JsonValue* entity = parsed.Find("TizenEntityCalendar");
  EXPECT_TRUE(entity != nullptr);
  EXPECT_EQ(entity->StringOr("Id", ""), std::string("event-001"));
  EXPECT_EQ(entity->Find("Number")->AsDouble(), 7.0);
  EXPECT_TRUE(entity->Find("Ok")->AsBool());
  EXPECT_TRUE(entity->Find("Missing") == nullptr);
}

CALENDAR_TEST(json_parses_arrays_and_scalars) {
  JsonValue parsed;
  EXPECT_TRUE(JsonValue::Parse(R"([1,"two",true,false,null,{"a":[]}])",
                               &parsed));
  EXPECT_TRUE(parsed.IsArray());
  EXPECT_EQ(parsed.Elements().size(), static_cast<std::size_t>(6));
  EXPECT_EQ(parsed.Elements()[1].AsString(), std::string("two"));
  EXPECT_TRUE(parsed.Elements()[2].AsBool());
  EXPECT_TRUE(parsed.Elements()[4].IsNull());
  EXPECT_TRUE(parsed.Elements()[5].Find("a")->IsArray());
}

CALENDAR_TEST(json_unescapes_string_escapes) {
  JsonValue parsed;
  EXPECT_TRUE(JsonValue::Parse(R"({"s":"a\"b\\c\nd\teéA"})",
                               &parsed));
  EXPECT_EQ(parsed.StringOr("s", ""),
            std::string("a\"b\\c\nd\te\xc3\xa9"
                        "A"));
}

CALENDAR_TEST(json_rejects_malformed_input) {
  JsonValue parsed;
  EXPECT_FALSE(JsonValue::Parse("", &parsed));
  EXPECT_FALSE(JsonValue::Parse("{", &parsed));
  EXPECT_FALSE(JsonValue::Parse(R"({"a":})", &parsed));
  EXPECT_FALSE(JsonValue::Parse(R"({"a":1,})", &parsed));
  EXPECT_FALSE(JsonValue::Parse(R"({"a" 1})", &parsed));
  EXPECT_FALSE(JsonValue::Parse("[1,2", &parsed));
  EXPECT_FALSE(JsonValue::Parse("tru", &parsed));
  EXPECT_FALSE(JsonValue::Parse(R"({"a":1} trailing)", &parsed));
}

CALENDAR_TEST(json_rejects_input_deeper_than_the_bound) {
  std::string deep;
  for (int index = 0; index < 200; ++index) deep += "[";
  for (int index = 0; index < 200; ++index) deep += "]";

  JsonValue parsed;
  EXPECT_FALSE(JsonValue::Parse(deep, &parsed));
}

CALENDAR_TEST(json_serializes_objects_in_insertion_order) {
  JsonValue root = JsonValue::Object();
  root.Set("id", JsonValue::String("event-001"));
  root.Set("title", JsonValue::String("Design \"review\"\n"));
  root.Set("count", JsonValue::Number(3));
  root.Set("done", JsonValue::Bool(false));

  JsonValue tags = JsonValue::Array();
  tags.Append(JsonValue::String("a"));
  tags.Append(JsonValue::Null());
  root.Set("tags", std::move(tags));

  EXPECT_EQ(root.ToString(),
            std::string(R"({"id":"event-001",)"
                        R"("title":"Design \"review\"\n",)"
                        R"("count":3,"done":false,"tags":["a",null]})"));
}

CALENDAR_TEST(json_serializes_integral_numbers_without_a_fraction) {
  JsonValue root = JsonValue::Object();
  root.Set("whole", JsonValue::Number(42));
  root.Set("negative", JsonValue::Number(-7));
  root.Set("fraction", JsonValue::Number(0.5));
  EXPECT_EQ(root.ToString(),
            std::string(R"({"whole":42,"negative":-7,"fraction":0.5})"));
}

CALENDAR_TEST(json_round_trips_a_serialized_document) {
  JsonValue root = JsonValue::Object();
  root.Set("SchemaVersion", JsonValue::Number(1));
  JsonValue events = JsonValue::Array();
  JsonValue event = JsonValue::Object();
  event.Set("Id", JsonValue::String("event-001"));
  event.Set("Title", JsonValue::String("Tab \t sample"));
  events.Append(std::move(event));
  root.Set("Events", std::move(events));

  JsonValue reparsed;
  EXPECT_TRUE(JsonValue::Parse(root.ToString(), &reparsed));
  EXPECT_EQ(reparsed.ToString(), root.ToString());
  EXPECT_EQ(reparsed.Find("Events")->Elements()[0].StringOr("Title", ""),
            std::string("Tab \t sample"));
}

}  // namespace
