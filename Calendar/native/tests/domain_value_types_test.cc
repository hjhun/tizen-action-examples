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

#include <string>

#include "base/local_zone.hh"
#include "domain/calendar_date_boundary.hh"
#include "domain/calendar_event.hh"
#include "domain/calendar_reminder.hh"
#include "domain/calendar_search_criteria.hh"
#include "domain/calendar_search_query_adapter.hh"
#include "harness.hh"

namespace {

using ::calendar::base::Date;
using ::calendar::base::OffsetDateTime;
using ::calendar::base::ScopedTimeZone;
using ::calendar::domain::CalendarDateBoundary;
using ::calendar::domain::CalendarEvent;
using ::calendar::domain::CalendarReminder;
using ::calendar::domain::CalendarSearchCriteria;
using ::calendar::domain::CalendarSearchQueryAdapter;

OffsetDateTime At(int year, int month, int day, int hour, int minute) {
  return OffsetDateTime::FromLocalParts(Date(year, month, day), hour, minute,
                                        0, 0, 540);
}

// --- CalendarEvent ---------------------------------------------------------

CALENDAR_TEST(event_create_trims_and_defaults_optional_text) {
  CalendarEvent created;
  std::string error;
  EXPECT_TRUE(CalendarEvent::TryCreate("event-001", "  Design review  ",
                                       At(2026, 8, 14, 9, 0),
                                       At(2026, 8, 14, 10, 0), "  note  ",
                                       "  Room 3  ", &created, &error));
  EXPECT_EQ(created.id(), std::string("event-001"));
  EXPECT_EQ(created.title(), std::string("Design review"));
  EXPECT_EQ(created.note(), std::string("note"));
  EXPECT_EQ(created.location(), std::string("Room 3"));
  EXPECT_EQ(created.DurationTicks(), 36000000000LL);
}

CALENDAR_TEST(event_create_rejects_non_positive_range) {
  CalendarEvent created;
  std::string error;
  EXPECT_FALSE(CalendarEvent::TryCreate("event-001", "Title",
                                        At(2026, 8, 14, 10, 0),
                                        At(2026, 8, 14, 10, 0), "", "",
                                        &created, &error));
  EXPECT_EQ(error, std::string("An event must end after it starts."));

  EXPECT_FALSE(CalendarEvent::TryCreate("event-001", "Title",
                                        At(2026, 8, 14, 11, 0),
                                        At(2026, 8, 14, 10, 0), "", "",
                                        &created, &error));
}

CALENDAR_TEST(event_create_rejects_blank_identity_fields) {
  CalendarEvent created;
  std::string error;
  EXPECT_FALSE(CalendarEvent::TryCreate("   ", "Title", At(2026, 8, 14, 9, 0),
                                        At(2026, 8, 14, 10, 0), "", "",
                                        &created, &error));
  EXPECT_EQ(error, std::string("An event ID is required."));

  EXPECT_FALSE(CalendarEvent::TryCreate("event-001", " \t ",
                                        At(2026, 8, 14, 9, 0),
                                        At(2026, 8, 14, 10, 0), "", "",
                                        &created, &error));
  EXPECT_EQ(error, std::string("An event title is required."));
}

// --- CalendarReminder ------------------------------------------------------

CALENDAR_TEST(reminder_create_starts_open_and_unlinked) {
  CalendarReminder created;
  std::string error;
  EXPECT_TRUE(CalendarReminder::TryCreate("reminder-1", " Pay rent ",
                                          At(2026, 8, 14, 9, 0), " soon ",
                                          &created, &error));
  EXPECT_EQ(created.title(), std::string("Pay rent"));
  EXPECT_EQ(created.note(), std::string("soon"));
  EXPECT_FALSE(created.is_completed());
  EXPECT_FALSE(created.calendar_event_id().has_value());
  EXPECT_FALSE(created.offset_minutes().has_value());
  EXPECT_FALSE(created.alarm_id().has_value());
}

CALENDAR_TEST(reminder_for_event_subtracts_the_offset_from_the_start) {
  CalendarReminder created;
  std::string error;
  EXPECT_TRUE(CalendarReminder::TryCreateForEvent(
      "reminder:event-001:30", "Design review", At(2026, 8, 14, 9, 0),
      "event-001", 30, "note", &created, &error));
  EXPECT_EQ(created.due_at(), At(2026, 8, 14, 8, 30));
  EXPECT_EQ(created.calendar_event_id().value(), std::string("event-001"));
  EXPECT_EQ(created.offset_minutes().value(), 30);
}

CALENDAR_TEST(reminder_for_event_rejects_unknown_offset) {
  CalendarReminder created;
  std::string error;
  EXPECT_FALSE(CalendarReminder::TryCreateForEvent(
      "reminder:event-001:45", "Design review", At(2026, 8, 14, 9, 0),
      "event-001", 45, "", &created, &error));
  EXPECT_EQ(error,
            std::string("An event-linked reminder offset must be one of 10, "
                        "30, 60, 1440 minutes."));

  EXPECT_FALSE(CalendarReminder::TryCreateForEvent(
      "reminder", "Design review", At(2026, 8, 14, 9, 0), "  ", 30, "",
      &created, &error));
  EXPECT_EQ(error, std::string("A linked calendar event ID is required."));
}

CALENDAR_TEST(reminder_allowed_offsets_match_the_reference) {
  const std::vector<int>& allowed = CalendarReminder::AllowedOffsetMinutes();
  EXPECT_EQ(allowed.size(), static_cast<std::size_t>(4));
  EXPECT_EQ(allowed[0], 10);
  EXPECT_EQ(allowed[1], 30);
  EXPECT_EQ(allowed[2], 60);
  EXPECT_EQ(allowed[3], 1440);
  EXPECT_TRUE(CalendarReminder::IsAllowedOffset(1440));
  EXPECT_FALSE(CalendarReminder::IsAllowedOffset(0));
}

// --- CalendarDateBoundary --------------------------------------------------

CALENDAR_TEST(date_boundary_delegates_to_the_process_time_zone) {
  const ScopedTimeZone zone("Asia/Seoul");
  EXPECT_EQ(CalendarDateBoundary::AtStartOfDay(Date(2026, 8, 14))
                .ToRoundTripString(),
            std::string("2026-08-14T00:00:00.0000000+09:00"));
}

// --- CalendarSearchCriteria ------------------------------------------------

CALENDAR_TEST(criteria_rejects_all_false_fields) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_FALSE(CalendarSearchCriteria::TryCreate("term", std::nullopt,
                                                 std::nullopt, 20, false,
                                                 false, false, &criteria,
                                                 &error));
  EXPECT_EQ(error,
            std::string("At least one calendar text field must be selected."));
}

CALENDAR_TEST(criteria_rejects_out_of_range_limit_and_long_keyword) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_FALSE(CalendarSearchCriteria::TryCreate("term", std::nullopt,
                                                 std::nullopt, 0, true, true,
                                                 true, &criteria, &error));
  EXPECT_FALSE(CalendarSearchCriteria::TryCreate("term", std::nullopt,
                                                 std::nullopt, 101, true, true,
                                                 true, &criteria, &error));

  const std::string too_long(513, 'x');
  EXPECT_FALSE(CalendarSearchCriteria::TryCreate(too_long, std::nullopt,
                                                 std::nullopt, 20, true, true,
                                                 true, &criteria, &error));
  EXPECT_EQ(error,
            std::string("The search keyword must not exceed 512 characters."));

  const std::string at_limit(512, 'x');
  EXPECT_TRUE(CalendarSearchCriteria::TryCreate(at_limit, std::nullopt,
                                                std::nullopt, 100, true, true,
                                                true, &criteria, &error));
}

CALENDAR_TEST(criteria_rejects_a_non_positive_period) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_FALSE(CalendarSearchCriteria::TryCreate(
      "term", At(2026, 8, 14, 9, 0), At(2026, 8, 14, 9, 0), 20, true, true,
      true, &criteria, &error));
  EXPECT_EQ(error,
            std::string("The search period end must be after its start."));

  // A single open bound is allowed.
  EXPECT_TRUE(CalendarSearchCriteria::TryCreate(
      "term", At(2026, 8, 14, 9, 0), std::nullopt, 20, true, true, true,
      &criteria, &error));
  EXPECT_TRUE(CalendarSearchCriteria::TryCreate(
      "term", std::nullopt, At(2026, 8, 14, 9, 0), 20, true, true, true,
      &criteria, &error));
}

CALENDAR_TEST(criteria_trims_the_keyword) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_TRUE(CalendarSearchCriteria::TryCreate("  review  ", std::nullopt,
                                                std::nullopt, 20, true, false,
                                                false, &criteria, &error));
  EXPECT_EQ(criteria.keyword(), std::string("review"));
  EXPECT_TRUE(criteria.search_title());
  EXPECT_FALSE(criteria.search_location());
  EXPECT_FALSE(criteria.search_note());
}

// --- CalendarSearchQueryAdapter --------------------------------------------

CALENDAR_TEST(adapter_defaults_all_fields_when_omitted) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_TRUE(CalendarSearchQueryAdapter::TryCreate("review", "", "", 0, false,
                                                    false, false, &criteria,
                                                    &error));
  EXPECT_TRUE(criteria.search_title());
  EXPECT_TRUE(criteria.search_location());
  EXPECT_TRUE(criteria.search_note());
  // A non-positive requested limit becomes 20.
  EXPECT_EQ(criteria.limit(), 20);
  EXPECT_FALSE(criteria.start_inclusive().has_value());
  EXPECT_FALSE(criteria.end_exclusive().has_value());
}

CALENDAR_TEST(adapter_honours_an_explicit_partial_field_selection) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_TRUE(CalendarSearchQueryAdapter::TryCreate("review", "", "", 5, false,
                                                    true, false, &criteria,
                                                    &error));
  EXPECT_FALSE(criteria.search_title());
  EXPECT_TRUE(criteria.search_location());
  EXPECT_FALSE(criteria.search_note());
  EXPECT_EQ(criteria.limit(), 5);
}

CALENDAR_TEST(adapter_clamps_the_limit_to_one_hundred) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_TRUE(CalendarSearchQueryAdapter::TryCreate("review", "", "", 5000,
                                                    true, true, true,
                                                    &criteria, &error));
  EXPECT_EQ(criteria.limit(), 100);
}

CALENDAR_TEST(adapter_requires_iso_timestamps_with_an_explicit_offset) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_TRUE(CalendarSearchQueryAdapter::TryCreate(
      "review", "2026-08-01T00:00:00+09:00", "2026-09-01T00:00:00+09:00", 20,
      true, true, true, &criteria, &error));
  EXPECT_EQ(criteria.start_inclusive().value(), At(2026, 8, 1, 0, 0));
  EXPECT_EQ(criteria.end_exclusive().value(), At(2026, 9, 1, 0, 0));

  EXPECT_TRUE(CalendarSearchQueryAdapter::TryCreate(
      "review", "2026-08-01T00:00:00Z", "", 20, true, true, true, &criteria,
      &error));

  EXPECT_FALSE(CalendarSearchQueryAdapter::TryCreate(
      "review", "2026-08-01", "", 20, true, true, true, &criteria, &error));
  EXPECT_EQ(error,
            std::string("StartDate and EndDate must be empty or valid ISO 8601 "
                        "timestamps with an explicit UTC offset."));

  EXPECT_FALSE(CalendarSearchQueryAdapter::TryCreate(
      "review", "2026-08-01T00:00:00", "", 20, true, true, true, &criteria,
      &error));
}

CALENDAR_TEST(adapter_surfaces_criteria_validation_errors) {
  CalendarSearchCriteria criteria;
  std::string error;
  EXPECT_FALSE(CalendarSearchQueryAdapter::TryCreate(
      "review", "2026-09-01T00:00:00+09:00", "2026-08-01T00:00:00+09:00", 20,
      true, true, true, &criteria, &error));
  EXPECT_EQ(error,
            std::string("The search period end must be after its start."));
}

}  // namespace
