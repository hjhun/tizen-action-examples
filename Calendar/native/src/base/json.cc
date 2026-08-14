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

#include <cmath>
#include <cstdio>
#include <cstdlib>

namespace calendar {
namespace base {
namespace {

bool IsDigit(char character) { return character >= '0' && character <= '9'; }

bool IsWhitespace(char character) {
  return character == ' ' || character == '\t' || character == '\n' ||
         character == '\r';
}

void AppendUtf8(unsigned int code_point, std::string* out) {
  if (code_point < 0x80) {
    out->push_back(static_cast<char>(code_point));
  } else if (code_point < 0x800) {
    out->push_back(static_cast<char>(0xC0 | (code_point >> 6)));
    out->push_back(static_cast<char>(0x80 | (code_point & 0x3F)));
  } else if (code_point < 0x10000) {
    out->push_back(static_cast<char>(0xE0 | (code_point >> 12)));
    out->push_back(static_cast<char>(0x80 | ((code_point >> 6) & 0x3F)));
    out->push_back(static_cast<char>(0x80 | (code_point & 0x3F)));
  } else {
    out->push_back(static_cast<char>(0xF0 | (code_point >> 18)));
    out->push_back(static_cast<char>(0x80 | ((code_point >> 12) & 0x3F)));
    out->push_back(static_cast<char>(0x80 | ((code_point >> 6) & 0x3F)));
    out->push_back(static_cast<char>(0x80 | (code_point & 0x3F)));
  }
}

// Recursive-descent parser over a bounded buffer. Every entry point returns
// false rather than throwing, so callers can treat malformed agent input as
// ordinary data.
class Parser {
 public:
  explicit Parser(const std::string& text) : text_(text) {}

  bool ParseDocument(JsonValue* parsed) {
    SkipWhitespace();
    if (!ParseValue(0, parsed)) return false;
    SkipWhitespace();
    return position_ == text_.size();
  }

 private:
  bool AtEnd() const { return position_ >= text_.size(); }
  char Peek() const { return text_[position_]; }

  void SkipWhitespace() {
    while (!AtEnd() && IsWhitespace(Peek())) ++position_;
  }

  bool Consume(char expected) {
    if (AtEnd() || Peek() != expected) return false;
    ++position_;
    return true;
  }

  bool ConsumeLiteral(const char* literal) {
    const std::size_t length = std::char_traits<char>::length(literal);
    if (text_.compare(position_, length, literal) != 0) return false;
    position_ += length;
    return true;
  }

  bool ParseValue(int depth, JsonValue* parsed) {
    if (depth >= kJsonMaxDepth || AtEnd()) return false;
    switch (Peek()) {
      case '{':
        return ParseObject(depth, parsed);
      case '[':
        return ParseArray(depth, parsed);
      case '"': {
        std::string value;
        if (!ParseString(&value)) return false;
        *parsed = JsonValue::String(std::move(value));
        return true;
      }
      case 't':
        if (!ConsumeLiteral("true")) return false;
        *parsed = JsonValue::Bool(true);
        return true;
      case 'f':
        if (!ConsumeLiteral("false")) return false;
        *parsed = JsonValue::Bool(false);
        return true;
      case 'n':
        if (!ConsumeLiteral("null")) return false;
        *parsed = JsonValue::Null();
        return true;
      default:
        return ParseNumber(parsed);
    }
  }

  bool ParseObject(int depth, JsonValue* parsed) {
    if (!Consume('{')) return false;
    JsonValue object = JsonValue::Object();
    SkipWhitespace();
    if (Consume('}')) {
      *parsed = std::move(object);
      return true;
    }

    while (true) {
      SkipWhitespace();
      std::string name;
      if (!ParseString(&name)) return false;
      SkipWhitespace();
      if (!Consume(':')) return false;
      SkipWhitespace();
      JsonValue member;
      if (!ParseValue(depth + 1, &member)) return false;
      object.Set(name, std::move(member));
      SkipWhitespace();
      if (Consume(',')) continue;
      if (!Consume('}')) return false;
      *parsed = std::move(object);
      return true;
    }
  }

  bool ParseArray(int depth, JsonValue* parsed) {
    if (!Consume('[')) return false;
    JsonValue array = JsonValue::Array();
    SkipWhitespace();
    if (Consume(']')) {
      *parsed = std::move(array);
      return true;
    }

    while (true) {
      SkipWhitespace();
      JsonValue element;
      if (!ParseValue(depth + 1, &element)) return false;
      array.Append(std::move(element));
      SkipWhitespace();
      if (Consume(',')) continue;
      if (!Consume(']')) return false;
      *parsed = std::move(array);
      return true;
    }
  }

  bool ParseHex4(unsigned int* value) {
    if (position_ + 4 > text_.size()) return false;
    unsigned int accumulated = 0;
    for (int index = 0; index < 4; ++index) {
      const char character = text_[position_ + static_cast<std::size_t>(index)];
      accumulated <<= 4;
      if (IsDigit(character)) {
        accumulated |= static_cast<unsigned int>(character - '0');
      } else if (character >= 'a' && character <= 'f') {
        accumulated |= static_cast<unsigned int>(character - 'a' + 10);
      } else if (character >= 'A' && character <= 'F') {
        accumulated |= static_cast<unsigned int>(character - 'A' + 10);
      } else {
        return false;
      }
    }
    position_ += 4;
    *value = accumulated;
    return true;
  }

  bool ParseString(std::string* value) {
    if (!Consume('"')) return false;
    value->clear();
    while (true) {
      if (AtEnd()) return false;
      const char character = text_[position_++];
      if (character == '"') return true;
      if (character != '\\') {
        value->push_back(character);
        continue;
      }
      if (AtEnd()) return false;
      const char escape = text_[position_++];
      switch (escape) {
        case '"': value->push_back('"'); break;
        case '\\': value->push_back('\\'); break;
        case '/': value->push_back('/'); break;
        case 'b': value->push_back('\b'); break;
        case 'f': value->push_back('\f'); break;
        case 'n': value->push_back('\n'); break;
        case 'r': value->push_back('\r'); break;
        case 't': value->push_back('\t'); break;
        case 'u': {
          unsigned int code_point = 0;
          if (!ParseHex4(&code_point)) return false;
          // Combine a surrogate pair when the low half follows.
          if (code_point >= 0xD800 && code_point <= 0xDBFF &&
              position_ + 1 < text_.size() && text_[position_] == '\\' &&
              text_[position_ + 1] == 'u') {
            const std::size_t saved = position_;
            position_ += 2;
            unsigned int low = 0;
            if (ParseHex4(&low) && low >= 0xDC00 && low <= 0xDFFF) {
              code_point = 0x10000 + ((code_point - 0xD800) << 10) +
                           (low - 0xDC00);
            } else {
              position_ = saved;
            }
          }
          AppendUtf8(code_point, value);
          break;
        }
        default:
          return false;
      }
    }
  }

  bool ParseNumber(JsonValue* parsed) {
    const std::size_t start = position_;
    if (!AtEnd() && Peek() == '-') ++position_;
    if (AtEnd() || !IsDigit(Peek())) return false;
    while (!AtEnd() && IsDigit(Peek())) ++position_;
    if (!AtEnd() && Peek() == '.') {
      ++position_;
      if (AtEnd() || !IsDigit(Peek())) return false;
      while (!AtEnd() && IsDigit(Peek())) ++position_;
    }
    if (!AtEnd() && (Peek() == 'e' || Peek() == 'E')) {
      ++position_;
      if (!AtEnd() && (Peek() == '+' || Peek() == '-')) ++position_;
      if (AtEnd() || !IsDigit(Peek())) return false;
      while (!AtEnd() && IsDigit(Peek())) ++position_;
    }

    *parsed = JsonValue::Number(
        std::strtod(text_.substr(start, position_ - start).c_str(), nullptr));
    return true;
  }

  const std::string& text_;
  std::size_t position_ = 0;
};

}  // namespace

JsonValue JsonValue::Bool(bool value) {
  JsonValue result;
  result.kind_ = Kind::kBool;
  result.bool_ = value;
  return result;
}

JsonValue JsonValue::Number(double value) {
  JsonValue result;
  result.kind_ = Kind::kNumber;
  result.number_ = value;
  return result;
}

JsonValue JsonValue::String(std::string value) {
  JsonValue result;
  result.kind_ = Kind::kString;
  result.string_ = std::move(value);
  return result;
}

JsonValue JsonValue::Array() {
  JsonValue result;
  result.kind_ = Kind::kArray;
  return result;
}

JsonValue JsonValue::Object() {
  JsonValue result;
  result.kind_ = Kind::kObject;
  return result;
}

bool JsonValue::Parse(const std::string& text, JsonValue* parsed) {
  if (text.empty() || text.size() > kJsonMaxBytes) return false;
  Parser parser(text);
  JsonValue candidate;
  if (!parser.ParseDocument(&candidate)) return false;
  *parsed = std::move(candidate);
  return true;
}

const JsonValue* JsonValue::Find(const std::string& name) const {
  if (kind_ != Kind::kObject) return nullptr;
  for (const auto& member : members_) {
    if (member.first == name) return &member.second;
  }
  return nullptr;
}

std::string JsonValue::StringOr(const std::string& name,
                                const std::string& fallback) const {
  const JsonValue* member = Find(name);
  return member != nullptr && member->IsString() ? member->string_ : fallback;
}

void JsonValue::Append(JsonValue element) {
  if (kind_ != Kind::kArray) return;
  elements_.push_back(std::move(element));
}

void JsonValue::Set(const std::string& name, JsonValue value) {
  if (kind_ != Kind::kObject) return;
  for (auto& member : members_) {
    if (member.first == name) {
      member.second = std::move(value);
      return;
    }
  }
  members_.emplace_back(name, std::move(value));
}

std::string JsonQuote(const std::string& value) {
  std::string out;
  out.reserve(value.size() + 2);
  out.push_back('"');
  for (const char character : value) {
    switch (character) {
      case '"': out += "\\\""; break;
      case '\\': out += "\\\\"; break;
      case '\b': out += "\\b"; break;
      case '\f': out += "\\f"; break;
      case '\n': out += "\\n"; break;
      case '\r': out += "\\r"; break;
      case '\t': out += "\\t"; break;
      default:
        if (static_cast<unsigned char>(character) < 0x20) {
          char escape[8];
          std::snprintf(escape, sizeof(escape), "\\u%04x",
                        static_cast<unsigned char>(character));
          out += escape;
        } else {
          out.push_back(character);
        }
    }
  }
  out.push_back('"');
  return out;
}

void JsonValue::Write(std::string* out) const {
  switch (kind_) {
    case Kind::kNull:
      *out += "null";
      return;
    case Kind::kBool:
      *out += bool_ ? "true" : "false";
      return;
    case Kind::kNumber: {
      char buffer[40];
      // Integral values are written without a fractional part so documents
      // compare byte-for-byte with the reference serializer.
      if (std::isfinite(number_) && number_ == std::floor(number_) &&
          std::fabs(number_) < 1e15) {
        std::snprintf(buffer, sizeof(buffer), "%lld",
                      static_cast<long long>(number_));
      } else if (!std::isfinite(number_)) {
        *out += "null";
        return;
      } else {
        std::snprintf(buffer, sizeof(buffer), "%.17g", number_);
      }
      *out += buffer;
      return;
    }
    case Kind::kString:
      *out += JsonQuote(string_);
      return;
    case Kind::kArray: {
      out->push_back('[');
      for (std::size_t index = 0; index < elements_.size(); ++index) {
        if (index != 0) out->push_back(',');
        elements_[index].Write(out);
      }
      out->push_back(']');
      return;
    }
    case Kind::kObject: {
      out->push_back('{');
      for (std::size_t index = 0; index < members_.size(); ++index) {
        if (index != 0) out->push_back(',');
        *out += JsonQuote(members_[index].first);
        out->push_back(':');
        members_[index].second.Write(out);
      }
      out->push_back('}');
      return;
    }
  }
}

std::string JsonValue::ToString() const {
  std::string out;
  Write(&out);
  return out;
}

}  // namespace base
}  // namespace calendar
