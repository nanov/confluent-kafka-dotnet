#!/usr/bin/env bash
# rename-namespace.sh - Rewrite the Confluent.Kafka namespace in place, so a
# second package can live side by side with the official Confluent.Kafka.
#
# Rewrites every whole-word "Confluent.Kafka" in the project's C# sources:
# namespaces, usings, qualified names, crefs, InternalsVisibleTo and the
# assembly name the AWS OAuth auto-wire looks up. Run only on a throwaway
# checkout (release.yml does this after packing the regular package); the
# result is never committed, so upstream syncs are unaffected.
#
# Usage: scripts/rename-namespace.sh <new-namespace>

set -euo pipefail

new=${1:?usage: rename-namespace.sh <new-namespace>}

REPO_ROOT=$(git rev-parse --show-toplevel)

find "$REPO_ROOT/src/Confluent.Kafka" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 \
    | xargs -0 perl -pi -e "s/\\bConfluent\\.Kafka\\b/${new}/g"
