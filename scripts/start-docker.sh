#!/usr/bin/env bash
# Starts the Docker daemon in a Claude Code cloud container (Linux, root), where nothing else starts it or restarts
# it after the session's worker restarts. Used by scripts/cloud-setup.sh and by verify.cs when Docker isn't running.
# Exit code 0 = Docker answers. The daemon's log is /var/log/dockerd.log.
set -uo pipefail

docker info > /dev/null 2>&1 && exit 0

# A daemon that was killed leaves its pid files behind. dockerd then waits for a containerd that isn't there and
# gives up ("timeout waiting for containerd to start", MEA-119). A pid file is stale when no process with that id
# runs the program it names (the id may have been reused by something else).
stale() { [[ -f "$1" ]] && [[ "$(cat "/proc/$(cat "$1")/comm" 2> /dev/null)" != "$2" ]]; }
if stale /var/run/docker/containerd/containerd.pid containerd; then
  rm -f /var/run/docker/containerd/containerd.pid /var/run/docker/containerd/containerd*.sock*
fi
if stale /var/run/docker.pid dockerd; then
  rm -f /var/run/docker.pid
fi

nohup dockerd > /var/log/dockerd.log 2>&1 &
for _ in $(seq 1 30); do
  docker info > /dev/null 2>&1 && exit 0
  sleep 1
done
echo "Docker didn't start; see /var/log/dockerd.log" >&2
exit 1
