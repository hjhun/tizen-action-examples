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

#include "state/calendar_projection.hh"

#include <string>
#include <vector>

#include "harness.hh"
#include "testing_factories.hh"

namespace calendar {
namespace {

void PopulateProjectionRepository(domain::CalendarEventRepository *repository) {
  repository->TryAdd(testing::MakeEvent("all-day", "All day",
                                        testing::At(2026, 8, 24, 0, 0),
                                        testing::At(2026, 8, 25, 0, 0)));
  repository->TryAdd(
      testing::MakeEvent("meeting", "Meeting", testing::At(2026, 8, 24, 14, 0),
                         testing::At(2026, 8, 24, 15, 0), "Notes", "Studio"));
  repository->TryAdd(testing::MakeEvent("next-week", "Next week",
                                        testing::At(2026, 8, 31, 9, 0),
                                        testing::At(2026, 8, 31, 10, 0)));
}

CALENDAR_TEST(projection_derives_titles_and_half_open_ranges_for_every_mode) {
  domain::CalendarEventRepository repository;
  PopulateProjectionRepository(&repository);
  const base::Date selected(2026, 8, 24);

  auto month = state::CalendarProjection::Create(
      state::CalendarUiState::Create(selected), &repository);
  EXPECT_EQ(month.title, "August 2026");
  EXPECT_EQ(month.event_groups.size(), static_cast<std::size_t>(1));
  EXPECT_EQ(testing::Ids(month.event_groups[0].events), "all-day,meeting");

  auto week_state = state::CalendarUiState::Create(selected).ChangeViewMode(
      state::CalendarViewMode::kWeek);
  auto week = state::CalendarProjection::Create(week_state, &repository);
  EXPECT_EQ(week.title, "Aug 23 - Aug 29, 2026");
  EXPECT_EQ(week.event_groups.size(), static_cast<std::size_t>(7));
  EXPECT_EQ(testing::Ids(week.event_groups[1].events), "all-day,meeting");

  auto day_state = week_state.ChangeViewMode(state::CalendarViewMode::kDay);
  auto day = state::CalendarProjection::Create(day_state, &repository);
  EXPECT_EQ(day.title, "Aug 24, 2026");
  EXPECT_EQ(day.event_groups.size(), static_cast<std::size_t>(1));

  auto agenda_state =
      day_state.ChangeViewMode(state::CalendarViewMode::kAgenda);
  auto agenda = state::CalendarProjection::Create(agenda_state, &repository);
  EXPECT_EQ(agenda.title, "August 2026 agenda");
  EXPECT_EQ(agenda.event_groups.size(), static_cast<std::size_t>(2));
}

CALENDAR_TEST(projection_uses_stable_event_ids_for_focus_restoration) {
  domain::CalendarEventRepository repository;
  PopulateProjectionRepository(&repository);
  auto state_value = state::CalendarUiState::Create(base::Date(2026, 8, 24))
                         .ChangeViewMode(state::CalendarViewMode::kDay)
                         .FocusPeriodEvent("meeting");
  auto projection = state::CalendarProjection::Create(state_value, &repository);
  EXPECT_EQ(projection.focused_event_id.value_or(""), "meeting");
  repository.TryDelete("all-day");
  projection = state::CalendarProjection::Create(state_value, &repository);
  EXPECT_EQ(projection.focused_event_id.value_or(""), "meeting");
}

} // namespace
} // namespace calendar
