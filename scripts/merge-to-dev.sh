#!/usr/bin/env bash
# Merges a PR into dev, and only into dev. Bug-mode sessions (/implement-issue --bug, /fix-bugs) merge their own PR
# through this script instead of calling `gh pr merge` directly. .claude/settings.json pre-approves this script and
# not `gh pr merge`, because a permission pattern can't see a PR's base branch, and a merge into main deploys to
# production.
#
# Usage: scripts/merge-to-dev.sh <pr-number>
# Exit codes: 0 merged, 1 refused (wrong base, not open, draft, checks not green), 2 bad usage or gh error.
set -euo pipefail

if [[ $# -ne 1 || ! "$1" =~ ^[0-9]+$ ]]; then
  echo "usage: scripts/merge-to-dev.sh <pr-number>" >&2
  exit 2
fi
pr="$1"

if ! info="$(gh pr view "$pr" --json baseRefName,state,isDraft,mergeable,headRefName \
    --jq '"\(.baseRefName)\t\(.state)\t\(.isDraft)\t\(.mergeable)\t\(.headRefName)"')"; then
  echo "merge-to-dev: could not read PR #$pr" >&2
  exit 2
fi
IFS=$'\t' read -r base state draft mergeable head <<< "$info"

refuse() { echo "merge-to-dev: refusing to merge PR #$pr ($head -> $base): $1" >&2; exit 1; }

[[ "$base" == "dev" ]] || refuse "its base is '$base', not 'dev'. Only the user merges into other branches."
[[ "$head" != "dev" && "$head" != "main" ]] || refuse "its head is '$head'; release PRs are merged by the user."
[[ "$state" == "OPEN" ]] || refuse "it is $state."
[[ "$draft" == "false" ]] || refuse "it is a draft."
[[ "$mergeable" == "MERGEABLE" ]] || refuse "GitHub reports it as $mergeable (conflicts or not yet computed)."

# Exits non-zero when any check failed or is still pending.
if ! gh pr checks "$pr" > /dev/null 2>&1; then
  gh pr checks "$pr" >&2 || true
  refuse "its checks are not all green."
fi

gh pr merge "$pr" --merge --delete-branch
echo "merge-to-dev: merged PR #$pr ($head -> dev)"
