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

#include "domain/calendar_search_criteria.hh"

#include "base/strings.hh"

namespace calendar {
namespace domain {

bool CalendarSearchCriteria::TryCreate(
    const std::string& keyword,
    const std::optional<base::OffsetDateTime>& start,
    const std::optional<base::OffsetDateTime>& end, int limit,
    bool search_title, bool search_location, bool search_note,
    CalendarSearchCriteria* criteria, std::string* error) {
  const std::string trimmed = base::Trim(keyword);
  if (trimmed.size() > kMaxSearchKeywordLength) {
    *error = "The search keyword must not exceed 512 characters.";
    return false;
  }
  if (start.has_value() && end.has_value() && *end <= *start) {
    *error = "The search period end must be after its start.";
    return false;
  }
  if (limit < kMinSearchLimit || limit > kMaxSearchLimit) {
    *error = "The search limit must be between 1 and 100.";
    return false;
  }
  if (!search_title && !search_location && !search_note) {
    *error = "At least one calendar text field must be selected.";
    return false;
  }

  CalendarSearchCriteria candidate;
  candidate.keyword_ = trimmed;
  candidate.start_inclusive_ = start;
  candidate.end_exclusive_ = end;
  candidate.limit_ = limit;
  candidate.search_title_ = search_title;
  candidate.search_location_ = search_location;
  candidate.search_note_ = search_note;
  *criteria = candidate;
  error->clear();
  return true;
}

}  // namespace domain
}  // namespace calendar
