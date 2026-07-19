#!/usr/bin/env bash
# acp-smoke.sh — Docker-based end-to-end smoke test for the OpenMono ACP agent.
#
# Builds (or assumes) the openmono/agent:latest image, launches a container with the
# current working tree mounted at /workspace, then asserts:
#   - GET /api/v1/discovery returns 200 and the host_workspace matches $PWD
#   - <workspace>/.openmono/agent.lock appears with the right port
#   - POST /api/v1/sessions returns a session_id
#   - GET /api/v1/sessions/{id} returns 200
#   - POST /api/v1/sessions/{id}/turn streams at least a `done` SSE event
#
# Usage:
#   public/scripts/acp-smoke.sh
#
# Env:
#   OPENMONO_IMAGE   image tag to use (default: openmono/agent:latest)
#   ACP_PORT         override the picked host port (default: ephemeral)

set -euo pipefail

WORKSPACE="${PWD}"
IMAGE="${OPENMONO_IMAGE:-openmono/agent:latest}"

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is not on PATH" >&2
  exit 2
fi
if ! docker info >/dev/null 2>&1; then
  echo "docker daemon is not reachable" >&2
  exit 2
fi
if [[ -z "${OPENMONO_SKIP_BUILD:-}" ]]; then
  echo ">> docker build -f public/docker/Dockerfile.agent -t $IMAGE public/"
  docker build -f public/docker/Dockerfile.agent -t "$IMAGE" public/
fi

PORT="${ACP_PORT:-$(python3 -c 'import socket; s=socket.socket(); s.bind(("",0)); print(s.getsockname()[1]); s.close()')}"
NAME="openmono_smoke_$$"
VOLUME_NAME="openmono-sessions-smoke-$$"

cleanup() {
  docker stop "$NAME" >/dev/null 2>&1 || true
  docker volume rm "$VOLUME_NAME" >/dev/null 2>&1 || true
  rm -f "$WORKSPACE/.openmono/agent.lock"
  rmdir "$WORKSPACE/.openmono" 2>/dev/null || true
}
trap cleanup EXIT

echo ">> docker run on port $PORT"
docker run -d --rm --name "$NAME" \
  -v "$WORKSPACE:/workspace" \
  -v "$VOLUME_NAME:/data" \
  -p "127.0.0.1:$PORT:7475" \
  -e HOST_ACP_PORT="$PORT" \
  -e HOST_WORKSPACE_PATH="$WORKSPACE" \
  "$IMAGE" >/dev/null

echo ">> waiting for /api/v1/discovery"
for _ in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:$PORT/api/v1/discovery" >/dev/null 2>&1; then break; fi
  sleep 0.5
done

echo ">> discovery"
DISC=$(curl -fsS "http://127.0.0.1:$PORT/api/v1/discovery")
echo "$DISC"
echo "$DISC" | jq -e ".host_workspace == \"$WORKSPACE\"" >/dev/null

echo ">> lock file"
test -f "$WORKSPACE/.openmono/agent.lock"
jq -e ".port == $PORT" "$WORKSPACE/.openmono/agent.lock" >/dev/null
jq -e ".host_workspace == \"$WORKSPACE\"" "$WORKSPACE/.openmono/agent.lock" >/dev/null

echo ">> POST /sessions"
SID=$(curl -fsS -X POST "http://127.0.0.1:$PORT/api/v1/sessions" \
  -H 'Content-Type: application/json' -d '{}' | jq -r .session_id)
echo "session_id=$SID"
test -n "$SID"

echo ">> GET /sessions/$SID"
curl -fsS "http://127.0.0.1:$PORT/api/v1/sessions/$SID" >/dev/null

echo ">> POST /sessions/$SID/turn (looking for `event: done`)"
STREAM=$(curl -N -fsS -X POST "http://127.0.0.1:$PORT/api/v1/sessions/$SID/turn" \
  -H 'Content-Type: application/json' \
  -H 'Accept: text/event-stream' \
  --max-time 60 \
  -d '{"message":"Reply with exactly: pong"}' | head -n 200)
echo "$STREAM" | grep -q "^event: done" || {
  echo "no `event: done` in stream:" >&2
  echo "$STREAM" >&2
  exit 1
}

echo "OK"
