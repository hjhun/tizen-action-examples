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

#ifndef CALENDAR_NATIVE_DOMAIN_CALENDAR_EVENT_REPOSITORY_HH_
#define CALENDAR_NATIVE_DOMAIN_CALENDAR_EVENT_REPOSITORY_HH_

#include <cstdint>
#include <map>
#include <mutex>
#include <string>
#include <vector>

#include "base/offset_date_time.hh"
#include "domain/calendar_event.hh"
#include "domain/calendar_search_criteria.hh"

namespace calendar {
namespace domain {

// Batch resolution result. Request order is preserved, duplicates are
// returned once per request, and every identifier that did not resolve is
// reported explicitly.
struct CalendarEventResolution {
  std::vector<CalendarEvent> events;
  std::vector<std::string> unresolved_ids;
};

// A search result together with the repository version it was taken at, so a
// caller can tell whether its cached result list is still current.
struct CalendarSearchSnapshot {
  std::vector<CalendarEvent> events;
  long long repository_version = 0;
};

// Thread-safe store of calendar events, shared unchanged between the EFL UI
// and every Action provider in the process. Every accessor takes the lock and
// returns an ordered copy, so a caller can never observe a torn view or hold
// a reference into mutating state.
class CalendarEventRepository {
 public:
  CalendarEventRepository() = default;

  CalendarEventRepository(const CalendarEventRepository&) = delete;
  CalendarEventRepository& operator=(const CalendarEventRepository&) = delete;

  // Fails on a duplicate identifier rather than silently keeping one entry.
  static bool TryCreate(const std::vector<CalendarEvent>& events,
                        CalendarEventRepository* repository,
                        std::string* error);

  // Monotonic counter bumped by every accepted mutation.
  long long Version() const;

  bool TryAdd(const CalendarEvent& calendar_event);
  bool TryUpdate(const CalendarEvent& calendar_event);
  bool TryDelete(const std::string& id);

  // Ordered by start, then by ordinal identifier; used for persistence,
  // provider responses and rollback.
  std::vector<CalendarEvent> Snapshot() const;

  // Restores a previously captured snapshot. Duplicate identifiers in the
  // input are dropped after the first occurrence, which cannot happen for a
  // snapshot this repository produced.
  void ReplaceAll(const std::vector<CalendarEvent>& events);

  // Legacy keyword search over all three text fields with no period bound,
  // backing Tv_Tizen.Action.Calendar_Search.
  std::vector<CalendarEvent> SearchByTerm(const std::string& term) const;

  std::vector<CalendarEvent> Search(
      const CalendarSearchCriteria& criteria) const;

  CalendarSearchSnapshot SearchWithVersion(
      const CalendarSearchCriteria& criteria) const;

  // Half-open overlap: [start_inclusive, end_exclusive). Returns false when
  // the period is empty or inverted.
  bool TryGetEventsOverlapping(const base::OffsetDateTime& start_inclusive,
                               const base::OffsetDateTime& end_exclusive,
                               std::vector<CalendarEvent>* events) const;

  CalendarEventResolution ResolveByIds(
      const std::vector<std::string>& ids) const;

 private:
  // Callers must already hold events_mutex_.
  std::vector<CalendarEvent> OrderedLocked() const;

  mutable std::mutex events_mutex_;
  std::map<std::string, CalendarEvent> events_by_id_;
  long long version_ = 0;
};

}  // namespace domain
}  // namespace calendar

#endif  // CALENDAR_NATIVE_DOMAIN_CALENDAR_EVENT_REPOSITORY_HH_
