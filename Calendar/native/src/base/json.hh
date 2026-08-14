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

#ifndef CALENDAR_NATIVE_BASE_JSON_HH_
#define CALENDAR_NATIVE_BASE_JSON_HH_

#include <cstddef>
#include <string>
#include <utility>
#include <vector>

namespace calendar {
namespace base {

// Externally supplied JSON, such as the Entity payload carried on a
// ViewAnnotation, is bounded so a hostile document cannot exhaust the stack
// or memory.
constexpr int kJsonMaxDepth = 64;
constexpr std::size_t kJsonMaxBytes = 4u * 1024u * 1024u;

// A minimal RFC 8259 value. Object members keep insertion order because the
// persistence document and the A2UI payloads are compared as text.
class JsonValue {
 public:
  enum class Kind { kNull, kBool, kNumber, kString, kArray, kObject };

  JsonValue() : kind_(Kind::kNull), bool_(false), number_(0.0) {}

  static JsonValue Null() { return JsonValue(); }
  static JsonValue Bool(bool value);
  static JsonValue Number(double value);
  static JsonValue String(std::string value);
  static JsonValue Array();
  static JsonValue Object();

  // Returns false and leaves *parsed untouched on any malformed input,
  // trailing content, oversized document, or excessive nesting.
  static bool Parse(const std::string& text, JsonValue* parsed);

  Kind kind() const { return kind_; }
  bool IsNull() const { return kind_ == Kind::kNull; }
  bool IsBool() const { return kind_ == Kind::kBool; }
  bool IsNumber() const { return kind_ == Kind::kNumber; }
  bool IsString() const { return kind_ == Kind::kString; }
  bool IsArray() const { return kind_ == Kind::kArray; }
  bool IsObject() const { return kind_ == Kind::kObject; }

  bool AsBool() const { return kind_ == Kind::kBool && bool_; }
  double AsDouble() const { return kind_ == Kind::kNumber ? number_ : 0.0; }
  const std::string& AsString() const { return string_; }

  const std::vector<JsonValue>& Elements() const { return elements_; }

  const std::vector<std::pair<std::string, JsonValue>>& Members() const {
    return members_;
  }

  // Object member lookup; nullptr when absent or when this is not an object.
  const JsonValue* Find(const std::string& name) const;

  // Convenience for the common "string member or default" read.
  std::string StringOr(const std::string& name,
                       const std::string& fallback) const;

  // Appends to an array, replaces-or-appends on an object. Both are no-ops on
  // a value of the wrong kind, which keeps builder code branch-free.
  void Append(JsonValue element);
  void Set(const std::string& name, JsonValue value);

  // Compact serialization, no insignificant whitespace.
  std::string ToString() const;

 private:
  void Write(std::string* out) const;

  Kind kind_;
  bool bool_;
  double number_;
  std::string string_;
  std::vector<JsonValue> elements_;
  std::vector<std::pair<std::string, JsonValue>> members_;
};

// Escapes a string into JSON text, including the surrounding quotes. Exposed
// because a few callers build fragments without a full JsonValue tree.
std::string JsonQuote(const std::string& value);

}  // namespace base
}  // namespace calendar

#endif  // CALENDAR_NATIVE_BASE_JSON_HH_
