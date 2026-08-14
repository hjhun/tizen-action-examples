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

#ifndef CALENDAR_NATIVE_PROVIDER_CALENDAR_VIEW_REGISTRY_HH_
#define CALENDAR_NATIVE_PROVIDER_CALENDAR_VIEW_REGISTRY_HH_

#include <mutex>
#include <optional>
#include <string>
#include <vector>

#include "domain/calendar_event.hh"
#include "provider/calendar_service_core.hh"

namespace calendar {
namespace provider {

struct ViewBounds {
  double x = 0;
  double y = 0;
  double width = 0;
  double height = 0;
};

struct VisibleEventView {
  domain::CalendarEvent event;
  ViewBounds screen_bounds;
  ViewBounds window_bounds;
  bool is_focused = false;
  std::string generated_entity_json;
};

struct CalendarAnnotation {
  std::string entity_id;
  std::string entity_type;
  std::string entity_info;
};

struct CalendarAnnotatedView {
  std::string id;
  std::string type;
  std::string description;
  ViewBounds screen_bounds;
  ViewBounds window_bounds;
  bool is_focused = false;
  bool is_enabled = true;
  CalendarAnnotation annotation;
};

class CalendarViewRegistry {
public:
  void Publish(const std::vector<VisibleEventView> &visible_views);
  void Clear();
  std::vector<CalendarAnnotatedView> GetAnnotatedViews() const;
  std::optional<CalendarAnnotatedView> GetFocusedView() const;
  std::optional<CalendarAnnotatedView> FindById(const std::string &id) const;

private:
  mutable std::mutex gate_;
  std::vector<CalendarAnnotatedView> views_;
};

class CalendarViewServiceCore {
public:
  explicit CalendarViewServiceCore(CalendarViewRegistry *registry)
      : registry_(registry) {}

  ServiceStatus FindById(const std::string &id,
                         CalendarAnnotatedView *view) const;
  ServiceStatus GetFocusedView(CalendarAnnotatedView *view) const;
  std::vector<CalendarAnnotatedView> GetAnnotatedViews() const;
  ServiceStatus ToPresentation(const CalendarAnnotatedView &view,
                               PresentationData *presentation) const;

private:
  CalendarViewRegistry *registry_;
};

} // namespace provider
} // namespace calendar

#endif // CALENDAR_NATIVE_PROVIDER_CALENDAR_VIEW_REGISTRY_HH_
