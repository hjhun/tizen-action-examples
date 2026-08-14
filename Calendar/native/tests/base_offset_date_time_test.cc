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

#include "base/date.hh"
#include "harness.hh"

namespace {

using ::calendar::base::Date;
using ::calendar::base::OffsetDateTime;

OffsetDateTime Local(int year, int month, int day, int hour, int minute,
                     int offset_minutes) {
  return OffsetDateTime::FromLocalParts(Date(year, month, day), hour, minute,
                                        0, 0, offset_minutes);
}

CALENDAR_TEST(offset_date_time_round_trip_format) {
  // Matches the .NET "O" round-trip format the reference writes on the wire.
  EXPECT_EQ(Local(2026, 8, 14, 9, 0, 540).ToRoundTripString(),
            std::string("2026-08-14T09:00:00.0000000+09:00"));
  EXPECT_EQ(Local(2026, 1, 2, 3, 4, 0).ToRoundTripString(),
            std::string("2026-01-02T03:04:00.0000000+00:00"));
  EXPECT_EQ(Local(2026, 12, 31, 23, 59, -330).ToRoundTripString(),
            std::string("2026-12-31T23:59:00.0000000-05:30"));
}

CALENDAR_TEST(offset_date_time_parses_strict_iso_with_offset) {
  OffsetDateTime parsed;
  EXPECT_TRUE(
      OffsetDateTime::TryParseStrict("2026-08-14T09:00:00+09:00", &parsed));
  EXPECT_EQ(parsed.ToRoundTripString(),
            std::string("2026-08-14T09:00:00.0000000+09:00"));

  EXPECT_TRUE(OffsetDateTime::TryParseStrict(
      "2026-08-14T09:00:00.1234567+09:00", &parsed));
  EXPECT_EQ(parsed.ToRoundTripString(),
            std::string("2026-08-14T09:00:00.1234567+09:00"));

  EXPECT_TRUE(
      OffsetDateTime::TryParseStrict("2026-08-14T00:00:00Z", &parsed));
  EXPECT_EQ(parsed.ToRoundTripString(),
            std::string("2026-08-14T00:00:00.0000000+00:00"));
}

CALENDAR_TEST(offset_date_time_strict_parse_requires_explicit_offset) {
  OffsetDateTime parsed;
  EXPECT_FALSE(OffsetDateTime::TryParseStrict("2026-08-14T09:00:00", &parsed));
  EXPECT_FALSE(OffsetDateTime::TryParseStrict("2026-08-14", &parsed));
  EXPECT_FALSE(OffsetDateTime::TryParseStrict("not a timestamp", &parsed));
  EXPECT_FALSE(OffsetDateTime::TryParseStrict("", &parsed));
  EXPECT_FALSE(
      OffsetDateTime::TryParseStrict("2026-13-01T00:00:00+00:00", &parsed));
  EXPECT_FALSE(
      OffsetDateTime::TryParseStrict("2026-08-14T24:00:00+00:00", &parsed));
}

CALENDAR_TEST(offset_date_time_compares_on_the_same_instant) {
  // 09:00+09:00 and 03:00+03:00 are the same instant.
  const OffsetDateTime seoul = Local(2026, 8, 14, 9, 0, 540);
  const OffsetDateTime moscow = Local(2026, 8, 14, 3, 0, 180);
  EXPECT_TRUE(seoul == moscow);
  EXPECT_FALSE(seoul < moscow);
  EXPECT_FALSE(moscow < seoul);
  EXPECT_TRUE(seoul < seoul.AddMinutes(1));
}

CALENDAR_TEST(offset_date_time_exposes_local_wall_clock_parts) {
  const OffsetDateTime moment = Local(2026, 8, 14, 9, 30, 540);
  EXPECT_EQ(moment.LocalDate(), Date(2026, 8, 14));
  EXPECT_EQ(moment.LocalHour(), 9);
  EXPECT_EQ(moment.LocalMinute(), 30);
  EXPECT_EQ(moment.OffsetMinutes(), 540);
  EXPECT_EQ(moment.LocalTimeOfDayTicks(), 9LL * 36000000000LL + 30LL *
                                              600000000LL);
}

CALENDAR_TEST(offset_date_time_arithmetic_preserves_offset) {
  const OffsetDateTime start = Local(2026, 8, 14, 9, 0, 540);
  const OffsetDateTime shifted = start.AddMinutes(-1440);
  EXPECT_EQ(shifted.LocalDate(), Date(2026, 8, 13));
  EXPECT_EQ(shifted.LocalHour(), 9);
  EXPECT_EQ(shifted.OffsetMinutes(), 540);
  EXPECT_EQ(start.TicksUtc() - shifted.TicksUtc(), 864000000000LL);
}

CALENDAR_TEST(offset_date_time_flexible_parse_accepts_generated_json_shapes) {
  OffsetDateTime parsed;
  EXPECT_TRUE(OffsetDateTime::TryParseFlexible(
      "2026-08-14T09:00:00.0000000+09:00", &parsed));
  EXPECT_EQ(parsed.OffsetMinutes(), 540);

  EXPECT_TRUE(
      OffsetDateTime::TryParseFlexible("2026-08-14T09:00:00.5Z", &parsed));
  EXPECT_EQ(parsed.OffsetMinutes(), 0);
  EXPECT_EQ(parsed.ToRoundTripString(),
            std::string("2026-08-14T09:00:00.5000000+00:00"));

  EXPECT_FALSE(OffsetDateTime::TryParseFlexible("garbage", &parsed));
}

}  // namespace
