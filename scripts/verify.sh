#!/usr/bin/env bash
# Verifies the app: scripts/verify.sh <fast|smoke|full>. Exit code 0 = passed, 1 = failed, 2 = couldn't run.
# The logic lives in verify.cs (shared with verify.ps1); see the top of that file.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
exec dotnet run --file scripts/verify.cs -- "$@"
