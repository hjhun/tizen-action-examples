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

#include "domain/calendar_event_repository.hh"

#include <algorithm>

#include "base/strings.hh"

namespace calendar {
namespace domain {
namespace {

// Ordering shared by every list this repository returns: chronological, with
// the ordinal identifier breaking ties so the result is total and stable.
bool IsBeforeInDisplayOrder(const CalendarEvent& left,
                            const CalendarEvent& right) {
  if (left.start() != right.start()) return left.start() < right.start();
  return left.id() < right.id();
}

bool MatchesSelectedFields(const CalendarEvent& calendar_event,
                           const CalendarSearchCriteria& criteria) {
  return (criteria.search_title() &&
          base::ContainsIgnoreCase(calendar_event.title(),
                                   criteria.keyword())) ||
         (criteria.search_location() &&
          base::ContainsIgnoreCase(calendar_event.location(),
                                   criteria.keyword())) ||
         (criteria.search_note() &&
          base::ContainsIgnoreCase(calendar_event.note(), criteria.keyword()));
}

bool MatchesAnyField(const CalendarEvent& calendar_event,
                     const std::string& term) {
  return base::ContainsIgnoreCase(calendar_event.title(), term) ||
         base::ContainsIgnoreCase(calendar_event.location(), term) ||
         base::ContainsIgnoreCase(calendar_event.note(), term);
}

}  // namespace

bool CalendarEventRepository::TryCreate(const std::vector<CalendarEvent>& events,
                                        CalendarEventRepository* repository,
                                        std::string* error) {
  std::map<std::string, CalendarEvent> events_by_id;
  for (const CalendarEvent& calendar_event : events) {
    if (!events_by_id.emplace(calendar_event.id(), calendar_event).second) {
      *error = "Duplicate calendar event ID: " + calendar_event.id();
      return false;
    }
  }

  std::lock_guard<std::mutex> guard(repository->events_mutex_);
  repository->events_by_id_ = std::move(events_by_id);
  error->clear();
  return true;
}

long long CalendarEventRepository::Version() const {
  std::lock_guard<std::mutex> guard(events_mutex_);
  return version_;
}

bool CalendarEventRepository::TryAdd(const CalendarEvent& calendar_event) {
  std::lock_guard<std::mutex> guard(events_mutex_);
  if (!events_by_id_.emplace(calendar_event.id(), calendar_event).second) {
    return false;
  }
  ++version_;
  return true;
}

bool CalendarEventRepository::TryUpdate(const CalendarEvent& calendar_event) {
  std::lock_guard<std::mutex> guard(events_mutex_);
  const auto existing = events_by_id_.find(calendar_event.id());
  if (existing == events_by_id_.end()) return false;
  existing->second = calendar_event;
  ++version_;
  return true;
}

bool CalendarEventRepository::TryDelete(const std::string& id) {
  if (base::IsBlank(id)) return false;
  std::lock_guard<std::mutex> guard(events_mutex_);
  if (events_by_id_.erase(id) == 0) return false;
  ++version_;
  return true;
}

std::vector<CalendarEvent> CalendarEventRepository::OrderedLocked() const {
  std::vector<CalendarEvent> ordered;
  ordered.reserve(events_by_id_.size());
  for (const auto& entry : events_by_id_) ordered.push_back(entry.second);
  std::stable_sort(ordered.begin(), ordered.end(), IsBeforeInDisplayOrder);
  return ordered;
}

std::vector<CalendarEvent> CalendarEventRepository::Snapshot() const {
  std::lock_guard<std::mutex> guard(events_mutex_);
  return OrderedLocked();
}

void CalendarEventRepository::ReplaceAll(
    const std::vector<CalendarEvent>& events) {
  std::map<std::string, CalendarEvent> replacement;
  for (const CalendarEvent& calendar_event : events) {
    replacement.emplace(calendar_event.id(), calendar_event);
  }

  std::lock_guard<std::mutex> guard(events_mutex_);
  events_by_id_ = std::move(replacement);
  ++version_;
}

std::vector<CalendarEvent> CalendarEventRepository::SearchByTerm(
    const std::string& term) const {
  const std::string trimmed = base::Trim(term);
  std::lock_guard<std::mutex> guard(events_mutex_);

  std::vector<CalendarEvent> matches;
  for (const CalendarEvent& calendar_event : OrderedLocked()) {
    if (trimmed.empty() || MatchesAnyField(calendar_event, trimmed)) {
      matches.push_back(calendar_event);
    }
  }
  return matches;
}

CalendarSearchSnapshot CalendarEventRepository::SearchWithVersion(
    const CalendarSearchCriteria& criteria) const {
  std::lock_guard<std::mutex> guard(events_mutex_);

  CalendarSearchSnapshot snapshot;
  snapshot.repository_version = version_;
  for (const CalendarEvent& calendar_event : OrderedLocked()) {
    if (static_cast<int>(snapshot.events.size()) >= criteria.limit()) break;
    if (!criteria.keyword().empty() &&
        !MatchesSelectedFields(calendar_event, criteria)) {
      continue;
    }
    if (criteria.start_inclusive().has_value() &&
        !(calendar_event.end() > *criteria.start_inclusive())) {
      continue;
    }
    if (criteria.end_exclusive().has_value() &&
        !(calendar_event.start() < *criteria.end_exclusive())) {
      continue;
    }
    snapshot.events.push_back(calendar_event);
  }
  return snapshot;
}

std::vector<CalendarEvent> CalendarEventRepository::Search(
    const CalendarSearchCriteria& criteria) const {
  return SearchWithVersion(criteria).events;
}

bool CalendarEventRepository::TryGetEventsOverlapping(
    const base::OffsetDateTime& start_inclusive,
    const base::OffsetDateTime& end_exclusive,
    std::vector<CalendarEvent>* events) const {
  if (end_exclusive <= start_inclusive) return false;

  std::lock_guard<std::mutex> guard(events_mutex_);
  events->clear();
  for (const CalendarEvent& calendar_event : OrderedLocked()) {
    if (calendar_event.start() < end_exclusive &&
        calendar_event.end() > start_inclusive) {
      events->push_back(calendar_event);
    }
  }
  return true;
}

CalendarEventResolution CalendarEventRepository::ResolveByIds(
    const std::vector<std::string>& ids) const {
  CalendarEventResolution resolution;
  std::lock_guard<std::mutex> guard(events_mutex_);
  for (const std::string& id : ids) {
    const auto found = base::IsBlank(id) ? events_by_id_.end()
                                         : events_by_id_.find(id);
    if (found == events_by_id_.end()) {
      resolution.unresolved_ids.push_back(id);
      continue;
    }
    resolution.events.push_back(found->second);
  }
  return resolution;
}

}  // namespace domain
}  // namespace calendar
