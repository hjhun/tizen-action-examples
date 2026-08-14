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

#include "provider/calendar_view_registry.hh"

#include <limits>
#include <string>
#include <vector>

#include "harness.hh"
#include "testing_factories.hh"

namespace calendar {
namespace {

provider::VisibleEventView Visible(const std::string &id, double x,
                                   double width, bool focused = false) {
  auto event =
      testing::MakeEvent(id, "Visible event", testing::At(2026, 8, 24, 14, 0),
                         testing::At(2026, 8, 24, 15, 0), "Note", "Studio");
  return {event,
          {x, 120.0, width, 72.0},
          {x, 120.0, width, 72.0},
          focused,
          "{\"TizenEntityCalendar\":{\"Id\":\"" + id +
              "\",\"Extra\":\"\",\"Title\":\"Visible event\","
              "\"StartDate\":\"2026-08-24T14:00:00+09:00\","
              "\"EndDate\":\"2026-08-24T15:00:00+09:00\","
              "\"Note\":\"Note\",\"Location\":\"Studio\"}}"};
}

CALENDAR_TEST(view_registry_publishes_only_unique_positive_measured_views) {
  provider::CalendarViewRegistry registry;
  registry.Publish(
      {Visible("event-1", 100.0, 500.0, true), Visible("event-1", 200.0, 500.0),
       Visible("event-2", 300.0, 0.0),
       Visible("event-3", std::numeric_limits<double>::infinity(), 500.0)});

  auto views = registry.GetAnnotatedViews();
  EXPECT_EQ(views.size(), static_cast<std::size_t>(1));
  EXPECT_EQ(views[0].id, "calendar:event:event-1");
  EXPECT_EQ(views[0].annotation.entity_type, "Tizen.Entity.Calendar");
  EXPECT_EQ(views[0].annotation.entity_id, "event-1");
  EXPECT_TRUE(views[0].is_focused);
  EXPECT_TRUE(registry.GetFocusedView().has_value());
  EXPECT_TRUE(registry.FindById("calendar:event:event-1").has_value());
}

CALENDAR_TEST(view_registry_clear_removes_stale_views) {
  provider::CalendarViewRegistry registry;
  registry.Publish({Visible("event-1", 100.0, 500.0, true)});
  registry.Clear();
  EXPECT_TRUE(registry.GetAnnotatedViews().empty());
  EXPECT_FALSE(registry.GetFocusedView().has_value());
}

CALENDAR_TEST(view_service_core_creates_legacy_a2ui_from_entity_json) {
  provider::CalendarViewRegistry registry;
  registry.Publish({Visible("event-1", 100.0, 500.0, true)});
  provider::CalendarViewServiceCore service(&registry);
  provider::PresentationData presentation;

  auto status =
      service.ToPresentation(*registry.GetFocusedView(), &presentation);
  EXPECT_TRUE(status.success);
  EXPECT_TRUE(presentation.template_value.find("surfaceUpdate") !=
              std::string::npos);
  EXPECT_TRUE(presentation.document.find("dataModelUpdate") !=
              std::string::npos);
  EXPECT_TRUE(presentation.document.find("Visible event") != std::string::npos);

  auto invalid = *registry.GetFocusedView();
  invalid.annotation.entity_type = "Tizen.Entity.Other";
  EXPECT_FALSE(service.ToPresentation(invalid, &presentation).success);
}

} // namespace
} // namespace calendar
