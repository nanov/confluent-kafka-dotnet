#!/usr/bin/env bash
# Merges upstream/master into the fork's master, preserving the 4 access-modifier
# changes this fork makes to src/Confluent.Kafka/Consumer.cs.
#
# Behavior:
#   - Exits 1 on merge conflict.
#   - If any of our 4 patches is missing post-merge, tries to re-apply it by
#     matching the original upstream pattern. If the original isn't found either,
#     upstream restructured that area and manual review is required.
#   - Never pushes. On success, prints the command to push.
#
# Run from a clean working tree on any branch. Requires: `upstream` remote set
# to confluentinc/confluent-kafka-dotnet.

set -euo pipefail

REPO_ROOT=$(git rev-parse --show-toplevel)
cd "$REPO_ROOT"

if ! git diff --quiet || ! git diff --cached --quiet; then
    echo "Working tree is not clean. Commit or stash changes first." >&2
    exit 1
fi

if ! git config --get remote.upstream.url >/dev/null; then
    echo "No 'upstream' remote configured." >&2
    exit 1
fi

echo "Fetching upstream..."
git fetch upstream

echo "Switching to master..."
git checkout master

echo "Merging upstream/master..."
if ! git merge --no-edit -m "[sync] Merged upstream/master" upstream/master; then
    echo "Merge conflict. Resolve manually, then commit and rerun verification." >&2
    exit 1
fi

CONSUMER=src/Confluent.Kafka/Consumer.cs

# Each entry: "<desired regex>|<original regex>|<replacement line>"
# Tab separator; tabs in patterns are escaped as \t if ever needed (none here).
PATCHES=(
    "^        internal SafeKafkaHandle kafkaHandle;$|^        private SafeKafkaHandle kafkaHandle;$|        internal SafeKafkaHandle kafkaHandle;"
    "^        internal Exception handlerException = null;$|^        private Exception handlerException = null;$|        internal Exception handlerException = null;"
    "^        internal int cancellationDelayMaxMs;$|^        private int cancellationDelayMaxMs;$|        internal int cancellationDelayMaxMs;"
    "^        protected virtual int StatisticsCallback\(|^        private int StatisticsCallback\(|        protected virtual int StatisticsCallback("
)

reapplied=0
failed=0

for entry in "${PATCHES[@]}"; do
    IFS='|' read -r want have replace <<<"$entry"

    if grep -qE "$want" "$CONSUMER"; then
        continue
    fi

    echo "Missing patch matching: $want"

    if ! grep -qE "$have" "$CONSUMER"; then
        echo "  Original upstream pattern not found either — upstream restructured this area." >&2
        echo "  Manual intervention required." >&2
        failed=$((failed + 1))
        continue
    fi

    echo "  Found original; re-applying."
    tmp=$(mktemp)
    sed -E "s|$have|$replace|" "$CONSUMER" >"$tmp" && mv "$tmp" "$CONSUMER"

    if ! grep -qE "$want" "$CONSUMER"; then
        echo "  Re-apply produced no match. Aborting." >&2
        exit 1
    fi

    reapplied=$((reapplied + 1))
done

if [ "$failed" -gt 0 ]; then
    echo "$failed patch(es) could not be re-applied. Review and fix manually." >&2
    exit 1
fi

if [ "$reapplied" -gt 0 ]; then
    echo "Re-applied $reapplied patch(es). Amending merge commit."
    git add "$CONSUMER"
    git commit --amend --no-edit
fi

echo "Sync complete. Push with: git push origin master"
