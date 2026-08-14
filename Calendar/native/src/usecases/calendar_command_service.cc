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

#include "usecases/calendar_command_service.hh"

#include <algorithm>

namespace calendar {
namespace usecases {

CalendarCommandService::CalendarCommandService(
    domain::CalendarEventRepository* events,
    domain::CalendarReminderRepository* reminders,
    persistence::CalendarJsonStore* persistence,
    ReminderAlarmScheduler* alarms)
    : events_(events),
      reminders_(reminders),
      persistence_(persistence),
      alarms_(alarms) {}

CalendarCommandResult CalendarCommandService::CreateEvent(
    const domain::CalendarEvent& calendar_event,
    const std::vector<int>& reminder_offsets) {
  std::lock_guard<std::mutex> lock(gate_);

  if (!events_->ResolveByIds({calendar_event.id()}).events.empty()) {
    return CalendarCommandResult::Failed("Event '" + calendar_event.id() +
                                         "' already exists.");
  }

  std::vector<int> offsets;
  std::string error;
  if (!NormalizeOffsets(reminder_offsets, &offsets, &error)) {
    return CalendarCommandResult::Failed(error);
  }

  auto event_snapshot = events_->Snapshot();
  auto reminder_snapshot = reminders_->Snapshot();
  std::vector<int> scheduled_alarm_ids;
  std::vector<domain::CalendarReminder> linked_reminders;

  for (int offset : offsets) {
    domain::CalendarReminder reminder;
    if (!domain::CalendarReminder::TryCreateForEvent(
            LinkedReminderId(calendar_event.id(), offset),
            calendar_event.title(), calendar_event.start(), calendar_event.id(),
            offset, calendar_event.note(), &reminder, &error)) {
      for (int id : scheduled_alarm_ids) TryCancel(id);
      return CalendarCommandResult::Failed(error);
    }
    std::optional<int> alarm_id = alarms_->Schedule(reminder);
    if (alarm_id.has_value()) {
      scheduled_alarm_ids.push_back(*alarm_id);
      reminder = reminder.WithAlarmId(alarm_id);
    }
    linked_reminders.push_back(reminder);
  }

  std::vector<domain::CalendarEvent> desired_events = event_snapshot;
  desired_events.push_back(calendar_event);

  std::vector<domain::CalendarReminder> desired_reminders = reminder_snapshot;
  desired_reminders.insert(desired_reminders.end(), linked_reminders.begin(),
                           linked_reminders.end());

  persistence::CalendarStoreDocument doc;
  doc.events = desired_events;
  doc.reminders = desired_reminders;
  if (!persistence_->TrySave(doc, &error)) {
    for (int id : scheduled_alarm_ids) TryCancel(id);
    return CalendarCommandResult::Failed(error);
  }

  events_->ReplaceAll(desired_events);
  reminders_->ReplaceAll(desired_reminders);
  return CalendarCommandResult::Succeeded();
}

CalendarCommandResult CalendarCommandService::Restore() {
  std::lock_guard<std::mutex> lock(gate_);
  std::vector<int> scheduled_alarm_ids;

  persistence::CalendarStoreDocument document = persistence_->Load();
  std::vector<domain::CalendarReminder> restored_reminders;
  restored_reminders.reserve(document.reminders.size());

  for (const auto& reminder : document.reminders) {
    if (reminder.alarm_id().has_value()) {
      TryCancel(*reminder.alarm_id());
    }

    auto restored = reminder.WithAlarmId(std::nullopt);
    if (!restored.is_completed()) {
      std::optional<int> alarm_id = alarms_->Schedule(restored);
      if (alarm_id.has_value()) {
        scheduled_alarm_ids.push_back(*alarm_id);
        restored = restored.WithAlarmId(alarm_id);
      }
    }
    restored_reminders.push_back(restored);
  }

  persistence::CalendarStoreDocument reconciled;
  reconciled.events = document.events;
  reconciled.reminders = restored_reminders;

  std::string error;
  if (!persistence_->TrySave(reconciled, &error)) {
    for (int id : scheduled_alarm_ids) TryCancel(id);
    return CalendarCommandResult::Failed(error);
  }

  events_->ReplaceAll(reconciled.events);
  reminders_->ReplaceAll(reconciled.reminders);
  return CalendarCommandResult::Succeeded();
}

CalendarCommandResult CalendarCommandService::UpdateEvent(
    const domain::CalendarEvent& calendar_event,
    const std::vector<int>& reminder_offsets) {
  std::lock_guard<std::mutex> lock(gate_);

  if (events_->ResolveByIds({calendar_event.id()}).events.empty()) {
    return CalendarCommandResult::Failed("Event '" + calendar_event.id() +
                                         "' was not found.");
  }

  std::vector<int> offsets;
  std::string error;
  if (!NormalizeOffsets(reminder_offsets, &offsets, &error)) {
    return CalendarCommandResult::Failed(error);
  }

  auto event_snapshot = events_->Snapshot();
  auto reminder_snapshot = reminders_->Snapshot();
  std::vector<domain::CalendarReminder> old_linked_reminders;
  for (const auto& r : reminder_snapshot) {
    if (r.calendar_event_id() == calendar_event.id()) {
      old_linked_reminders.push_back(r);
    }
  }

  std::vector<int> scheduled_alarm_ids;
  std::vector<domain::CalendarReminder> replacement_reminders;

  for (int offset : offsets) {
    domain::CalendarReminder reminder;
    if (!domain::CalendarReminder::TryCreateForEvent(
            LinkedReminderId(calendar_event.id(), offset),
            calendar_event.title(), calendar_event.start(), calendar_event.id(),
            offset, calendar_event.note(), &reminder, &error)) {
      for (int id : scheduled_alarm_ids) TryCancel(id);
      return CalendarCommandResult::Failed(error);
    }
    std::optional<int> alarm_id = alarms_->Schedule(reminder);
    if (alarm_id.has_value()) {
      scheduled_alarm_ids.push_back(*alarm_id);
      reminder = reminder.WithAlarmId(alarm_id);
    }
    replacement_reminders.push_back(reminder);
  }

  std::vector<domain::CalendarEvent> desired_events;
  for (const auto& e : event_snapshot) {
    desired_events.push_back(e.id() == calendar_event.id() ? calendar_event
                                                           : e);
  }

  std::vector<domain::CalendarReminder> desired_reminders;
  for (const auto& r : reminder_snapshot) {
    if (r.calendar_event_id() != calendar_event.id()) {
      desired_reminders.push_back(r);
    }
  }
  desired_reminders.insert(desired_reminders.end(),
                           replacement_reminders.begin(),
                           replacement_reminders.end());

  persistence::CalendarStoreDocument doc;
  doc.events = desired_events;
  doc.reminders = desired_reminders;
  if (!persistence_->TrySave(doc, &error)) {
    for (int id : scheduled_alarm_ids) TryCancel(id);
    return CalendarCommandResult::Failed(error);
  }

  for (const auto& old : old_linked_reminders) {
    if (old.alarm_id().has_value()) {
      TryCancel(*old.alarm_id());
    }
  }

  events_->ReplaceAll(desired_events);
  reminders_->ReplaceAll(desired_reminders);
  return CalendarCommandResult::Succeeded();
}

CalendarCommandResult CalendarCommandService::DeleteEvent(
    const std::string& event_id) {
  if (event_id.empty()) {
    return CalendarCommandResult::Failed("An event ID is required.");
  }

  std::lock_guard<std::mutex> lock(gate_);
  if (events_->ResolveByIds({event_id}).events.empty()) {
    return CalendarCommandResult::Failed("Event '" + event_id +
                                         "' was not found.");
  }

  auto event_snapshot = events_->Snapshot();
  auto reminder_snapshot = reminders_->Snapshot();

  std::vector<domain::CalendarReminder> linked_reminders;
  std::vector<domain::CalendarEvent> desired_events;
  std::vector<domain::CalendarReminder> desired_reminders;

  for (const auto& e : event_snapshot) {
    if (e.id() != event_id) desired_events.push_back(e);
  }
  for (const auto& r : reminder_snapshot) {
    if (r.calendar_event_id() == event_id) {
      linked_reminders.push_back(r);
    } else {
      desired_reminders.push_back(r);
    }
  }

  persistence::CalendarStoreDocument doc;
  doc.events = desired_events;
  doc.reminders = desired_reminders;
  std::string error;
  if (!persistence_->TrySave(doc, &error)) {
    return CalendarCommandResult::Failed(error);
  }

  for (const auto& r : linked_reminders) {
    if (r.alarm_id().has_value()) {
      TryCancel(*r.alarm_id());
    }
  }

  events_->ReplaceAll(desired_events);
  reminders_->ReplaceAll(desired_reminders);
  return CalendarCommandResult::Succeeded();
}

CalendarCommandResult CalendarCommandService::CreateReminder(
    const domain::CalendarReminder& reminder) {
  std::lock_guard<std::mutex> lock(gate_);

  if (reminder.calendar_event_id().has_value()) {
    return CalendarCommandResult::Failed(
        "Event-linked reminders must be managed through their calendar event.");
  }

  if (reminders_->Find(reminder.id()).has_value()) {
    return CalendarCommandResult::Failed("Reminder '" + reminder.id() +
                                         "' already exists.");
  }

  std::optional<int> alarm_id;
  if (!reminder.is_completed()) {
    alarm_id = alarms_->Schedule(reminder);
  }
  auto persisted = reminder.WithAlarmId(alarm_id);

  auto desired_reminders = reminders_->Snapshot();
  desired_reminders.push_back(persisted);

  persistence::CalendarStoreDocument doc;
  doc.events = events_->Snapshot();
  doc.reminders = desired_reminders;

  std::string error;
  if (!persistence_->TrySave(doc, &error)) {
    if (alarm_id.has_value()) TryCancel(*alarm_id);
    return CalendarCommandResult::Failed(error);
  }

  reminders_->ReplaceAll(desired_reminders);
  return CalendarCommandResult::Succeeded();
}

CalendarCommandResult CalendarCommandService::UpdateReminder(
    const domain::CalendarReminder& reminder) {
  std::lock_guard<std::mutex> lock(gate_);

  auto existing = reminders_->Find(reminder.id());
  if (!existing.has_value()) {
    return CalendarCommandResult::Failed("Reminder '" + reminder.id() +
                                         "' was not found.");
  }

  if (existing->calendar_event_id().has_value() ||
      reminder.calendar_event_id().has_value()) {
    return CalendarCommandResult::Failed(
        "Event-linked reminders must be managed through their calendar event.");
  }

  std::optional<int> new_alarm_id;
  if (!reminder.is_completed()) {
    new_alarm_id = alarms_->Schedule(reminder.WithAlarmId(std::nullopt));
  }
  auto persisted = reminder.WithAlarmId(new_alarm_id);

  auto desired_reminders = reminders_->Snapshot();
  for (auto& r : desired_reminders) {
    if (r.id() == reminder.id()) r = persisted;
  }

  persistence::CalendarStoreDocument doc;
  doc.events = events_->Snapshot();
  doc.reminders = desired_reminders;

  std::string error;
  if (!persistence_->TrySave(doc, &error)) {
    if (new_alarm_id.has_value()) TryCancel(*new_alarm_id);
    return CalendarCommandResult::Failed(error);
  }

  if (existing->alarm_id().has_value()) {
    TryCancel(*existing->alarm_id());
  }

  reminders_->ReplaceAll(desired_reminders);
  return CalendarCommandResult::Succeeded();
}

CalendarCommandResult CalendarCommandService::SetReminderCompleted(
    const std::string& reminder_id, bool is_completed) {
  if (reminder_id.empty()) {
    return CalendarCommandResult::Failed("A reminder ID is required.");
  }

  auto existing = reminders_->Find(reminder_id);
  if (!existing.has_value()) {
    return CalendarCommandResult::Failed("Reminder '" + reminder_id +
                                         "' was not found.");
  }

  return UpdateReminder(existing->WithCompleted(is_completed));
}

CalendarCommandResult CalendarCommandService::DeleteReminder(
    const std::string& reminder_id) {
  if (reminder_id.empty()) {
    return CalendarCommandResult::Failed("A reminder ID is required.");
  }

  std::lock_guard<std::mutex> lock(gate_);
  auto existing = reminders_->Find(reminder_id);
  if (!existing.has_value()) {
    return CalendarCommandResult::Failed("Reminder '" + reminder_id +
                                         "' was not found.");
  }

  if (existing->calendar_event_id().has_value()) {
    return CalendarCommandResult::Failed(
        "Event-linked reminders must be managed through their calendar event.");
  }

  std::vector<domain::CalendarReminder> desired_reminders;
  for (const auto& r : reminders_->Snapshot()) {
    if (r.id() != reminder_id) desired_reminders.push_back(r);
  }

  persistence::CalendarStoreDocument doc;
  doc.events = events_->Snapshot();
  doc.reminders = desired_reminders;

  std::string error;
  if (!persistence_->TrySave(doc, &error)) {
    return CalendarCommandResult::Failed(error);
  }

  if (existing->alarm_id().has_value()) {
    TryCancel(*existing->alarm_id());
  }

  reminders_->ReplaceAll(desired_reminders);
  return CalendarCommandResult::Succeeded();
}

bool CalendarCommandService::NormalizeOffsets(
    const std::vector<int>& reminder_offsets, std::vector<int>* offsets,
    std::string* error) {
  *offsets = reminder_offsets;
  std::sort(offsets->begin(), offsets->end());
  offsets->erase(std::unique(offsets->begin(), offsets->end()), offsets->end());

  for (int offset : *offsets) {
    if (!domain::CalendarReminder::IsAllowedOffset(offset)) {
      *error = "Reminder offsets must be 10, 30, 60, or 1440 minutes.";
      return false;
    }
  }
  return true;
}

std::string CalendarCommandService::LinkedReminderId(
    const std::string& event_id, int offset_minutes) {
  return "reminder:" + event_id + ":" + std::to_string(offset_minutes);
}

void CalendarCommandService::TryCancel(int alarm_id) {
  try {
    alarms_->Cancel(alarm_id);
  } catch (...) {
  }
}

}  // namespace usecases
}  // namespace calendar
