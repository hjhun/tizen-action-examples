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

#ifndef CALENDAR_NATIVE_DOMAIN_CALENDAR_SEARCH_QUERY_ADAPTER_HH_
#define CALENDAR_NATIVE_DOMAIN_CALENDAR_SEARCH_QUERY_ADAPTER_HH_

#include <string>

#include "domain/calendar_search_criteria.hh"

namespace calendar {
namespace domain {

// The default result count when a caller does not request one.
constexpr int kDefaultSearchLimit = 20;

// Translates the wire shape of Tv_Tizen.Action.Calendar_SearchInPeriod into a
// validated CalendarSearchCriteria. Kept in the domain, rather than in the
// generated provider adapter, so the compatibility rules are host-testable.
class CalendarSearchQueryAdapter {
 public:
  // Empty or whitespace timestamps mean "no bound". A non-empty timestamp
  // must be strict ISO 8601 with an explicit UTC offset.
  //
  // When no field selector is set the query is treated as omitting the
  // selection entirely and all three text fields are searched, preserving
  // compatibility with clients built before the selectors existed.
  static bool TryCreate(const std::string& keyword,
                        const std::string& start_date,
                        const std::string& end_date, int requested_limit,
                        bool search_title, bool search_location,
                        bool search_note, CalendarSearchCriteria* criteria,
                        std::string* error);
};

}  // namespace domain
}  // namespace calendar

#endif  // CALENDAR_NATIVE_DOMAIN_CALENDAR_SEARCH_QUERY_ADAPTER_HH_
