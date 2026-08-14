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

#include "provider/calendar_service_core.hh"

#include <algorithm>

#include "base/json.hh"
#include "base/strings.hh"
#include "domain/calendar_search_query_adapter.hh"

namespace calendar {
namespace provider {
namespace {

ServiceStatus Success() { return {true, ""}; }

ServiceStatus Failure(const std::string &reason) { return {false, reason}; }

ServiceStatus FromCommand(const usecases::CalendarCommandResult &result) {
  return result.success ? Success() : Failure(result.reason);
}

bool ToDomain(const CalendarWireEvent &wire, domain::CalendarEvent *event,
              std::string *error) {
  if (wire.id.size() > 256) {
    *error = "A stable event ID must not exceed 256 characters.";
    return false;
  }
  base::OffsetDateTime start;
  base::OffsetDateTime end;
  if (!base::OffsetDateTime::TryParseFlexible(wire.start_date, &start) ||
      !base::OffsetDateTime::TryParseFlexible(wire.end_date, &end)) {
    *error = "Calendar requires a valid start and end date.";
    return false;
  }
  return domain::CalendarEvent::TryCreate(
      wire.id, wire.title, start, end, wire.note, wire.location, event, error);
}

CalendarWireEvent ToWire(const domain::CalendarEvent &event) {
  return {event.id(),
          event.title(),
          event.start().ToRoundTripString(),
          event.end().ToRoundTripString(),
          event.note(),
          event.location()};
}

base::JsonValue EventDocument(const domain::CalendarEvent &event) {
  base::JsonValue json = base::JsonValue::Object();
  json.Set("id", base::JsonValue::String(event.id()));
  json.Set("title", base::JsonValue::String(event.title()));
  json.Set("start", base::JsonValue::String(event.start().ToRoundTripString()));
  json.Set("end", base::JsonValue::String(event.end().ToRoundTripString()));
  json.Set("note", base::JsonValue::String(event.note()));
  json.Set("location", base::JsonValue::String(event.location()));
  return json;
}

bool ToDomain(const ReminderWireEntity &wire,
              domain::CalendarReminder *reminder, std::string *error) {
  if (wire.id.size() > 256) {
    *error = "A stable reminder ID must not exceed 256 characters.";
    return false;
  }
  base::OffsetDateTime due_at;
  if (!base::OffsetDateTime::TryParseFlexible(wire.due_date, &due_at)) {
    *error = "Reminder requires a valid due date.";
    return false;
  }
  if (!domain::CalendarReminder::TryCreate(wire.id, wire.title, due_at,
                                           wire.note, reminder, error)) {
    return false;
  }
  *reminder = reminder->WithCompleted(wire.completed);
  return true;
}

ReminderWireEntity ToWire(const domain::CalendarReminder &reminder) {
  return {reminder.id(), reminder.title(),
          reminder.due_at().ToRoundTripString(), reminder.note(),
          reminder.is_completed()};
}

} // namespace

CalendarServiceCore::CalendarServiceCore(
    domain::CalendarEventRepository *repository,
    usecases::CalendarCommandService *commands)
    : repository_(repository), commands_(commands) {}

ServiceStatus CalendarServiceCore::AddEvent(const CalendarWireEvent &wire) {
  if (commands_ == nullptr) {
    return Failure("Calendar mutation service is unavailable.");
  }
  domain::CalendarEvent event;
  std::string error;
  return ToDomain(wire, &event, &error)
             ? FromCommand(commands_->CreateEvent(event, {}))
             : Failure(error);
}

ServiceStatus CalendarServiceCore::UpdateEvent(const CalendarWireEvent &wire) {
  if (commands_ == nullptr) {
    return Failure("Calendar mutation service is unavailable.");
  }
  domain::CalendarEvent event;
  std::string error;
  return ToDomain(wire, &event, &error)
             ? FromCommand(commands_->UpdateEvent(event, {}))
             : Failure(error);
}

ServiceStatus CalendarServiceCore::RemoveEvent(const std::string &id) {
  if (commands_ == nullptr) {
    return Failure("Calendar mutation service is unavailable.");
  }
  if (base::IsBlank(id) || id.size() > 256) {
    return Failure("A stable event ID is required.");
  }
  return FromCommand(commands_->DeleteEvent(id));
}

CalendarSearchResult CalendarServiceCore::Search(const std::string &keyword,
                                                 int number) const {
  CalendarSearchResult result;
  if (keyword.size() > domain::kMaxSearchKeywordLength) {
    result.status =
        Failure("The search keyword must not exceed 512 characters.");
    return result;
  }
  const int limit = number <= 0 ? domain::kDefaultSearchLimit
                                : std::min(number, domain::kMaxSearchLimit);
  auto events = repository_->SearchByTerm(keyword);
  if (events.size() > static_cast<std::size_t>(limit)) {
    events.resize(static_cast<std::size_t>(limit));
  }
  for (const auto &event : events)
    result.events.push_back(ToWire(event));
  result.status = Success();
  return result;
}

CalendarSearchResult
CalendarServiceCore::SearchInPeriod(const CalendarWireSearch &query) const {
  CalendarSearchResult result;
  domain::CalendarSearchCriteria criteria;
  std::string error;
  if (!domain::CalendarSearchQueryAdapter::TryCreate(
          query.keyword, query.start_date, query.end_date, query.number,
          query.search_title, query.search_location, query.search_note,
          &criteria, &error)) {
    result.status = Failure(error);
    return result;
  }
  for (const auto &event : repository_->Search(criteria)) {
    result.events.push_back(ToWire(event));
  }
  result.status = Success();
  return result;
}

CalendarResolveResult
CalendarServiceCore::GetEventByIds(const std::vector<std::string> &ids) const {
  CalendarResolveResult result;
  if (ids.size() > 100 ||
      std::any_of(ids.begin(), ids.end(), [](const std::string &id) {
        return base::IsBlank(id) || id.size() > 256;
      })) {
    result.status = Failure(
        "ids must contain at most 100 non-empty stable IDs, each no longer "
        "than 256 characters.");
    return result;
  }
  auto resolution = repository_->ResolveByIds(ids);
  for (const auto &event : resolution.events) {
    result.events.push_back(ToWire(event));
  }
  result.unresolved_ids = std::move(resolution.unresolved_ids);
  result.status = Success();
  return result;
}

ServiceStatus
CalendarServiceCore::ToPresentation(const CalendarWireEvent &wire,
                                    PresentationData *presentation) const {
  domain::CalendarEvent event;
  std::string error;
  if (!ToDomain(wire, &event, &error))
    return Failure(error);
  presentation->template_value = "calendar-event-card-v1";
  presentation->document = EventDocument(event).ToString();
  return Success();
}

ScheduleServiceCore::ScheduleServiceCore(
    domain::CalendarReminderRepository *repository,
    usecases::CalendarCommandService *commands)
    : repository_(repository), commands_(commands) {}

ServiceStatus
ScheduleServiceCore::CreateReminder(const ReminderWireEntity &wire) {
  if (commands_ == nullptr) {
    return Failure("Schedule reminder mutation service is unavailable.");
  }
  domain::CalendarReminder reminder;
  std::string error;
  return ToDomain(wire, &reminder, &error)
             ? FromCommand(commands_->CreateReminder(reminder))
             : Failure(error);
}

ServiceStatus
ScheduleServiceCore::UpdateReminder(const ReminderWireEntity &wire) {
  if (commands_ == nullptr) {
    return Failure("Schedule reminder mutation service is unavailable.");
  }
  domain::CalendarReminder reminder;
  std::string error;
  return ToDomain(wire, &reminder, &error)
             ? FromCommand(commands_->UpdateReminder(reminder))
             : Failure(error);
}

ServiceStatus ScheduleServiceCore::CompleteReminder(const std::string &id) {
  if (commands_ == nullptr) {
    return Failure("Schedule reminder mutation service is unavailable.");
  }
  if (base::IsBlank(id) || id.size() > 256) {
    return Failure("A stable reminder ID is required.");
  }
  return FromCommand(commands_->SetReminderCompleted(id, true));
}

ServiceStatus ScheduleServiceCore::DeleteReminder(const std::string &id) {
  if (commands_ == nullptr) {
    return Failure("Schedule reminder mutation service is unavailable.");
  }
  if (base::IsBlank(id) || id.size() > 256) {
    return Failure("A stable reminder ID is required.");
  }
  return FromCommand(commands_->DeleteReminder(id));
}

ReminderSearchResult
ScheduleServiceCore::SearchReminder(const std::string &keyword,
                                    int number) const {
  ReminderSearchResult result;
  if (keyword.size() > domain::kMaxSearchKeywordLength) {
    result.status =
        Failure("The search keyword must not exceed 512 characters.");
    return result;
  }
  const int limit = number <= 0 ? domain::kDefaultSearchLimit
                                : std::min(number, domain::kMaxSearchLimit);
  for (const auto &reminder : repository_->Search(keyword)) {
    if (reminder.calendar_event_id().has_value())
      continue;
    if (static_cast<int>(result.reminders.size()) >= limit)
      break;
    result.reminders.push_back(ToWire(reminder));
  }
  result.status = Success();
  return result;
}

ReservationResult ScheduleServiceCore::GetReservations() const {
  return {UnsupportedReservation()};
}

ServiceStatus ScheduleServiceCore::UnsupportedReservation() const {
  return Failure("Recording and viewing reservations are not supported by "
                 "the Calendar reminder provider.");
}

} // namespace provider
} // namespace calendar
