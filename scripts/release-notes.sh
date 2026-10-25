#!/usr/bin/env bash
# release-notes.sh - Header for a fork release's GitHub release notes.
#
# Prints the upstream base and, when this release moves to a new upstream
# version (or there is no previous fork release), that version's section of
# upstream's CHANGELOG.md. The fork's own PRs are appended by
# `gh release create --generate-notes` in release.yml.
#
# Upstream PR references are pointed at confluentinc/confluent-kafka-dotnet and
# relative links at this repo's tree at the new tag, so neither resolves to an
# unrelated issue or path in the fork.
#
# Usage: scripts/release-notes.sh <upstream-prefix> <new-tag> [<previous-tag>]
# Requires: GITHUB_REPOSITORY (owner/name), set by GitHub Actions.

set -euo pipefail

prefix=$1
tag=$2
prev=${3:-}
repo=${GITHUB_REPOSITORY:?GITHUB_REPOSITORY must be set}

REPO_ROOT=$(git rev-parse --show-toplevel)

echo "Upstream base: confluent-kafka-dotnet ${prefix}."

# Same upstream version as the previous fork release: nothing new from upstream.
if [ -n "$prev" ] && [ "${prev%.*}" = "v${prefix}" ]; then
    exit 0
fi

# Headings are "# X.Y.Z" or "# X.Y.Z (unreleased)"; the section runs to the next "# ".
section=$(awk -v v="$prefix" '/^# /{ if (s) exit; s = ($2 == v); next } s' "$REPO_ROOT/CHANGELOG.md" \
    | sed -E \
        -e 's|\(#([0-9]+)\)|(confluentinc/confluent-kafka-dotnet#\1)|g' \
        -e "s|\]\(([^):/#][^):]*)\)|](https://github.com/${repo}/tree/${tag}/\1)|g")

if [ -z "${section//[[:space:]]/}" ]; then
    echo "No upstream CHANGELOG.md section found for ${prefix}." >&2
    exit 0
fi

echo
echo "## Upstream changes in ${prefix}"
# Demote upstream's "##" headings below ours.
printf '%s\n' "$section" | sed -E 's/^## /### /'
