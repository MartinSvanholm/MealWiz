#!/usr/bin/env bash
# SessionStart hook: bring the local dev branch up to date with origin/dev, so a session never works on a stale
# checkout (a resumed or cached container can hold an old local dev). Fast-forward only; never fails the session.
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0
git fetch --quiet origin dev 2>/dev/null || { echo "sync-dev: could not fetch origin/dev; the checkout may be stale."; exit 0; }
git rev-parse --verify --quiet refs/heads/dev >/dev/null || exit 0
local_dev=$(git rev-parse dev) remote_dev=$(git rev-parse origin/dev)
[ "$local_dev" = "$remote_dev" ] && exit 0
if ! git merge-base --is-ancestor "$local_dev" "$remote_dev"; then
  echo "sync-dev: local dev has commits origin/dev doesn't; left as is."
  exit 0
fi
if [ "$(git symbolic-ref --quiet --short HEAD)" = "dev" ]; then
  if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
    echo "sync-dev: dev is behind origin/dev but has uncommitted changes; run git pull --ff-only yourself."
    exit 0
  fi
  git merge --quiet --ff-only origin/dev && echo "sync-dev: fast-forwarded dev to origin/dev ($(git rev-parse --short HEAD))."
else
  git update-ref refs/heads/dev "$remote_dev" "$local_dev" && echo "sync-dev: moved local dev to origin/dev ($(git rev-parse --short dev))."
fi
exit 0
