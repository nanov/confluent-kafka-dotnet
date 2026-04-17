#!/usr/bin/env bash
# Checks whether the fork has published a release for upstream's latest stable tag.
# Fork releases use a 4-segment version convention <upstream-tag>.<N>, e.g. v2.14.0.1.
#
# Exits 0 if a matching fork release exists locally, 1 otherwise, 2 if upstream has none.
# Requires: gh CLI authenticated, and local tags up to date (run `git fetch origin`).

set -euo pipefail

UPSTREAM="${UPSTREAM_REPO:-confluentinc/confluent-kafka-dotnet}"

latest=$(gh release list -R "$UPSTREAM" --json tagName --jq '[.[] | select(.tagName | test("^v[0-9]+\\.[0-9]+\\.[0-9]+$"))] | .[0].tagName')

if [ -z "$latest" ] || [ "$latest" = "null" ]; then
    echo "No stable upstream release found (pattern: vX.Y.Z)." >&2
    exit 2
fi

echo "Upstream latest stable release: $latest"

escaped=${latest//./\\.}
matches=$(git tag -l "${latest}.*" | grep -E "^${escaped}\.[0-9]+$" || true)

if [ -n "$matches" ]; then
    echo "Fork has published for $latest:"
    echo "$matches" | sed 's/^/  /'
    exit 0
fi

echo "No fork release exists for $latest (pattern: ${latest}.N). Fork is behind upstream."
exit 1
