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

#include <atomic>
#include <string>
#include <thread>
#include <vector>

#include "domain/calendar_event_repository.hh"
#include "domain/calendar_reminder_repository.hh"
#include "harness.hh"
#include "testing_factories.hh"

namespace {

using ::calendar::base::Date;
using ::calendar::base::OffsetDateTime;
using ::calendar::domain::CalendarEvent;
using ::calendar::domain::CalendarEventRepository;
using ::calendar::domain::CalendarEventResolution;
using ::calendar::domain::CalendarReminder;
using ::calendar::domain::CalendarReminderRepository;
using ::calendar::domain::CalendarSearchCriteria;
using ::calendar::domain::CalendarSearchSnapshot;
using ::calendar::testing::At;
using ::calendar::testing::MakeEvent;
using ::calendar::testing::MakeReminder;
using ::calendar::testing::Ids;

CalendarSearchCriteria Criteria(const std::string& keyword,
                                std::optional<OffsetDateTime> start,
                                std::optional<OffsetDateTime> end, int limit,
                                bool title, bool location, bool note) {
  CalendarSearchCriteria criteria;
  std::string error;
  if (!CalendarSearchCriteria::TryCreate(keyword, start, end, limit, title,
                                         location, note, &criteria, &error)) {
    FAIL_TEST("criteria construction failed: " + error);
  }
  return criteria;
}

// --- CalendarEventRepository -----------------------------------------------

CALENDAR_TEST(event_repository_rejects_duplicate_identifiers_at_construction) {
  std::string error;
  CalendarEventRepository repository;
  EXPECT_FALSE(CalendarEventRepository::TryCreate(
      {MakeEvent("event-001", "A", At(2026, 8, 14, 9, 0)),
       MakeEvent("event-001", "B", At(2026, 8, 14, 11, 0))},
      &repository, &error));
  EXPECT_EQ(error, std::string("Duplicate calendar event ID: event-001"));
}

CALENDAR_TEST(event_repository_snapshot_orders_by_start_then_ordinal_id) {
  CalendarEventRepository repository;
  repository.ReplaceAll(
      {MakeEvent("event-b", "B", At(2026, 8, 14, 9, 0)),
       MakeEvent("event-a", "A", At(2026, 8, 14, 9, 0)),
       MakeEvent("event-c", "C", At(2026, 8, 14, 8, 0))});
  EXPECT_EQ(Ids(repository.Snapshot()),
            std::string("event-c,event-a,event-b"));
}

CALENDAR_TEST(event_repository_mutations_move_the_version_forward) {
  CalendarEventRepository repository;
  const long long initial = repository.Version();

  EXPECT_TRUE(repository.TryAdd(MakeEvent("event-1", "A", At(2026, 8, 14, 9, 0))));
  EXPECT_EQ(repository.Version(), initial + 1);

  // A duplicate add is rejected and must not bump the version.
  EXPECT_FALSE(
      repository.TryAdd(MakeEvent("event-1", "B", At(2026, 8, 14, 10, 0))));
  EXPECT_EQ(repository.Version(), initial + 1);

  EXPECT_TRUE(
      repository.TryUpdate(MakeEvent("event-1", "B", At(2026, 8, 14, 10, 0))));
  EXPECT_EQ(repository.Version(), initial + 2);

  EXPECT_FALSE(
      repository.TryUpdate(MakeEvent("absent", "C", At(2026, 8, 14, 10, 0))));
  EXPECT_EQ(repository.Version(), initial + 2);

  EXPECT_TRUE(repository.TryDelete("event-1"));
  EXPECT_EQ(repository.Version(), initial + 3);
  EXPECT_FALSE(repository.TryDelete("event-1"));
  EXPECT_FALSE(repository.TryDelete("   "));
  EXPECT_EQ(repository.Version(), initial + 3);
}

CALENDAR_TEST(event_repository_search_applies_half_open_period) {
  CalendarEventRepository repository;
  repository.ReplaceAll({
      // Ends exactly at the period start, so it must be excluded.
      MakeEvent("before", "Review", At(2026, 8, 13, 8, 0),
                At(2026, 8, 14, 9, 0)),
      // Starts exactly at the period end, so it must be excluded.
      MakeEvent("after", "Review", At(2026, 8, 14, 17, 0),
                At(2026, 8, 14, 18, 0)),
      MakeEvent("inside", "Review", At(2026, 8, 14, 10, 0),
                At(2026, 8, 14, 11, 0)),
      // Straddles the whole period, so it must be included.
      MakeEvent("straddle", "Review", At(2026, 8, 14, 8, 0),
                At(2026, 8, 14, 20, 0)),
  });

  const CalendarSearchCriteria criteria =
      Criteria("Review", At(2026, 8, 14, 9, 0), At(2026, 8, 14, 17, 0), 100,
               true, true, true);
  EXPECT_EQ(Ids(repository.Search(criteria)), std::string("straddle,inside"));
}

CALENDAR_TEST(event_repository_search_honours_field_selectors) {
  CalendarEventRepository repository;
  repository.ReplaceAll({
      MakeEvent("by-title", "Budget sync", At(2026, 8, 14, 9, 0),
                At(2026, 8, 14, 10, 0), "", ""),
      MakeEvent("by-location", "Standup", At(2026, 8, 14, 9, 0),
                At(2026, 8, 14, 10, 0), "", "Budget room"),
      MakeEvent("by-note", "Retro", At(2026, 8, 14, 9, 0),
                At(2026, 8, 14, 10, 0), "budget notes", ""),
  });

  EXPECT_EQ(Ids(repository.Search(Criteria("budget", std::nullopt,
                                           std::nullopt, 100, true, false,
                                           false))),
            std::string("by-title"));
  EXPECT_EQ(Ids(repository.Search(Criteria("budget", std::nullopt,
                                           std::nullopt, 100, false, true,
                                           false))),
            std::string("by-location"));
  EXPECT_EQ(Ids(repository.Search(Criteria("budget", std::nullopt,
                                           std::nullopt, 100, false, false,
                                           true))),
            std::string("by-note"));
  EXPECT_EQ(Ids(repository.Search(Criteria("BUDGET", std::nullopt,
                                           std::nullopt, 100, true, true,
                                           true))),
            std::string("by-location,by-note,by-title"));
}

CALENDAR_TEST(event_repository_search_applies_the_limit_after_ordering) {
  CalendarEventRepository repository;
  repository.ReplaceAll({
      MakeEvent("third", "Review", At(2026, 8, 14, 11, 0)),
      MakeEvent("first", "Review", At(2026, 8, 14, 9, 0)),
      MakeEvent("second", "Review", At(2026, 8, 14, 10, 0)),
  });
  EXPECT_EQ(Ids(repository.Search(Criteria("Review", std::nullopt,
                                           std::nullopt, 2, true, true,
                                           true))),
            std::string("first,second"));
}

CALENDAR_TEST(event_repository_empty_keyword_matches_everything) {
  CalendarEventRepository repository;
  repository.ReplaceAll({
      MakeEvent("a", "Alpha", At(2026, 8, 14, 9, 0)),
      MakeEvent("b", "Beta", At(2026, 8, 14, 10, 0)),
  });
  EXPECT_EQ(Ids(repository.Search(Criteria("", std::nullopt, std::nullopt, 100,
                                           true, true, true))),
            std::string("a,b"));
  EXPECT_EQ(Ids(repository.SearchByTerm("")), std::string("a,b"));
  EXPECT_EQ(Ids(repository.SearchByTerm("  ")), std::string("a,b"));
  EXPECT_EQ(Ids(repository.SearchByTerm("bet")), std::string("b"));
}

CALENDAR_TEST(event_repository_search_with_version_reports_the_version) {
  CalendarEventRepository repository;
  repository.ReplaceAll({MakeEvent("a", "Alpha", At(2026, 8, 14, 9, 0))});
  const CalendarSearchSnapshot snapshot = repository.SearchWithVersion(
      Criteria("", std::nullopt, std::nullopt, 100, true, true, true));
  EXPECT_EQ(snapshot.repository_version, repository.Version());
  EXPECT_EQ(Ids(snapshot.events), std::string("a"));
}

CALENDAR_TEST(event_repository_overlap_requires_a_positive_period) {
  CalendarEventRepository repository;
  repository.ReplaceAll({MakeEvent("a", "Alpha", At(2026, 8, 14, 9, 0))});

  std::vector<CalendarEvent> events;
  EXPECT_FALSE(repository.TryGetEventsOverlapping(
      At(2026, 8, 14, 9, 0), At(2026, 8, 14, 9, 0), &events));
  EXPECT_TRUE(repository.TryGetEventsOverlapping(
      At(2026, 8, 14, 0, 0), At(2026, 8, 15, 0, 0), &events));
  EXPECT_EQ(Ids(events), std::string("a"));
}

CALENDAR_TEST(event_repository_resolve_preserves_request_order_and_duplicates) {
  CalendarEventRepository repository;
  repository.ReplaceAll({
      MakeEvent("event-a", "A", At(2026, 8, 14, 11, 0)),
      MakeEvent("event-b", "B", At(2026, 8, 14, 9, 0)),
  });

  const CalendarEventResolution resolution =
      repository.ResolveByIds({"event-b", "missing", "event-a", "event-b", ""});
  EXPECT_EQ(Ids(resolution.events), std::string("event-b,event-a,event-b"));
  EXPECT_EQ(resolution.unresolved_ids.size(), static_cast<std::size_t>(2));
  EXPECT_EQ(resolution.unresolved_ids[0], std::string("missing"));
  EXPECT_EQ(resolution.unresolved_ids[1], std::string(""));
}

CALENDAR_TEST(event_repository_is_safe_under_concurrent_readers_and_writers) {
  CalendarEventRepository repository;
  repository.ReplaceAll({MakeEvent("seed", "Seed", At(2026, 8, 14, 9, 0))});

  std::atomic<bool> stop(false);
  std::atomic<int> reads(0);
  std::thread reader([&repository, &stop, &reads]() {
    while (!stop.load()) {
      const std::vector<CalendarEvent> snapshot = repository.Snapshot();
      if (!snapshot.empty()) reads.fetch_add(1);
    }
  });

  for (int index = 0; index < 400; ++index) {
    repository.TryAdd(MakeEvent("event-" + std::to_string(index), "Busy",
                                At(2026, 8, 14, 9, 0)));
    repository.TryDelete("event-" + std::to_string(index));
  }
  stop.store(true);
  reader.join();

  EXPECT_TRUE(reads.load() > 0);
  EXPECT_EQ(Ids(repository.Snapshot()), std::string("seed"));
}

// --- CalendarReminderRepository --------------------------------------------

CALENDAR_TEST(reminder_repository_orders_open_before_completed) {
  CalendarReminderRepository repository;
  repository.ReplaceAll({
      MakeReminder("done-early", "Done early", At(2026, 8, 14, 8, 0))
          .WithCompleted(true),
      MakeReminder("open-late", "Open late", At(2026, 8, 14, 18, 0)),
      MakeReminder("open-early", "Open early", At(2026, 8, 14, 9, 0)),
  });
  EXPECT_EQ(Ids(repository.Snapshot()),
            std::string("open-early,open-late,done-early"));
}

CALENDAR_TEST(reminder_repository_breaks_due_ties_on_ordinal_id) {
  CalendarReminderRepository repository;
  repository.ReplaceAll({
      MakeReminder("b", "B", At(2026, 8, 14, 9, 0)),
      MakeReminder("a", "A", At(2026, 8, 14, 9, 0)),
  });
  EXPECT_EQ(Ids(repository.Snapshot()), std::string("a,b"));
}

CALENDAR_TEST(reminder_repository_completion_drops_the_alarm_identifier) {
  CalendarReminderRepository repository;
  repository.ReplaceAll(
      {MakeReminder("r1", "Pay rent", At(2026, 8, 14, 9, 0)).WithAlarmId(77)});

  EXPECT_TRUE(repository.TryComplete("r1"));
  EXPECT_TRUE(repository.Find("r1")->is_completed());
  EXPECT_FALSE(repository.Find("r1")->alarm_id().has_value());

  EXPECT_TRUE(repository.TryReopen("r1"));
  EXPECT_FALSE(repository.Find("r1")->is_completed());

  EXPECT_FALSE(repository.TryComplete("absent"));
  EXPECT_FALSE(repository.TryComplete(""));
}

CALENDAR_TEST(reminder_repository_finds_reminders_linked_to_an_event) {
  CalendarReminder linked_ten;
  CalendarReminder linked_thirty;
  std::string error;
  EXPECT_TRUE(CalendarReminder::TryCreateForEvent(
      "reminder:event-001:10", "Review", At(2026, 8, 14, 9, 0), "event-001",
      10, "", &linked_ten, &error));
  EXPECT_TRUE(CalendarReminder::TryCreateForEvent(
      "reminder:event-001:30", "Review", At(2026, 8, 14, 9, 0), "event-001",
      30, "", &linked_thirty, &error));

  CalendarReminderRepository repository;
  repository.ReplaceAll(
      {linked_ten, linked_thirty,
       MakeReminder("standalone", "Pay rent", At(2026, 8, 14, 9, 0))});

  EXPECT_EQ(Ids(repository.FindByCalendarEventId("event-001")),
            std::string("reminder:event-001:30,reminder:event-001:10"));
  EXPECT_EQ(repository.FindByCalendarEventId("other").size(),
            static_cast<std::size_t>(0));
  EXPECT_EQ(repository.FindByCalendarEventId("").size(),
            static_cast<std::size_t>(0));
}

CALENDAR_TEST(reminder_repository_search_matches_title_and_note) {
  CalendarReminderRepository repository;
  repository.ReplaceAll({
      MakeReminder("by-title", "Budget review", At(2026, 8, 14, 9, 0)),
      MakeReminder("by-note", "Standup", At(2026, 8, 14, 10, 0), "budget doc"),
      MakeReminder("neither", "Retro", At(2026, 8, 14, 11, 0)),
  });
  EXPECT_EQ(Ids(repository.Search("BUDGET")),
            std::string("by-title,by-note"));
  EXPECT_EQ(Ids(repository.Search("")),
            std::string("by-title,by-note,neither"));
}

CALENDAR_TEST(reminder_repository_add_update_delete_respect_existence) {
  CalendarReminderRepository repository;
  EXPECT_TRUE(
      repository.TryAdd(MakeReminder("r1", "Pay rent", At(2026, 8, 14, 9, 0))));
  EXPECT_FALSE(
      repository.TryAdd(MakeReminder("r1", "Other", At(2026, 8, 14, 9, 0))));
  EXPECT_TRUE(repository.TryUpdate(
      MakeReminder("r1", "Pay rent early", At(2026, 8, 14, 8, 0))));
  EXPECT_EQ(repository.Find("r1")->title(), std::string("Pay rent early"));
  EXPECT_FALSE(repository.TryUpdate(
      MakeReminder("absent", "Nope", At(2026, 8, 14, 8, 0))));
  EXPECT_FALSE(repository.Find("absent").has_value());
  EXPECT_FALSE(repository.Find("").has_value());
  EXPECT_TRUE(repository.TryDelete("r1"));
  EXPECT_FALSE(repository.TryDelete("r1"));
}

}  // namespace
