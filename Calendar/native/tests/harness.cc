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

#include "harness.hh"

namespace calendar {
namespace testing {

std::vector<TestCase>& Registry() {
  static std::vector<TestCase> registry;
  return registry;
}

void FailAt(const char* file, int line, const std::string& reason) {
  std::ostringstream stream;
  stream << file << ":" << line << ": " << reason;
  throw AssertionFailure(stream.str());
}

int RunAll() {
  int failed = 0;
  for (const TestCase& test_case : Registry()) {
    try {
      test_case.body();
      std::cout << "PASS  " << test_case.name << "\n";
    } catch (const AssertionFailure& failure) {
      ++failed;
      std::cout << "FAIL  " << test_case.name << "\n        "
                << failure.what() << "\n";
    } catch (const std::exception& error) {
      ++failed;
      std::cout << "FAIL  " << test_case.name
                << "\n        unexpected exception: " << error.what() << "\n";
    } catch (...) {
      ++failed;
      std::cout << "FAIL  " << test_case.name
                << "\n        unexpected non-standard exception\n";
    }
  }

  const std::size_t total = Registry().size();
  std::cout << "\n" << (total - static_cast<std::size_t>(failed)) << "/"
            << total << " cases passed\n";
  return failed == 0 ? 0 : 1;
}

}  // namespace testing
}  // namespace calendar

int main() { return ::calendar::testing::RunAll(); }
