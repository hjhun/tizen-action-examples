# Copyright (c) 2026 Samsung Electronics Co., Ltd. All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# Host build for the Tizen-free Calendar layers and their tests. The target
# build is driven by the Tizen CLI through project_def.prop instead.

CXX ?= g++
CXXFLAGS := -std=c++17 -Wall -Werror -Werror=shadow -g -O0 -Isrc
OUT := build

HOST_SRCS := \
	$(wildcard src/base/*.cc) \
	$(wildcard src/domain/*.cc) \
	$(wildcard src/persistence/*.cc) \
	src/provider/calendar_service_core.cc \
	src/provider/calendar_view_registry.cc \
	$(wildcard src/usecases/*.cc) \
	$(wildcard src/state/*.cc)

TEST_SRCS := $(wildcard tests/*.cc)

HOST_OBJS := $(patsubst %.cc,$(OUT)/%.o,$(HOST_SRCS))
TEST_OBJS := $(patsubst %.cc,$(OUT)/%.o,$(TEST_SRCS))

TEST_BIN := $(OUT)/calendar-host-tests

.PHONY: test build clean

test: $(TEST_BIN)
	$(TEST_BIN)

build: $(TEST_BIN)

$(TEST_BIN): $(HOST_OBJS) $(TEST_OBJS)
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -o $@ $^ -lpthread

$(OUT)/%.o: %.cc
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -MMD -MP -c $< -o $@

clean:
	rm -rf $(OUT)

-include $(HOST_OBJS:.o=.d) $(TEST_OBJS:.o=.d)
