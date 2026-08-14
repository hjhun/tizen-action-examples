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

#include "domain/calendar_search_query_adapter.hh"

#include <algorithm>
#include <optional>

#include "base/offset_date_time.hh"
#include "base/strings.hh"

namespace calendar {
namespace domain {
namespace {

bool TryParseOptionalTimestamp(const std::string& value,
                               std::optional<base::OffsetDateTime>* parsed) {
  if (base::IsBlank(value)) {
    parsed->reset();
    return true;
  }

  base::OffsetDateTime timestamp;
  if (!base::OffsetDateTime::TryParseStrict(base::Trim(value), &timestamp)) {
    return false;
  }
  *parsed = timestamp;
  return true;
}

}  // namespace

bool CalendarSearchQueryAdapter::TryCreate(
    const std::string& keyword, const std::string& start_date,
    const std::string& end_date, int requested_limit, bool search_title,
    bool search_location, bool search_note, CalendarSearchCriteria* criteria,
    std::string* error) {
  std::optional<base::OffsetDateTime> start_inclusive;
  std::optional<base::OffsetDateTime> end_exclusive;
  if (!TryParseOptionalTimestamp(start_date, &start_inclusive) ||
      !TryParseOptionalTimestamp(end_date, &end_exclusive)) {
    *error =
        "StartDate and EndDate must be empty or valid ISO 8601 timestamps "
        "with an explicit UTC offset.";
    return false;
  }

  const bool has_explicit_field_selection =
      search_title || search_location || search_note;
  const int limit = requested_limit <= 0
                        ? kDefaultSearchLimit
                        : std::min(requested_limit, kMaxSearchLimit);

  return CalendarSearchCriteria::TryCreate(
      keyword, start_inclusive, end_exclusive, limit,
      has_explicit_field_selection ? search_title : true,
      has_explicit_field_selection ? search_location : true,
      has_explicit_field_selection ? search_note : true, criteria, error);
}

}  // namespace domain
}  // namespace calendar
