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

#include "base/local_zone.hh"

#include <cstdlib>
#include <ctime>

namespace calendar {
namespace base {
namespace {

// Days from 0001-01-01 to 1970-01-01, used to move between our day numbers
// and POSIX time.
constexpr std::int64_t kDaysToUnixEpoch = 719162;
constexpr std::int64_t kUnixEpochTicks = kDaysToUnixEpoch * kTicksPerDay;

std::int64_t UnixSecondsFromWallClock(const Date& date, int hour, int minute,
                                      int second) {
  return (static_cast<std::int64_t>(date.DayNumber()) - kDaysToUnixEpoch) *
             86400LL +
         hour * 3600LL + minute * 60LL + second;
}

// True when interpreting `naive_seconds` as a UTC reading and shifting it by
// `offset_seconds` lands on a real local time that reads back identically.
bool OffsetReproducesWallClock(std::int64_t naive_seconds, long offset_seconds,
                               const Date& date, int hour, int minute,
                               int second) {
  const std::time_t candidate =
      static_cast<std::time_t>(naive_seconds - offset_seconds);
  std::tm broken_down = {};
  if (localtime_r(&candidate, &broken_down) == nullptr) return false;
  return broken_down.tm_gmtoff == offset_seconds &&
         broken_down.tm_year + 1900 == date.year() &&
         broken_down.tm_mon + 1 == date.month() &&
         broken_down.tm_mday == date.day() && broken_down.tm_hour == hour &&
         broken_down.tm_min == minute && broken_down.tm_sec == second;
}

// The offsets in effect a day either side of an instant. Every real zone
// changes by at most one transition per day, so these two readings bracket
// every offset that can apply to the wall clock in between.
void CandidateOffsets(std::int64_t naive_seconds, long* first, long* second) {
  const std::time_t before =
      static_cast<std::time_t>(naive_seconds - 2 * 86400LL);
  const std::time_t after =
      static_cast<std::time_t>(naive_seconds + 2 * 86400LL);
  std::tm broken_down = {};
  *first = localtime_r(&before, &broken_down) != nullptr
               ? broken_down.tm_gmtoff
               : 0;
  *second = localtime_r(&after, &broken_down) != nullptr
                ? broken_down.tm_gmtoff
                : *first;
}

}  // namespace

WallClockResolution LocalZone::ResolveWallClock(const Date& date, int hour,
                                                int minute, int second) {
  const std::int64_t naive =
      UnixSecondsFromWallClock(date, hour, minute, second);
  long low = 0;
  long high = 0;
  CandidateOffsets(naive, &low, &high);
  if (low > high) {
    const long swapped = low;
    low = high;
    high = swapped;
  }

  WallClockResolution resolution;
  if (OffsetReproducesWallClock(naive, low, date, hour, minute, second)) {
    resolution.offset_minutes[resolution.count++] =
        static_cast<int>(low / 60);
  }
  if (high != low &&
      OffsetReproducesWallClock(naive, high, date, hour, minute, second)) {
    resolution.offset_minutes[resolution.count++] =
        static_cast<int>(high / 60);
  }
  return resolution;
}

OffsetDateTime LocalZone::AtStartOfDay(const Date& date) {
  // Walk forward a minute at a time out of a DST gap, capped at one day so a
  // pathological zone cannot spin. A zone with no valid reading in 24 hours
  // does not exist, but the cap keeps the function total.
  for (int minutes = 0; minutes < 24 * 60; ++minutes) {
    const int hour = minutes / 60;
    const int minute = minutes % 60;
    const WallClockResolution resolution =
        ResolveWallClock(date, hour, minute, 0);
    if (resolution.count == 0) continue;
    return OffsetDateTime::FromLocalParts(
        date, hour, minute, 0, 0,
        resolution.offset_minutes[resolution.count - 1]);
  }
  return OffsetDateTime::FromLocalParts(date, 0, 0, 0, 0, 0);
}

OffsetDateTime LocalZone::Now() {
  const std::time_t now = std::time(nullptr);
  std::tm broken_down = {};
  const long offset_seconds = localtime_r(&now, &broken_down) != nullptr
                                  ? broken_down.tm_gmtoff
                                  : 0;
  return OffsetDateTime::FromUtcTicks(
      kUnixEpochTicks + static_cast<std::int64_t>(now) * kTicksPerSecond,
      static_cast<int>(offset_seconds / 60));
}

Date LocalZone::Today() {
  const std::time_t now = std::time(nullptr);
  std::tm broken_down = {};
  if (localtime_r(&now, &broken_down) == nullptr) return Date(1, 1, 1);
  return Date(broken_down.tm_year + 1900, broken_down.tm_mon + 1,
              broken_down.tm_mday);
}

ScopedTimeZone::ScopedTimeZone(const std::string& zone) : had_previous_(false) {
  const char* previous = std::getenv("TZ");
  if (previous != nullptr) {
    had_previous_ = true;
    previous_ = previous;
  }
  setenv("TZ", zone.c_str(), 1);
  tzset();
}

ScopedTimeZone::~ScopedTimeZone() {
  if (had_previous_) {
    setenv("TZ", previous_.c_str(), 1);
  } else {
    unsetenv("TZ");
  }
  tzset();
}

}  // namespace base
}  // namespace calendar
