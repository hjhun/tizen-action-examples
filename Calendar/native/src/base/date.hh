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

#ifndef CALENDAR_NATIVE_BASE_DATE_HH_
#define CALENDAR_NATIVE_BASE_DATE_HH_

#include <cstdint>
#include <ostream>
#include <string>

namespace calendar {
namespace base {

// A proleptic Gregorian calendar date with no time or zone, mirroring the
// System.DateOnly contract the C# Calendar reference relies on. Day numbers
// count days since 0001-01-01, which is day 0, so they can be compared and
// subtracted directly.
class Date {
 public:
  Date() : year_(1), month_(1), day_(1) {}

  // Clamps an out-of-range day to the length of the requested month so that
  // callers such as AddMonths() never observe an invalid date.
  Date(int year, int month, int day);

  static Date FromDayNumber(int day_number);

  static int DaysInMonth(int year, int month);

  static bool IsLeapYear(int year);

  int year() const { return year_; }
  int month() const { return month_; }
  int day() const { return day_; }

  int DayNumber() const;

  // 0 is Sunday, matching System.DayOfWeek.
  int DayOfWeek() const;

  Date AddDays(int days) const;

  // Clamps the day of month, matching DateOnly.AddMonths().
  Date AddMonths(int months) const;

  // "yyyy-MM-dd".
  std::string ToIsoString() const;

  friend bool operator==(const Date& left, const Date& right) {
    return left.year_ == right.year_ && left.month_ == right.month_ &&
           left.day_ == right.day_;
  }

  friend bool operator!=(const Date& left, const Date& right) {
    return !(left == right);
  }

  friend bool operator<(const Date& left, const Date& right) {
    return left.DayNumber() < right.DayNumber();
  }

  friend bool operator<=(const Date& left, const Date& right) {
    return !(right < left);
  }

  friend bool operator>(const Date& left, const Date& right) {
    return right < left;
  }

  friend bool operator>=(const Date& left, const Date& right) {
    return !(left < right);
  }

  friend std::ostream& operator<<(std::ostream& stream, const Date& date) {
    return stream << date.ToIsoString();
  }

 private:
  int year_;
  int month_;
  int day_;
};

}  // namespace base
}  // namespace calendar

#endif  // CALENDAR_NATIVE_BASE_DATE_HH_
