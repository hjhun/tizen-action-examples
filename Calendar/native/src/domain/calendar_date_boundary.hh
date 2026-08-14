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

#ifndef CALENDAR_NATIVE_DOMAIN_CALENDAR_DATE_BOUNDARY_HH_
#define CALENDAR_NATIVE_DOMAIN_CALENDAR_DATE_BOUNDARY_HH_

#include "base/date.hh"
#include "base/local_zone.hh"
#include "base/offset_date_time.hh"

namespace calendar {
namespace domain {

// Converts a date-only UI boundary into an instant in the application's local
// time zone. Kept as a named domain concept, rather than an inline LocalZone
// call, because every half-open period in the app must agree on how a local
// day begins across DST transitions.
class CalendarDateBoundary {
 public:
  static base::OffsetDateTime AtStartOfDay(const base::Date& date) {
    return base::LocalZone::AtStartOfDay(date);
  }
};

}  // namespace domain
}  // namespace calendar

#endif  // CALENDAR_NATIVE_DOMAIN_CALENDAR_DATE_BOUNDARY_HH_
