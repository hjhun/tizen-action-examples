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

#include "base/date.hh"

#include "harness.hh"

namespace {

using ::calendar::base::Date;

CALENDAR_TEST(date_add_months_clamps_day) {
  EXPECT_EQ(Date(2026, 1, 31).AddMonths(1), Date(2026, 2, 28));
  EXPECT_EQ(Date(2024, 1, 31).AddMonths(1), Date(2024, 2, 29));
  EXPECT_EQ(Date(2026, 3, 31).AddMonths(-1), Date(2026, 2, 28));
  EXPECT_EQ(Date(2026, 12, 15).AddMonths(1), Date(2027, 1, 15));
}

CALENDAR_TEST(date_add_days_crosses_year_boundaries) {
  EXPECT_EQ(Date(2026, 12, 31).AddDays(1), Date(2027, 1, 1));
  EXPECT_EQ(Date(2027, 1, 1).AddDays(-1), Date(2026, 12, 31));
  EXPECT_EQ(Date(2024, 2, 28).AddDays(1), Date(2024, 2, 29));
  EXPECT_EQ(Date(2026, 2, 28).AddDays(1), Date(2026, 3, 1));
}

CALENDAR_TEST(date_day_number_round_trips) {
  const Date origin(1, 1, 1);
  EXPECT_EQ(origin.DayNumber(), 0);
  EXPECT_EQ(Date::FromDayNumber(origin.DayNumber()), origin);

  const Date sample(2026, 8, 14);
  EXPECT_EQ(Date::FromDayNumber(sample.DayNumber()), sample);
  EXPECT_EQ(sample.AddDays(0), sample);
}

CALENDAR_TEST(date_day_of_week_matches_reference_calendar) {
  // 2026-08-14 is a Friday; DayOfWeek() is 0 == Sunday like DateOnly.
  EXPECT_EQ(Date(2026, 8, 14).DayOfWeek(), 5);
  EXPECT_EQ(Date(2026, 8, 16).DayOfWeek(), 0);
  EXPECT_EQ(Date(2026, 8, 15).DayOfWeek(), 6);
}

CALENDAR_TEST(date_days_in_month_handles_leap_years) {
  EXPECT_EQ(Date::DaysInMonth(2024, 2), 29);
  EXPECT_EQ(Date::DaysInMonth(2026, 2), 28);
  EXPECT_EQ(Date::DaysInMonth(2000, 2), 29);
  EXPECT_EQ(Date::DaysInMonth(1900, 2), 28);
  EXPECT_EQ(Date::DaysInMonth(2026, 4), 30);
}

CALENDAR_TEST(date_orders_chronologically) {
  EXPECT_TRUE(Date(2026, 1, 1) < Date(2026, 1, 2));
  EXPECT_TRUE(Date(2025, 12, 31) < Date(2026, 1, 1));
  EXPECT_FALSE(Date(2026, 1, 1) < Date(2026, 1, 1));
}

}  // namespace
