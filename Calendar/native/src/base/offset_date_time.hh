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

#ifndef CALENDAR_NATIVE_BASE_OFFSET_DATE_TIME_HH_
#define CALENDAR_NATIVE_BASE_OFFSET_DATE_TIME_HH_

#include <cstdint>
#include <ostream>
#include <string>

#include "base/date.hh"

namespace calendar {
namespace base {

// Ticks are 100 ns, matching System.DateTimeOffset, so the round-trip "O"
// text this type produces is byte-identical to what the C# Calendar reference
// writes on the Action wire and into persistence.
constexpr std::int64_t kTicksPerSecond = 10000000LL;
constexpr std::int64_t kTicksPerMinute = 60LL * kTicksPerSecond;
constexpr std::int64_t kTicksPerHour = 60LL * kTicksPerMinute;
constexpr std::int64_t kTicksPerDay = 24LL * kTicksPerHour;

// An instant plus the UTC offset it was observed at. Two values compare equal
// when they name the same instant, even with different offsets, exactly like
// DateTimeOffset.
class OffsetDateTime {
 public:
  OffsetDateTime() : ticks_utc_(0), offset_minutes_(0) {}

  static OffsetDateTime FromUtcTicks(std::int64_t ticks_utc,
                                     int offset_minutes);

  static OffsetDateTime FromLocalParts(const Date& date, int hour, int minute,
                                       int second, std::int64_t sub_second_ticks,
                                       int offset_minutes);

  // Accepts only the two formats the reference's search adapter accepts:
  // "yyyy-MM-ddTHH:mm:sszzz" and "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz", with a
  // trailing "Z" treated as "+00:00". An explicit offset is required.
  static bool TryParseStrict(const std::string& text, OffsetDateTime* parsed);

  // Accepts the strict forms plus an omitted offset, which is read as UTC.
  // Used where the reference calls DateTimeOffset.TryParse on Entity input.
  static bool TryParseFlexible(const std::string& text,
                               OffsetDateTime* parsed);

  std::int64_t TicksUtc() const { return ticks_utc_; }
  int OffsetMinutes() const { return offset_minutes_; }

  std::int64_t LocalTicks() const {
    return ticks_utc_ + static_cast<std::int64_t>(offset_minutes_) *
                            kTicksPerMinute;
  }

  Date LocalDate() const;
  int LocalHour() const;
  int LocalMinute() const;
  int LocalSecond() const;

  // Ticks since local midnight; the analogue of DateTimeOffset.TimeOfDay.
  std::int64_t LocalTimeOfDayTicks() const;

  OffsetDateTime AddTicks(std::int64_t ticks) const;
  OffsetDateTime AddMinutes(std::int64_t minutes) const;
  OffsetDateTime AddHours(std::int64_t hours) const;

  // .NET "O": yyyy-MM-ddTHH:mm:ss.fffffff±hh:mm.
  std::string ToRoundTripString() const;

  friend bool operator==(const OffsetDateTime& left,
                         const OffsetDateTime& right) {
    return left.ticks_utc_ == right.ticks_utc_;
  }

  friend bool operator!=(const OffsetDateTime& left,
                         const OffsetDateTime& right) {
    return !(left == right);
  }

  friend bool operator<(const OffsetDateTime& left,
                        const OffsetDateTime& right) {
    return left.ticks_utc_ < right.ticks_utc_;
  }

  friend bool operator<=(const OffsetDateTime& left,
                         const OffsetDateTime& right) {
    return !(right < left);
  }

  friend bool operator>(const OffsetDateTime& left,
                        const OffsetDateTime& right) {
    return right < left;
  }

  friend bool operator>=(const OffsetDateTime& left,
                         const OffsetDateTime& right) {
    return !(left < right);
  }

  friend std::ostream& operator<<(std::ostream& stream,
                                  const OffsetDateTime& moment) {
    return stream << moment.ToRoundTripString();
  }

 private:
  OffsetDateTime(std::int64_t ticks_utc, int offset_minutes)
      : ticks_utc_(ticks_utc), offset_minutes_(offset_minutes) {}

  std::int64_t ticks_utc_;
  int offset_minutes_;
};

}  // namespace base
}  // namespace calendar

#endif  // CALENDAR_NATIVE_BASE_OFFSET_DATE_TIME_HH_
