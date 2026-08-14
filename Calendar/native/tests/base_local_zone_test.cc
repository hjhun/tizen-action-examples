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

#include "base/date.hh"
#include "base/offset_date_time.hh"
#include "harness.hh"

namespace {

using ::calendar::base::Date;
using ::calendar::base::LocalZone;
using ::calendar::base::OffsetDateTime;
using ::calendar::base::ScopedTimeZone;
using ::calendar::base::WallClockResolution;

CALENDAR_TEST(local_zone_resolves_an_unambiguous_wall_clock) {
  const ScopedTimeZone zone("Asia/Seoul");
  const WallClockResolution resolution =
      LocalZone::ResolveWallClock(Date(2026, 8, 14), 0, 0, 0);
  EXPECT_EQ(resolution.count, 1);
  EXPECT_EQ(resolution.offset_minutes[0], 540);
}

CALENDAR_TEST(local_zone_reports_a_spring_forward_gap_as_invalid) {
  // Chile advances the clock at midnight, so 2026-09-06 00:00 never occurs.
  const ScopedTimeZone zone("America/Santiago");
  EXPECT_EQ(LocalZone::ResolveWallClock(Date(2026, 9, 6), 0, 0, 0).count, 0);
  EXPECT_EQ(LocalZone::ResolveWallClock(Date(2026, 9, 6), 1, 0, 0).count, 1);
}

CALENDAR_TEST(local_zone_reports_both_offsets_for_an_ambiguous_wall_clock) {
  const ScopedTimeZone zone("Europe/Berlin");
  const WallClockResolution resolution =
      LocalZone::ResolveWallClock(Date(2026, 10, 25), 2, 0, 0);
  EXPECT_EQ(resolution.count, 2);
  // Ordered ascending, so the reference's Max() is the second entry.
  EXPECT_EQ(resolution.offset_minutes[0], 60);
  EXPECT_EQ(resolution.offset_minutes[1], 120);
}

CALENDAR_TEST(local_zone_start_of_day_is_plain_midnight_normally) {
  const ScopedTimeZone zone("Asia/Seoul");
  const OffsetDateTime start = LocalZone::AtStartOfDay(Date(2026, 8, 14));
  EXPECT_EQ(start.ToRoundTripString(),
            std::string("2026-08-14T00:00:00.0000000+09:00"));
}

CALENDAR_TEST(local_zone_start_of_day_skips_an_invalid_midnight) {
  // The reference walks forward a minute at a time out of the DST gap.
  const ScopedTimeZone zone("America/Santiago");
  const OffsetDateTime start = LocalZone::AtStartOfDay(Date(2026, 9, 6));
  EXPECT_EQ(start.ToRoundTripString(),
            std::string("2026-09-06T01:00:00.0000000-03:00"));
}

CALENDAR_TEST(local_zone_start_of_day_takes_the_max_ambiguous_offset) {
  // Synthetic zone whose DST ends at 01:00, making 00:00 occur twice at
  // +04:00 and then +03:00. The reference selects the maximum offset.
  const ScopedTimeZone zone("TST-3TDT,M3.5.0/1,M10.5.0/1");
  const OffsetDateTime start = LocalZone::AtStartOfDay(Date(2026, 10, 25));
  EXPECT_EQ(start.ToRoundTripString(),
            std::string("2026-10-25T00:00:00.0000000+04:00"));
}

CALENDAR_TEST(local_zone_scoped_time_zone_restores_the_previous_zone) {
  const ScopedTimeZone outer("Asia/Seoul");
  {
    const ScopedTimeZone inner("Europe/Berlin");
    EXPECT_EQ(LocalZone::ResolveWallClock(Date(2026, 8, 14), 0, 0, 0)
                  .offset_minutes[0],
              120);
  }
  EXPECT_EQ(
      LocalZone::ResolveWallClock(Date(2026, 8, 14), 0, 0, 0).offset_minutes[0],
      540);
}

}  // namespace
