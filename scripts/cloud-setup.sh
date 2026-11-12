#!/usr/bin/env bash
# Gets a Claude Code cloud container (Ubuntu 24.04, root) ready for `scripts/verify.sh local`: the .NET 10 SDK, the
# Supabase CLI and a running Docker daemon. It's the body of the cloud environment's setup script, so every new
# session starts ready instead of spending its first minutes on installs. Paste this into the environment's settings
# (Setup script):
#
#   bash scripts/cloud-setup.sh
#
# The setup script runs in the repository root. Safe to run again: each step skips what's already there. Set
# MEALWIZ_PULL_IMAGES=1 to also pull the Supabase images verify uses (about 1.5 GB), so the first verify doesn't.
set -euo pipefail

SUPABASE_CLI_VERSION="2.111.0"

log() { echo "[cloud-setup] $*"; }

# .NET 10 SDK. Ubuntu's own package; builds.dotnet.microsoft.com isn't reachable through the cloud proxy.
if ! command -v dotnet > /dev/null || ! dotnet --list-sdks | grep -q '^10\.'; then
  log "installing the .NET 10 SDK"
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0 > /dev/null
fi

# Supabase CLI, from the release .deb. It holds both `supabase` and `supabase-go`; `supabase db reset` needs the
# second one, so copying only `supabase` out of the tarball breaks verify's second run (MEA-72).
if ! command -v supabase-go > /dev/null || ! supabase --version 2>/dev/null | grep -q "$SUPABASE_CLI_VERSION"; then
  log "installing the Supabase CLI $SUPABASE_CLI_VERSION"
  deb="$(mktemp --suffix=.deb)"
  curl -fsSL -o "$deb" \
    "https://github.com/supabase/cli/releases/download/v${SUPABASE_CLI_VERSION}/supabase_${SUPABASE_CLI_VERSION}_linux_amd64.deb"
  dpkg -i "$deb" > /dev/null
  rm -f "$deb"
fi

# Docker. The container has the daemon but doesn't start it. verify.cs starts it again if it dies later.
if ! docker info > /dev/null 2>&1; then
  log "starting the Docker daemon"
  nohup dockerd > /var/log/dockerd.log 2>&1 &
  for _ in $(seq 1 30); do
    docker info > /dev/null 2>&1 && break
    sleep 1
  done
  docker info > /dev/null 2>&1 || { log "Docker didn't start; see /var/log/dockerd.log"; exit 1; }
fi

if [[ "${MEALWIZ_PULL_IMAGES:-}" == "1" ]]; then
  # Start and stop the same services verify starts, which pulls their images.
  log "pulling the Supabase images"
  supabase start -x studio,postgres-meta,imgproxy,edge-runtime,logflare,vector,supavisor > /dev/null
  supabase stop --no-backup > /dev/null
fi

log "ready: $(dotnet --version), supabase $(supabase --version), docker $(docker version --format '{{.Server.Version}}')"
