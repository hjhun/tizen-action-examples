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

#include "ui/calendar_app.hh"

int main(int argc, char *argv[]) {
  calendar::ui::CalendarApp app;

  ui_app_lifecycle_callback_s event_callback = {};
  event_callback.create = calendar::ui::CalendarApp::AppCreateCb;
  event_callback.terminate = calendar::ui::CalendarApp::AppTerminateCb;
  event_callback.pause = calendar::ui::CalendarApp::AppPauseCb;
  event_callback.resume = calendar::ui::CalendarApp::AppResumeCb;
  event_callback.app_control = calendar::ui::CalendarApp::AppControlCb;

  return ui_app_main(argc, argv, &event_callback, &app);
}
