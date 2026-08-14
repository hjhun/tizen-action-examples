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

#ifndef CALENDAR_NATIVE_DOMAIN_CALENDAR_SEARCH_CRITERIA_HH_
#define CALENDAR_NATIVE_DOMAIN_CALENDAR_SEARCH_CRITERIA_HH_

#include <optional>
#include <string>

#include "base/offset_date_time.hh"

namespace calendar {
namespace domain {

// Bounds enforced on externally supplied search input.
constexpr std::size_t kMaxSearchKeywordLength = 512;
constexpr int kMinSearchLimit = 1;
constexpr int kMaxSearchLimit = 100;

// A validated calendar query. The period is half open:
// [start_inclusive, end_exclusive). Either bound may be absent, which leaves
// that side open.
class CalendarSearchCriteria {
 public:
  CalendarSearchCriteria() = default;

  static bool TryCreate(const std::string& keyword,
                        const std::optional<base::OffsetDateTime>& start,
                        const std::optional<base::OffsetDateTime>& end,
                        int limit, bool search_title, bool search_location,
                        bool search_note, CalendarSearchCriteria* criteria,
                        std::string* error);

  const std::string& keyword() const { return keyword_; }

  const std::optional<base::OffsetDateTime>& start_inclusive() const {
    return start_inclusive_;
  }

  const std::optional<base::OffsetDateTime>& end_exclusive() const {
    return end_exclusive_;
  }

  int limit() const { return limit_; }
  bool search_title() const { return search_title_; }
  bool search_location() const { return search_location_; }
  bool search_note() const { return search_note_; }

 private:
  std::string keyword_;
  std::optional<base::OffsetDateTime> start_inclusive_;
  std::optional<base::OffsetDateTime> end_exclusive_;
  int limit_ = kMaxSearchLimit;
  bool search_title_ = true;
  bool search_location_ = true;
  bool search_note_ = true;
};

}  // namespace domain
}  // namespace calendar

#endif  // CALENDAR_NATIVE_DOMAIN_CALENDAR_SEARCH_CRITERIA_HH_
