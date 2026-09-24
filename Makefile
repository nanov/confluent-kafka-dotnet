# This is a maintainer-centric Makefile to help performing
# typical operations for all projects in this repo.
#
# Example:
#   make build

OS=$(shell uname -s)

EXAMPLE_DIRS=$(shell find ./examples -name '*.csproj' -exec dirname {} \;)
TEST_DIRS=$(shell find ./test -name '*.csproj' -exec dirname {} \;)
UNIT_TEST_DIRS=$(shell find . -type d -regex '.*UnitTests$$' -exec basename {} \;)

# We want to run tests by default with latest version of .NET
DEFAULT_TEST_FRAMEWORK?=net10.0

all:
	@echo "Usage:   make <dotnet-command>"
	@echo "Example: make build - runs 'dotnet build' for all projects"

.PHONY: test

build:
	for d in $(EXAMPLE_DIRS) ; do dotnet $@ $$d; done ; \
	for d in $(TEST_DIRS) ; do dotnet $@ $$d; done ;

test:
	@(for d in $(UNIT_TEST_DIRS) ; do \
		dotnet test --project test/$$d/$$d.csproj ; \
	done)

test-coverage:
	@(for d in $(UNIT_TEST_DIRS) ; do \
		$(DOTNET_COVERAGE_TOOL) collect "dotnet test --project test/$$d/$$d.csproj -f $(DEFAULT_TEST_FRAMEWORK)" \
			-f xml -o test/$$d/coverage.xml ; \
	done)

test-latest:
	@(for d in $(UNIT_TEST_DIRS) ; do \
		dotnet test --project test/$$d/$$d.csproj -f $(DEFAULT_TEST_FRAMEWORK) ; \
	done)

# Native AOT smoke test: publishes test/Confluent.Kafka.AotSmoke with
# PublishAot=true (trim/AOT analysis warnings are errors) and runs the
# resulting binary against a broker.
#   make aot-smoke RID=linux-x64 KAFKA_BOOTSTRAP_SERVERS=localhost:9092
AOT_SMOKE_DIR=test/Confluent.Kafka.AotSmoke
RID?=$(shell dotnet --info | awk '/RID:/ {print $$2}')
KAFKA_BOOTSTRAP_SERVERS?=localhost:9092

aot-smoke-publish:
	dotnet publish $(AOT_SMOKE_DIR) -c Release -r $(RID)

aot-smoke: aot-smoke-publish
	$(AOT_SMOKE_DIR)/bin/Release/net10.0/$(RID)/publish/Confluent.Kafka.AotSmoke $(KAFKA_BOOTSTRAP_SERVERS)
