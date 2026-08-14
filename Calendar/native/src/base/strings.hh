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

#ifndef CALENDAR_NATIVE_BASE_STRINGS_HH_
#define CALENDAR_NATIVE_BASE_STRINGS_HH_

#include <string>
#include <vector>

namespace calendar {
namespace base {

// Removes leading and trailing ASCII whitespace. The C# reference uses
// String.Trim(), which also strips non-ASCII Unicode whitespace; Calendar text
// arrives from Action wire input and on-screen entries where that difference
// has no observable effect, and keeping this ASCII-only avoids pulling in a
// Unicode table.
std::string Trim(const std::string& value);

// The analogue of string.IsNullOrWhiteSpace for a non-nullable std::string.
bool IsBlank(const std::string& value);

// Case-insensitive substring test over ASCII, matching the reference's
// StringComparison.OrdinalIgnoreCase keyword matching.
bool ContainsIgnoreCase(const std::string& haystack,
                        const std::string& needle);

// Lowercases ASCII letters only.
std::string ToLowerAscii(const std::string& value);

bool StartsWith(const std::string& value, const std::string& prefix);

// Joins integers with ", " for the reference's error-message formatting.
std::string JoinInts(const std::vector<int>& values,
                     const std::string& separator);

}  // namespace base
}  // namespace calendar

#endif  // CALENDAR_NATIVE_BASE_STRINGS_HH_
