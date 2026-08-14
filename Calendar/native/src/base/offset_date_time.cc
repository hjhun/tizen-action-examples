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

#include "base/offset_date_time.hh"

#include <cstdio>
#include <cstdlib>

namespace calendar {
namespace base {
namespace {

bool IsDigit(char character) { return character >= '0' && character <= '9'; }

// Reads exactly `digits` decimal characters starting at `position`, advancing
// it on success.
bool ReadFixedDigits(const std::string& text, std::size_t* position,
                     int digits, int* value) {
  if (*position + static_cast<std::size_t>(digits) > text.size()) return false;
  int accumulated = 0;
  for (int index = 0; index < digits; ++index) {
    const char character = text[*position + static_cast<std::size_t>(index)];
    if (!IsDigit(character)) return false;
    accumulated = accumulated * 10 + (character - '0');
  }
  *position += static_cast<std::size_t>(digits);
  *value = accumulated;
  return true;
}

bool ReadLiteral(const std::string& text, std::size_t* position, char expected) {
  if (*position >= text.size() || text[*position] != expected) return false;
  ++*position;
  return true;
}

// Parses ".fffffff" with one to seven digits into 100 ns ticks.
bool ReadOptionalFraction(const std::string& text, std::size_t* position,
                          std::int64_t* sub_second_ticks) {
  *sub_second_ticks = 0;
  if (*position >= text.size() || text[*position] != '.') return true;
  ++*position;

  int digits = 0;
  std::int64_t scaled = 0;
  while (*position < text.size() && IsDigit(text[*position])) {
    if (digits < 7) {
      scaled = scaled * 10 + (text[*position] - '0');
      ++digits;
    }
    ++*position;
  }
  if (digits == 0) return false;
  for (int index = digits; index < 7; ++index) scaled *= 10;
  *sub_second_ticks = scaled;
  return true;
}

// Parses "Z", "+hh:mm" or "-hh:mm". Returns false when nothing is present so
// the caller can decide whether an offset was mandatory.
bool ReadOffset(const std::string& text, std::size_t* position,
                int* offset_minutes) {
  if (*position >= text.size()) return false;
  const char sign = text[*position];
  if (sign == 'Z' || sign == 'z') {
    ++*position;
    *offset_minutes = 0;
    return true;
  }
  if (sign != '+' && sign != '-') return false;
  ++*position;

  int hours = 0;
  int minutes = 0;
  if (!ReadFixedDigits(text, position, 2, &hours)) return false;
  if (!ReadLiteral(text, position, ':')) return false;
  if (!ReadFixedDigits(text, position, 2, &minutes)) return false;
  if (hours > 14 || minutes > 59) return false;

  const int magnitude = hours * 60 + minutes;
  *offset_minutes = sign == '-' ? -magnitude : magnitude;
  return true;
}

bool ParseInternal(const std::string& text, bool require_offset,
                   OffsetDateTime* parsed) {
  if (text.empty() || text.size() > 64) return false;

  std::size_t position = 0;
  int year = 0;
  int month = 0;
  int day = 0;
  int hour = 0;
  int minute = 0;
  int second = 0;
  std::int64_t sub_second_ticks = 0;

  if (!ReadFixedDigits(text, &position, 4, &year)) return false;
  if (!ReadLiteral(text, &position, '-')) return false;
  if (!ReadFixedDigits(text, &position, 2, &month)) return false;
  if (!ReadLiteral(text, &position, '-')) return false;
  if (!ReadFixedDigits(text, &position, 2, &day)) return false;
  if (!ReadLiteral(text, &position, 'T')) return false;
  if (!ReadFixedDigits(text, &position, 2, &hour)) return false;
  if (!ReadLiteral(text, &position, ':')) return false;
  if (!ReadFixedDigits(text, &position, 2, &minute)) return false;
  if (!ReadLiteral(text, &position, ':')) return false;
  if (!ReadFixedDigits(text, &position, 2, &second)) return false;
  if (!ReadOptionalFraction(text, &position, &sub_second_ticks)) return false;

  int offset_minutes = 0;
  const bool has_offset = ReadOffset(text, &position, &offset_minutes);
  if (require_offset && !has_offset) return false;
  if (position != text.size()) return false;

  if (year < 1 || year > 9999) return false;
  if (month < 1 || month > 12) return false;
  if (day < 1 || day > Date::DaysInMonth(year, month)) return false;
  if (hour > 23 || minute > 59 || second > 59) return false;

  *parsed = OffsetDateTime::FromLocalParts(Date(year, month, day), hour,
                                           minute, second, sub_second_ticks,
                                           offset_minutes);
  return true;
}

}  // namespace

OffsetDateTime OffsetDateTime::FromUtcTicks(std::int64_t ticks_utc,
                                            int offset_minutes) {
  return OffsetDateTime(ticks_utc, offset_minutes);
}

OffsetDateTime OffsetDateTime::FromLocalParts(const Date& date, int hour,
                                              int minute, int second,
                                              std::int64_t sub_second_ticks,
                                              int offset_minutes) {
  const std::int64_t local_ticks =
      static_cast<std::int64_t>(date.DayNumber()) * kTicksPerDay +
      static_cast<std::int64_t>(hour) * kTicksPerHour +
      static_cast<std::int64_t>(minute) * kTicksPerMinute +
      static_cast<std::int64_t>(second) * kTicksPerSecond + sub_second_ticks;
  return OffsetDateTime(
      local_ticks -
          static_cast<std::int64_t>(offset_minutes) * kTicksPerMinute,
      offset_minutes);
}

bool OffsetDateTime::TryParseStrict(const std::string& text,
                                    OffsetDateTime* parsed) {
  return ParseInternal(text, /*require_offset=*/true, parsed);
}

bool OffsetDateTime::TryParseFlexible(const std::string& text,
                                      OffsetDateTime* parsed) {
  return ParseInternal(text, /*require_offset=*/false, parsed);
}

Date OffsetDateTime::LocalDate() const {
  return Date::FromDayNumber(static_cast<int>(LocalTicks() / kTicksPerDay));
}

std::int64_t OffsetDateTime::LocalTimeOfDayTicks() const {
  return LocalTicks() % kTicksPerDay;
}

int OffsetDateTime::LocalHour() const {
  return static_cast<int>(LocalTimeOfDayTicks() / kTicksPerHour);
}

int OffsetDateTime::LocalMinute() const {
  return static_cast<int>((LocalTimeOfDayTicks() / kTicksPerMinute) % 60);
}

int OffsetDateTime::LocalSecond() const {
  return static_cast<int>((LocalTimeOfDayTicks() / kTicksPerSecond) % 60);
}

OffsetDateTime OffsetDateTime::AddTicks(std::int64_t ticks) const {
  return OffsetDateTime(ticks_utc_ + ticks, offset_minutes_);
}

OffsetDateTime OffsetDateTime::AddMinutes(std::int64_t minutes) const {
  return AddTicks(minutes * kTicksPerMinute);
}

OffsetDateTime OffsetDateTime::AddHours(std::int64_t hours) const {
  return AddTicks(hours * kTicksPerHour);
}

std::string OffsetDateTime::ToRoundTripString() const {
  const Date date = LocalDate();
  const std::int64_t time_of_day = LocalTimeOfDayTicks();
  const int offset_magnitude =
      offset_minutes_ < 0 ? -offset_minutes_ : offset_minutes_;

  char buffer[80];
  std::snprintf(
      buffer, sizeof(buffer), "%04d-%02d-%02dT%02d:%02d:%02d.%07lld%c%02d:%02d",
      date.year(), date.month(), date.day(),
      static_cast<int>(time_of_day / kTicksPerHour),
      static_cast<int>((time_of_day / kTicksPerMinute) % 60),
      static_cast<int>((time_of_day / kTicksPerSecond) % 60),
      static_cast<long long>(time_of_day % kTicksPerSecond),
      offset_minutes_ < 0 ? '-' : '+', offset_magnitude / 60,
      offset_magnitude % 60);
  return std::string(buffer);
}

}  // namespace base
}  // namespace calendar
