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

#ifndef CALENDAR_NATIVE_BASE_LOCAL_ZONE_HH_
#define CALENDAR_NATIVE_BASE_LOCAL_ZONE_HH_

#include <string>

#include "base/date.hh"
#include "base/offset_date_time.hh"

namespace calendar {
namespace base {

// How many UTC offsets reproduce a requested local wall-clock reading.
// count == 0 is a spring-forward gap, count == 2 is a fall-back repeat.
// Offsets are ordered ascending, so offset_minutes[count - 1] is the maximum
// the reference's GetAmbiguousTimeOffsets(...).Max() would return.
struct WallClockResolution {
  int count = 0;
  int offset_minutes[2] = {0, 0};
};

// Wall-clock conversion for the process time zone, built on the C library so
// it needs neither std::chrono::tzdb (C++20) nor a Tizen dependency.
class LocalZone {
 public:
  static WallClockResolution ResolveWallClock(const Date& date, int hour,
                                              int minute, int second);

  // The first instant of a local calendar date. A date whose midnight falls
  // in a DST gap begins at the first wall-clock minute that exists; an
  // ambiguous midnight uses the larger UTC offset. This mirrors
  // CalendarDateBoundary.AtStartOfDay in the C# reference.
  static OffsetDateTime AtStartOfDay(const Date& date);

  // The current instant, carrying the local UTC offset.
  static OffsetDateTime Now();

  // Today's local calendar date.
  static Date Today();
};

// Pins the process time zone for the lifetime of the object and restores the
// previous TZ afterwards. Test-only in practice, but kept beside LocalZone
// because it is the seam that makes LocalZone deterministic.
class ScopedTimeZone {
 public:
  explicit ScopedTimeZone(const std::string& zone);
  ~ScopedTimeZone();

  ScopedTimeZone(const ScopedTimeZone&) = delete;
  ScopedTimeZone& operator=(const ScopedTimeZone&) = delete;

 private:
  bool had_previous_;
  std::string previous_;
};

}  // namespace base
}  // namespace calendar

#endif  // CALENDAR_NATIVE_BASE_LOCAL_ZONE_HH_
