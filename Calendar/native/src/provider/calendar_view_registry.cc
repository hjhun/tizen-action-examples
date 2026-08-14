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

#include <cmath>
#include <set>

#include "base/json.hh"
#include "base/strings.hh"

namespace calendar {
namespace provider {
namespace {

bool IsMeasured(const ViewBounds &bounds) {
  return std::isfinite(bounds.x) && std::isfinite(bounds.y) &&
         std::isfinite(bounds.width) && std::isfinite(bounds.height) &&
         bounds.width > 0 && bounds.height > 0;
}

ServiceStatus Success() { return {true, ""}; }

ServiceStatus Failure(const std::string &reason) { return {false, reason}; }

base::JsonValue TextComponent(const std::string &id, const std::string &path) {
  base::JsonValue binding = base::JsonValue::Object();
  binding.Set("path", base::JsonValue::String(path));
  base::JsonValue text = base::JsonValue::Object();
  text.Set("text", std::move(binding));
  base::JsonValue component = base::JsonValue::Object();
  component.Set("Text", std::move(text));
  base::JsonValue item = base::JsonValue::Object();
  item.Set("id", base::JsonValue::String(id));
  item.Set("component", std::move(component));
  return item;
}

bool ParseGeneratedEvent(const std::string &json,
                         domain::CalendarEvent *event) {
  base::JsonValue root;
  if (!base::JsonValue::Parse(json, &root))
    return false;
  const base::JsonValue *entity = root.Find("TizenEntityCalendar");
  if (entity == nullptr || !entity->IsObject())
    return false;
  base::OffsetDateTime start;
  base::OffsetDateTime end;
  if (!base::OffsetDateTime::TryParseFlexible(entity->StringOr("StartDate", ""),
                                              &start) ||
      !base::OffsetDateTime::TryParseFlexible(entity->StringOr("EndDate", ""),
                                              &end)) {
    return false;
  }
  std::string error;
  return domain::CalendarEvent::TryCreate(
      entity->StringOr("Id", ""), entity->StringOr("Title", ""), start, end,
      entity->StringOr("Note", ""), entity->StringOr("Location", ""), event,
      &error);
}

PresentationData MakeLegacyPresentation(const domain::CalendarEvent &event) {
  base::JsonValue children = base::JsonValue::Array();
  for (const char *id : {"title", "time", "location", "note"}) {
    children.Append(base::JsonValue::String(id));
  }
  base::JsonValue explicit_list = base::JsonValue::Object();
  explicit_list.Set("explicitList", std::move(children));
  base::JsonValue column = base::JsonValue::Object();
  column.Set("children", std::move(explicit_list));
  base::JsonValue column_type = base::JsonValue::Object();
  column_type.Set("Column", std::move(column));
  base::JsonValue root_component = base::JsonValue::Object();
  root_component.Set("id", base::JsonValue::String("calendar-event-card"));
  root_component.Set("component", std::move(column_type));

  base::JsonValue components = base::JsonValue::Array();
  components.Append(std::move(root_component));
  components.Append(TextComponent("title", "/title"));
  components.Append(TextComponent("time", "/time"));
  components.Append(TextComponent("location", "/location"));
  components.Append(TextComponent("note", "/note"));
  base::JsonValue surface = base::JsonValue::Object();
  surface.Set("surfaceId", base::JsonValue::String("calendar-event-card"));
  surface.Set("components", std::move(components));
  base::JsonValue template_root = base::JsonValue::Object();
  template_root.Set("surfaceUpdate", std::move(surface));

  base::JsonValue value = base::JsonValue::Object();
  value.Set("id", base::JsonValue::String(event.id()));
  value.Set("title", base::JsonValue::String(event.title()));
  value.Set("time",
            base::JsonValue::String(event.start().ToRoundTripString() + " — " +
                                    event.end().ToRoundTripString()));
  value.Set("location", base::JsonValue::String(event.location()));
  value.Set("note", base::JsonValue::String(event.note()));
  base::JsonValue update = base::JsonValue::Object();
  update.Set("surfaceId", base::JsonValue::String("calendar-event-card"));
  update.Set("path", base::JsonValue::String("/"));
  update.Set("value", std::move(value));
  base::JsonValue document_root = base::JsonValue::Object();
  document_root.Set("dataModelUpdate", std::move(update));
  return {template_root.ToString(), document_root.ToString()};
}

} // namespace

void CalendarViewRegistry::Publish(
    const std::vector<VisibleEventView> &visible_views) {
  std::vector<CalendarAnnotatedView> published;
  std::set<std::string> seen;
  for (const auto &visible : visible_views) {
    if (!IsMeasured(visible.screen_bounds) ||
        !IsMeasured(visible.window_bounds) ||
        !seen.insert(visible.event.id()).second) {
      continue;
    }
    published.push_back({"calendar:event:" + visible.event.id(),
                         "Calendar.EventCard",
                         visible.event.title(),
                         visible.screen_bounds,
                         visible.window_bounds,
                         visible.is_focused,
                         true,
                         {visible.event.id(), "Tizen.Entity.Calendar",
                          visible.generated_entity_json}});
  }
  std::lock_guard<std::mutex> lock(gate_);
  views_ = std::move(published);
}

void CalendarViewRegistry::Clear() {
  std::lock_guard<std::mutex> lock(gate_);
  views_.clear();
}

std::vector<CalendarAnnotatedView>
CalendarViewRegistry::GetAnnotatedViews() const {
  std::lock_guard<std::mutex> lock(gate_);
  return views_;
}

std::optional<CalendarAnnotatedView>
CalendarViewRegistry::GetFocusedView() const {
  std::lock_guard<std::mutex> lock(gate_);
  for (const auto &view : views_) {
    if (view.is_focused)
      return view;
  }
  return std::nullopt;
}

std::optional<CalendarAnnotatedView>
CalendarViewRegistry::FindById(const std::string &id) const {
  std::lock_guard<std::mutex> lock(gate_);
  for (const auto &view : views_) {
    if (view.id == id)
      return view;
  }
  return std::nullopt;
}

ServiceStatus
CalendarViewServiceCore::FindById(const std::string &id,
                                  CalendarAnnotatedView *view) const {
  if (base::IsBlank(id) || id.size() > 256) {
    return Failure("A view ID is required.");
  }
  auto found = registry_->FindById(id);
  if (!found.has_value()) {
    return Failure("The requested view is not currently visible.");
  }
  *view = *found;
  return Success();
}

ServiceStatus
CalendarViewServiceCore::GetFocusedView(CalendarAnnotatedView *view) const {
  auto focused = registry_->GetFocusedView();
  if (!focused.has_value()) {
    return Failure("No annotated calendar view is currently focused.");
  }
  *view = *focused;
  return Success();
}

std::vector<CalendarAnnotatedView>
CalendarViewServiceCore::GetAnnotatedViews() const {
  return registry_->GetAnnotatedViews();
}

ServiceStatus
CalendarViewServiceCore::ToPresentation(const CalendarAnnotatedView &view,
                                        PresentationData *presentation) const {
  if (view.annotation.entity_type != "Tizen.Entity.Calendar") {
    return Failure("A valid Calendar ViewAnnotation with generated EntityInfo "
                   "is required.");
  }
  domain::CalendarEvent event;
  if (!ParseGeneratedEvent(view.annotation.entity_info, &event) ||
      event.id() != view.annotation.entity_id) {
    return Failure("A valid Calendar ViewAnnotation with generated EntityInfo "
                   "is required.");
  }
  *presentation = MakeLegacyPresentation(event);
  return Success();
}

} // namespace provider
} // namespace calendar
