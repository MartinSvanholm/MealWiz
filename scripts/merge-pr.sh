#!/usr/bin/env bash
# Merges a session's own green PR, and only the two kinds a session may merge itself:
#   - an issue PR into its project branch (project/<slug>), from /implement-issue in feature mode;
#   - a bug PR into dev, from /implement-issue --bug and /fix-bugs.
# It never merges into main, and never merges a project branch into dev: that is the ship PR, which the user reviews
# and merges. .claude/settings.json pre-approves this script and not `gh pr merge`, because a permission pattern
# can't see a PR's base branch, and a merge into main deploys to production.
#
# Usage: scripts/merge-pr.sh <pr-number>
# Exit codes: 0 merged, 1 refused (wrong base or head, not open, draft, checks not green), 2 bad usage or gh error.
set -euo pipefail

if [[ $# -ne 1 || ! "$1" =~ ^[0-9]+$ ]]; then
  echo "usage: scripts/merge-pr.sh <pr-number>" >&2
  exit 2
fi
pr="$1"

if ! info="$(gh pr view "$pr" --json baseRefName,state,isDraft,mergeable,headRefName \
    --jq '"\(.baseRefName)\t\(.state)\t\(.isDraft)\t\(.mergeable)\t\(.headRefName)"')"; then
  echo "merge-pr: could not read PR #$pr" >&2
  exit 2
fi
IFS=$'\t' read -r base state draft mergeable head <<< "$info"

refuse() { echo "merge-pr: refusing to merge PR #$pr ($head -> $base): $1" >&2; exit 1; }

[[ "$base" == "dev" || "$base" == project/* ]] \
  || refuse "its base is '$base', not 'dev' or a project branch. Only the user merges into other branches."
[[ "$head" != "dev" && "$head" != "main" ]] || refuse "its head is '$head'; release PRs are merged by the user."
[[ "$head" != project/* ]] || refuse "its head is a project branch; the ship PR is reviewed and merged by the user."
[[ "$state" == "OPEN" ]] || refuse "it is $state."
[[ "$draft" == "false" ]] || refuse "it is a draft."
[[ "$mergeable" == "MERGEABLE" ]] || refuse "GitHub reports it as $mergeable (conflicts or not yet computed)."

# Exits non-zero when any check failed or is still pending.
if ! gh pr checks "$pr" > /dev/null 2>&1; then
  gh pr checks "$pr" >&2 || true
  refuse "its checks are not all green."
fi

gh pr merge "$pr" --merge --delete-branch
echo "merge-pr: merged PR #$pr ($head -> $base)"
