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

#ifndef CALENDAR_NATIVE_TESTS_HARNESS_HH_
#define CALENDAR_NATIVE_TESTS_HARNESS_HH_

#include <exception>
#include <functional>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

namespace calendar {
namespace testing {

// A single named check. Registered at static-initialisation time by the
// CALENDAR_TEST macro and run in registration order by RunAll().
struct TestCase {
  std::string name;
  std::function<void()> body;
};

// Thrown by the assertion helpers; carries the human-readable reason only.
class AssertionFailure : public std::exception {
 public:
  explicit AssertionFailure(std::string reason)
      : reason_(std::move(reason)) {}

  const char* what() const noexcept override { return reason_.c_str(); }

 private:
  std::string reason_;
};

std::vector<TestCase>& Registry();

// Runs every registered case, printing one line per case, and returns the
// process exit code: 0 when all cases pass.
int RunAll();

struct Registrar {
  Registrar(std::string name, std::function<void()> body) {
    Registry().push_back(TestCase{std::move(name), std::move(body)});
  }
};

template <typename T>
std::string Describe(const T& value) {
  std::ostringstream stream;
  stream << value;
  return stream.str();
}

inline std::string Describe(bool value) { return value ? "true" : "false"; }

inline std::string Describe(const std::string& value) {
  return "\"" + value + "\"";
}

void FailAt(const char* file, int line, const std::string& reason);

template <typename Actual, typename Expected>
void ExpectEqualAt(const char* file, int line, const char* expression,
                   const Actual& actual, const Expected& expected) {
  if (actual == expected) return;
  FailAt(file, line,
         std::string(expression) + " was " + Describe(actual) +
             ", expected " + Describe(expected));
}

inline void ExpectTrueAt(const char* file, int line, const char* expression,
                         bool actual) {
  if (actual) return;
  FailAt(file, line, std::string(expression) + " was false");
}

}  // namespace testing
}  // namespace calendar

#define CALENDAR_TEST(name)                                             \
  static void CalendarTestBody_##name();                                \
  static ::calendar::testing::Registrar CalendarTestRegistrar_##name(   \
      #name, CalendarTestBody_##name);                                  \
  static void CalendarTestBody_##name()

#define EXPECT_EQ(actual, expected)                                     \
  ::calendar::testing::ExpectEqualAt(__FILE__, __LINE__, #actual,       \
                                     (actual), (expected))

#define EXPECT_TRUE(actual)                                             \
  ::calendar::testing::ExpectTrueAt(__FILE__, __LINE__, #actual, (actual))

#define EXPECT_FALSE(actual)                                            \
  ::calendar::testing::ExpectTrueAt(__FILE__, __LINE__, "!" #actual,    \
                                    !(actual))

#define FAIL_TEST(reason)                                               \
  ::calendar::testing::FailAt(__FILE__, __LINE__, (reason))

#endif  // CALENDAR_NATIVE_TESTS_HARNESS_HH_
