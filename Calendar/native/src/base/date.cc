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

#include <algorithm>
#include <cstdio>

namespace calendar {
namespace base {
namespace {

// Days from 0001-01-01 to 1970-01-01 in the proleptic Gregorian calendar.
constexpr int kDaysToUnixEpoch = 719162;

// Howard Hinnant's civil-from-days / days-from-civil pair, shifted so that
// day 0 is 0001-01-01 instead of 1970-01-01.
int DaysFromCivil(int year, int month, int day) {
  int shifted_year = year - (month <= 2 ? 1 : 0);
  const int era = (shifted_year >= 0 ? shifted_year : shifted_year - 399) / 400;
  const unsigned year_of_era =
      static_cast<unsigned>(shifted_year - era * 400);
  const unsigned day_of_year = static_cast<unsigned>(
      (153 * (month + (month > 2 ? -3 : 9)) + 2) / 5 + day - 1);
  const unsigned day_of_era =
      year_of_era * 365 + year_of_era / 4 - year_of_era / 100 + day_of_year;
  return era * 146097 + static_cast<int>(day_of_era) - 719468;
}

void CivilFromDays(int days, int* year, int* month, int* day) {
  days += 719468;
  const int era = (days >= 0 ? days : days - 146096) / 146097;
  const unsigned day_of_era = static_cast<unsigned>(days - era * 146097);
  const unsigned year_of_era =
      (day_of_era - day_of_era / 1460 + day_of_era / 36524 -
       day_of_era / 146096) /
      365;
  const int shifted_year = static_cast<int>(year_of_era) + era * 400;
  const unsigned day_of_year =
      day_of_era - (365 * year_of_era + year_of_era / 4 - year_of_era / 100);
  const unsigned month_prime = (5 * day_of_year + 2) / 153;
  *day = static_cast<int>(day_of_year - (153 * month_prime + 2) / 5 + 1);
  *month = static_cast<int>(month_prime + (month_prime < 10 ? 3 : -9));
  *year = shifted_year + (*month <= 2 ? 1 : 0);
}

}  // namespace

Date::Date(int year, int month, int day)
    : year_(year),
      month_(std::min(12, std::max(1, month))),
      day_(day) {
  day_ = std::min(DaysInMonth(year_, month_), std::max(1, day_));
}

bool Date::IsLeapYear(int year) {
  return (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
}

int Date::DaysInMonth(int year, int month) {
  static const int kLengths[12] = {31, 28, 31, 30, 31, 30,
                                   31, 31, 30, 31, 30, 31};
  if (month < 1 || month > 12) return 31;
  if (month == 2 && IsLeapYear(year)) return 29;
  return kLengths[month - 1];
}

int Date::DayNumber() const {
  return DaysFromCivil(year_, month_, day_) + kDaysToUnixEpoch;
}

Date Date::FromDayNumber(int day_number) {
  int year = 0;
  int month = 0;
  int day = 0;
  CivilFromDays(day_number - kDaysToUnixEpoch, &year, &month, &day);
  return Date(year, month, day);
}

int Date::DayOfWeek() const {
  // 0001-01-01 was a Monday, and System.DayOfWeek numbers Sunday as 0.
  const int weekday = (DayNumber() + 1) % 7;
  return weekday < 0 ? weekday + 7 : weekday;
}

Date Date::AddDays(int days) const {
  return FromDayNumber(DayNumber() + days);
}

Date Date::AddMonths(int months) const {
  const int total = (year_ * 12 + (month_ - 1)) + months;
  const int target_year = total >= 0 ? total / 12 : (total - 11) / 12;
  const int target_month = total - target_year * 12 + 1;
  return Date(target_year, target_month,
              std::min(day_, DaysInMonth(target_year, target_month)));
}

std::string Date::ToIsoString() const {
  char buffer[16];
  std::snprintf(buffer, sizeof(buffer), "%04d-%02d-%02d", year_, month_, day_);
  return std::string(buffer);
}

}  // namespace base
}  // namespace calendar
