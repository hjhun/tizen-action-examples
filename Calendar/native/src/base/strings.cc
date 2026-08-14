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

#include "base/strings.hh"

#include <algorithm>
#include <string>

namespace calendar {
namespace base {
namespace {

bool IsAsciiSpace(char character) {
  return character == ' ' || character == '\t' || character == '\n' ||
         character == '\v' || character == '\f' || character == '\r';
}

char LowerAscii(char character) {
  return character >= 'A' && character <= 'Z'
             ? static_cast<char>(character - 'A' + 'a')
             : character;
}

}  // namespace

std::string Trim(const std::string& value) {
  std::size_t begin = 0;
  std::size_t end = value.size();
  while (begin < end && IsAsciiSpace(value[begin])) ++begin;
  while (end > begin && IsAsciiSpace(value[end - 1])) --end;
  return value.substr(begin, end - begin);
}

bool IsBlank(const std::string& value) {
  for (const char character : value) {
    if (!IsAsciiSpace(character)) return false;
  }
  return true;
}

std::string ToLowerAscii(const std::string& value) {
  std::string lowered = value;
  std::transform(lowered.begin(), lowered.end(), lowered.begin(), LowerAscii);
  return lowered;
}

bool ContainsIgnoreCase(const std::string& haystack,
                        const std::string& needle) {
  if (needle.empty()) return true;
  if (needle.size() > haystack.size()) return false;
  const std::string lowered_haystack = ToLowerAscii(haystack);
  const std::string lowered_needle = ToLowerAscii(needle);
  return lowered_haystack.find(lowered_needle) != std::string::npos;
}

bool StartsWith(const std::string& value, const std::string& prefix) {
  return value.size() >= prefix.size() &&
         value.compare(0, prefix.size(), prefix) == 0;
}

std::string JoinInts(const std::vector<int>& values,
                     const std::string& separator) {
  std::string joined;
  for (std::size_t index = 0; index < values.size(); ++index) {
    if (index != 0) joined += separator;
    joined += std::to_string(values[index]);
  }
  return joined;
}

}  // namespace base
}  // namespace calendar
