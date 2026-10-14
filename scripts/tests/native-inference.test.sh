#!/usr/bin/env bash
# Checks for the macOS Metal inference helpers. No llama-server required.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../llama-native.sh
source "$ROOT/scripts/llama-native.sh"

fail() { printf 'FAIL: %s\n' "$1" >&2; exit 1; }

unset REPO_DIR
got="$(_native_repo_root)"
[[ "$got" == "$ROOT" ]] || fail "repo root: expected $ROOT, got $got"

REPO_DIR="/tmp/openmono-custom"
[[ "$(_native_repo_root)" == "/tmp/openmono-custom" ]] || fail "REPO_DIR override"
[[ "$(_native_docker_env)" == "/tmp/openmono-custom/docker/.env" ]] || fail "docker env path"
unset REPO_DIR

unset LLAMA_HOST
[[ "$(_native_health_url 7474)" == "http://127.0.0.1:7474/health" ]] || fail "default health url"
LLAMA_HOST="0.0.0.0"
[[ "$(_native_health_url 7474)" == "http://127.0.0.1:7474/health" ]] || fail "wildcard health url"
LLAMA_HOST="100.1.2.3"
[[ "$(_native_health_url 7474)" == "http://100.1.2.3:7474/health" ]] || fail "tailscale health url"
unset LLAMA_HOST

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
env_file="$work/docker.env"
printf 'FOO=1\nLLAMA_API_KEY=old\nBAR=2\n' > "$env_file"
upsert_env_key "$env_file" LLAMA_API_KEY "new-key"
grep -qx 'LLAMA_API_KEY=new-key' "$env_file" || fail "key not rotated"
grep -qx 'FOO=1' "$env_file" || fail "dropped unrelated key"
grep -qx 'BAR=2' "$env_file" || fail "dropped trailing key"
[[ ! -e /tmp/env.tmp ]] || fail "wrote shared /tmp/env.tmp"
mode="$(stat -f '%OLp' "$env_file" 2>/dev/null || stat -c '%a' "$env_file")"
[[ "$mode" == "600" ]] || fail "env mode $mode"

drop_env_keys "$env_file" '^(MODEL_MMPROJ|OPENMONO_VISION_ENABLED)='
printf 'MODEL_MMPROJ=%s\n' "mmproj.gguf" >> "$env_file"
grep -qx 'LLAMA_API_KEY=new-key' "$env_file" || fail "drop removed api key"
drop_env_keys "$env_file" '^(MODEL_MMPROJ|OPENMONO_VISION_ENABLED)='
if grep -q '^MODEL_MMPROJ=' "$env_file"; then
    fail "mmproj key survived drop"
fi

# Port resolution must not require a real settings file or a running server.
empty_home="$work/empty-home"
mkdir -p "$empty_home"
saved_llama_port="${LLAMA_PORT-}"
saved_native_port="${LLAMA_NATIVE_PORT-}"
unset LLAMA_PORT LLAMA_NATIVE_PORT
got="$(HOME="$empty_home" OPENMONO_SETTINGS="$empty_home/missing.json" _native_port)"
[[ "$got" == "7474" ]] || fail "default port: $got"
printf '%s\n' '{"inference":{"port":7475}}' > "$work/settings.json"
got="$(OPENMONO_SETTINGS="$work/settings.json" _native_port)"
[[ "$got" == "7475" ]] || fail "settings port: $got"
LLAMA_PORT=8088
got="$(OPENMONO_SETTINGS="$work/settings.json" _native_port)"
[[ "$got" == "8088" ]] || fail "LLAMA_PORT override: $got"
unset LLAMA_PORT
if [[ -n "$saved_llama_port" ]]; then LLAMA_PORT="$saved_llama_port"; fi
if [[ -n "$saved_native_port" ]]; then LLAMA_NATIVE_PORT="$saved_native_port"; fi

printf 'ok\n'
