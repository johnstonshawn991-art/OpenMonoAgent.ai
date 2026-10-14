#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# OpenMono.ai Native Inference Commands (macOS Apple Silicon)
#
# Provides implementations of start/stop/restart/logs/status/agent/tunnel
# for native llama-server running via Homebrew with Metal GPU acceleration.
#
# Sources: openmono CLI passes $REPO_DIR and other vars. This script
# should be sourced by openmono and its functions called via native_cmd_*()
# ──────────────────────────────────────────────────────────────────────────────

# shellcheck source=lib/env-file.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib/env-file.sh"

# Repo root is the parent of scripts/, unless the caller already set REPO_DIR.
_native_repo_root() {
    if [[ -n "${REPO_DIR:-}" ]]; then
        printf '%s\n' "$REPO_DIR"
        return 0
    fi
    (cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
}

_native_docker_env() {
    printf '%s\n' "$(_native_repo_root)/docker/.env"
}

# Port from the environment, then settings, then the OpenMono default.
# Stop uses this so a missing settings file cannot leave llama-server running.
_native_port() {
    if [[ -n "${LLAMA_NATIVE_PORT:-}" ]]; then
        printf '%s\n' "$LLAMA_NATIVE_PORT"
        return 0
    fi
    if [[ -n "${LLAMA_PORT:-}" ]]; then
        printf '%s\n' "$LLAMA_PORT"
        return 0
    fi
    local settings="${OPENMONO_SETTINGS:-$HOME/.openmono/settings.json}"
    local port=""
    if [[ -f "$settings" ]] && command -v jq >/dev/null 2>&1; then
        port="$(jq -r '.inference.port // empty' "$settings" 2>/dev/null || true)"
    fi
    if [[ -n "$port" && "$port" != "null" ]]; then
        printf '%s\n' "$port"
        return 0
    fi
    printf '%s\n' 7474
}

# Load settings.json and set native-specific variables
_native_load_config() {
    local settings="$HOME/.openmono/settings.json"
    if ! command -v jq &>/dev/null; then
        err "jq is required. Install: brew install jq"; return 1
    fi
    if [[ ! -f "$settings" ]]; then
        err "Settings not found: $settings"; err "Re-run: openmono setup"; return 1
    fi
    MODEL_NAME=$(jq -r '.inference.model_name // empty' "$settings" 2>/dev/null)
    MODEL_ALIAS=$(jq -r '.inference.model_alias // empty' "$settings" 2>/dev/null)
    LLAMA_CTX_SIZE=$(jq -r '.inference.ctx_size // 65536' "$settings" 2>/dev/null)
    LLAMA_NATIVE_PORT=$(jq -r '.inference.port // "7474"' "$settings" 2>/dev/null)
    LLAMA_INSTALL_DIR=$(jq -r '.inference.install_dir // empty' "$settings" 2>/dev/null)
    LLAMA_API_KEY=$(jq -r '.inference.api_key // empty' "$settings" 2>/dev/null)
    LLAMA_MMPROJ_PATH=$(jq -r '.inference.mmproj_path // empty' "$settings" 2>/dev/null)
    LLAMA_INSTALL_DIR="${LLAMA_INSTALL_DIR:-$HOME/openmono.ai}"
    if [[ -z "$MODEL_NAME" ]]; then
        err "inference config not found in $settings"; err "Re-run: openmono setup"; return 1
    fi
}

# Start native llama-server in the background with Metal GPU.
# Requires _native_load_config to have been called first.
_native_start_llama() {
    if ! command -v llama-server &>/dev/null; then
        err "llama-server not found. Install: brew install llama.cpp"; return 1
    fi
    local model_file="$LLAMA_INSTALL_DIR/models/$MODEL_NAME"
    if [[ ! -f "$model_file" ]]; then
        err "Model not found: $model_file"; return 1
    fi
    local log_file="$HOME/.openmono/logs/llama-server.log"
    mkdir -p "$(dirname "$log_file")"

    local cpu_threads
    cpu_threads=$(sysctl -n hw.physicalcpu 2>/dev/null || echo 4)

    # Read API key from docker/.env (single source of truth), then settings.json
    local api_key=""
    local docker_env
    docker_env="$(_native_docker_env)"
    if [[ -f "$docker_env" ]]; then
        api_key=$(grep '^LLAMA_API_KEY=' "$docker_env" 2>/dev/null | cut -d= -f2- | tr -d '[:space:]' || true)
    fi
    if [[ -z "$api_key" && -f "$HOME/.openmono/settings.json" ]] && command -v jq &>/dev/null; then
        api_key=$(jq -r '.inference.api_key // .llm.api_key // empty' "$HOME/.openmono/settings.json" 2>/dev/null || true)
    fi

    # Dual-box / Tailscale: never start without an API key (docs/SECURITY.md)
    local require_key="${LLAMA_REQUIRE_API_KEY:-}"
    if [[ -z "$require_key" && -f "$HOME/.openmono/tailscale-tunnel.json" ]]; then
        require_key=1
    fi
    if [[ "$require_key" == "1" && -z "$api_key" ]]; then
        err "LLAMA_API_KEY required for dual-box/Tailscale (refusing insecure start)."
        err "Run: openmono tunnel setup --tailscale   # generates and stores a key"
        err "Or set LLAMA_REQUIRE_API_KEY=0 to override (not recommended)."
        return 1
    fi

    # Bind host: LLAMA_HOST, else Tailscale IPv4 when Running, else 0.0.0.0
    local bind_host="${LLAMA_HOST:-}"
    if [[ -z "$bind_host" ]]; then
        # shellcheck source=lib/tailscale-cli.sh
        if [[ -f "$(dirname "${BASH_SOURCE[0]}")/lib/tailscale-cli.sh" ]]; then
            # shellcheck disable=SC1091
            source "$(dirname "${BASH_SOURCE[0]}")/lib/tailscale-cli.sh"
            if resolve_tailscale_cli && tailscale_is_running; then
                bind_host="$(tailscale_cmd ip -4 2>/dev/null | head -1 || true)"
            fi
        fi
    fi
    bind_host="${bind_host:-0.0.0.0}"
    if [[ "$bind_host" != "0.0.0.0" && "$bind_host" != "127.0.0.1" ]]; then
        info "Binding llama-server to $bind_host (not all interfaces — see docs/SECURITY.md)"
    fi

    info "Starting llama-server (Metal) — model: $MODEL_NAME"
    # One argv array so bash 3.2 + set -u does not expand an empty array.
    local -a server_args=(
        --model "$model_file" --alias "${MODEL_ALIAS:-${MODEL_NAME%.gguf}}"
        --host "$bind_host" --port "${LLAMA_NATIVE_PORT:-7474}"
        --n-gpu-layers 99
        --ctx-size "$LLAMA_CTX_SIZE"
        --threads "$cpu_threads" --batch-size 2048 --ubatch-size 512
        --flash-attn on
        --cache-type-k q8_0 --cache-type-v q8_0
        --parallel 1 --jinja --reasoning off --metrics
    )
    if [[ -n "$api_key" ]]; then
        local key_file="$HOME/.openmono/llama-api-key"
        local old_umask
        old_umask="$(umask)"
        umask 077
        printf '%s\n' "$api_key" > "$key_file"
        umask "$old_umask"
        chmod 0600 "$key_file"
        if llama-server --help 2>&1 | grep -q -- '--api-key-file'; then
            server_args+=(--api-key-file "$key_file")
        else
            server_args+=(--api-key "$api_key")
        fi
    fi
    if [[ -n "${LLAMA_MMPROJ_PATH:-}" ]]; then
        server_args+=(--mmproj "$LLAMA_MMPROJ_PATH" --image-min-tokens 1024 --image-max-tokens 1280)
    fi
    nohup llama-server "${server_args[@]}" > "$log_file" 2>&1 &
    local pid=$!
    disown "$pid" 2>/dev/null || true

    # Sanity check: ensure process didn't die immediately
    sleep 0.5
    if ! kill -0 "$pid" 2>/dev/null; then
        err "llama-server failed to start (process exited immediately)"
        err "Check the log for details:"
        err "  tail -20 $log_file"
        return 1
    fi
}

# Resolve a health URL that works for localhost or Tailscale-bound hosts.
_native_health_url() {
    local port="${1:-${LLAMA_NATIVE_PORT:-7474}}"
    local host="${LLAMA_HOST:-}"
    if [[ -z "$host" || "$host" == "0.0.0.0" ]]; then
        printf 'http://127.0.0.1:%s/health' "$port"
    else
        printf 'http://%s:%s/health' "$host" "$port"
    fi
}

# PID of the llama-server bound to this port, including one still loading the model.
_native_llama_pid() {
    local port="$1" pid comm
    pid="$(pgrep -f "llama-server.*--port[= ]${port}([^0-9]|$)" 2>/dev/null | head -n 1 || true)"
    if [[ -n "$pid" ]]; then
        printf '%s\n' "$pid"
        return 0
    fi
    pid="$(lsof -ti ":${port}" 2>/dev/null | head -n 1 || true)"
    if [[ -n "$pid" ]]; then
        comm="$(ps -p "$pid" -o comm= 2>/dev/null || true)"
        if [[ "$comm" == *llama-server* ]]; then
            printf '%s\n' "$pid"
            return 0
        fi
    fi
    return 1
}

# Stop native llama-server by port. Health is not required: a model can take
# minutes to load, and stop must still reach that process.
# Args: $1 = port (optional, defaults to LLAMA_NATIVE_PORT)
_native_stop_llama() {
    local port="${1:-${LLAMA_NATIVE_PORT:-7474}}"
    local pid
    pid="$(_native_llama_pid "$port" || true)"

    if [[ -z "$pid" ]]; then
        warn "llama-server is not running on port ${port}"
        return 1
    fi

    info "Stopping llama-server (PID $pid) on port ${port}..."
    kill "$pid" 2>/dev/null || true
    local i
    for i in 1 2 3 4 5 6 7 8 9 10; do
        kill -0 "$pid" 2>/dev/null || return 0
        sleep 0.3
    done
    warn "llama-server did not exit after SIGTERM; sending SIGKILL"
    kill -9 "$pid" 2>/dev/null || true
    sleep 0.2
    if kill -0 "$pid" 2>/dev/null; then
        err "failed to stop llama-server (PID $pid)"
        return 1
    fi
}

_native_wait_until_healthy() {
    local port="$1"
    local tries="${2:-36}"
    local i
    for i in $(seq 1 "$tries"); do
        if _native_health_ok "$port"; then
            return 0
        fi
        sleep 5
        printf '.'
    done
    printf '\n'
    return 1
}

# ──────────────────────────────────────────────────────────────────────────────
# Native command implementations (called from openmono)
# ──────────────────────────────────────────────────────────────────────────────

# True if llama-server answers on any expected bind address.
_native_health_ok() {
    local port="${1:-${LLAMA_NATIVE_PORT:-7474}}"
    local health host
    health="$(_native_health_url "$port")"
    curl -sf --max-time 2 "$health" &>/dev/null && return 0
    curl -sf --max-time 2 "http://127.0.0.1:${port}/health" &>/dev/null && return 0
    curl -sf --max-time 2 "http://localhost:${port}/health" &>/dev/null && return 0
    # shellcheck disable=SC1091
    if [[ -f "$(dirname "${BASH_SOURCE[0]}")/lib/tailscale-cli.sh" ]]; then
        source "$(dirname "${BASH_SOURCE[0]}")/lib/tailscale-cli.sh"
        if resolve_tailscale_cli && tailscale_is_running; then
            host="$(tailscale_cmd ip -4 2>/dev/null | head -1 || true)"
            [[ -n "$host" ]] && curl -sf --max-time 2 "http://${host}:${port}/health" &>/dev/null && return 0
        fi
    fi
    return 1
}

native_cmd_start() {
    _native_load_config || return 1
    local port="${LLAMA_NATIVE_PORT:-7474}"

    # Check if llama-server is already healthy (best indicator)
    if _native_health_ok "$port"; then
        ok "llama-server already running on port ${port}"
        return 0
    fi

    _native_start_llama || return 1

    info "Waiting for llama-server to be healthy (model load: 1-3 min)..."
    if _native_wait_until_healthy "$port" 36; then
        ok "llama-server is healthy on port ${port} (Metal GPU)"
        return 0
    fi
    warn "llama-server did not become healthy within 180s — check: tail -f $HOME/.openmono/logs/llama-server.log"
    return 1
}

native_cmd_stop() {
    _native_load_config || true
    local port
    port="$(_native_port)"
    _native_stop_llama "$port" || return 1
    ok "llama-server stopped"
}

native_cmd_restart() {
    _native_load_config || return 1
    local port="${LLAMA_NATIVE_PORT:-7474}"
    _native_stop_llama "$port" || true
    sleep 1
    _native_start_llama || return 1
    info "Waiting for llama-server to be healthy..."
    if _native_wait_until_healthy "$port" 36; then
        ok "llama-server restarted and healthy on port ${port} (Metal GPU)"
        return 0
    fi
    warn "llama-server did not become healthy within 180s"
    return 1
}

native_cmd_logs() {
    local log_file="$HOME/.openmono/logs/llama-server.log"
    if [[ ! -f "$log_file" ]]; then
        err "Log file not found: $log_file"
        return 1
    fi
    tail -f "$log_file"
}

native_cmd_status() {
    _native_load_config || return 1
    local port="${LLAMA_NATIVE_PORT:-7474}"
    echo ""
    echo "╭─ Native Inference (Metal GPU) ────────────────────────────╮"

    # Check if running
    local pid proc_name
    pid=$(lsof -ti ":${port}" 2>/dev/null | head -1 || true)
    if [[ -n "$pid" ]]; then
        proc_name=$(ps -p "$pid" -o comm= 2>/dev/null || true)
        if [[ "$proc_name" == *"llama-server"* ]]; then
            local cpu mem
            cpu=$(ps -p "$pid" -o %cpu= 2>/dev/null || echo "—")
            mem=$(ps -p "$pid" -o %mem= 2>/dev/null || echo "—")
            printf "│ Status          : ${GREEN}Running${NC} (PID $pid)\n"
            printf "│ CPU/Mem         : %s%% / %s%%\n" "$cpu" "$mem"
        else
            printf "│ Status          : ${YELLOW}Port in use${NC} by $proc_name (PID $pid)\n"
        fi
    else
        printf "│ Status          : ${RED}Not running${NC}\n"
    fi

    printf "│ Port            : %s\n" "$port"
    printf "│ Model           : %s\n" "$MODEL_NAME"
    printf "│ Context Size    : %s\n" "$LLAMA_CTX_SIZE"
    if [[ -n "$LLAMA_MMPROJ_PATH" ]]; then
        printf "│ Vision          : ${GREEN}enabled${NC} (%s)\n" "$(basename "$LLAMA_MMPROJ_PATH")"
    else
        printf "│ Vision          : disabled\n"
    fi

    local health_url
    health_url="$(_native_health_url "$port")"
    if _native_health_ok "$port"; then
        printf "│ Health Endpoint : ${GREEN}✓${NC} %s\n" "$health_url"
    else
        printf "│ Health Endpoint : ${YELLOW}✗${NC} %s (offline)\n" "$health_url"
    fi

    # Apple Silicon info
    local chip_name memory_gb
    chip_name=$(sysctl -n machdep.cpu.brand_string 2>/dev/null || echo "Apple Silicon")
    memory_gb=$(( $(sysctl -n hw.memsize 2>/dev/null || echo 0) / 1024 / 1024 / 1024 ))
    printf "│ Apple Silicon   : %s (%s GB)\n" "$chip_name" "$memory_gb"

    echo "╰────────────────────────────────────────────────────────────╯"
    echo ""
}

native_cmd_agent() {
    _native_load_config || return 1
    local port="${LLAMA_NATIVE_PORT:-7474}"

    if _native_health_ok "$port"; then
        return 0
    fi
    info "llama-server not running — starting it (Metal, model load: 1–3 min)..."
    _native_start_llama || return 1
    info "Waiting for llama-server to be healthy..."
    if _native_wait_until_healthy "$port" 36; then
        ok "llama-server is healthy"
        return 0
    fi
    # cmd_agent() starts the Docker agent only after this returns success.
    err "llama-server did not become healthy — agent not started"
    return 1
}

native_cmd_tunnel_rotate_key() {
    _native_load_config || return 1
    local port="${LLAMA_NATIVE_PORT:-7474}"

    # Generate new API key
    local new_key
    new_key="$(openssl rand -hex 24)"

    local docker_env
    docker_env="$(_native_docker_env)"
    upsert_env_key "$docker_env" LLAMA_API_KEY "$new_key"
    ok "LLAMA_API_KEY rotated in docker/.env"

    # Restart only a real llama-server, including one still loading the model.
    local pid other
    pid="$(_native_llama_pid "$port" || true)"
    if [[ -n "$pid" ]]; then
        _native_stop_llama "$port" || true
        sleep 1
        info "Restarting native llama-server with new API key..."
        if _native_start_llama; then
            ok "llama-server restarted (Metal)"
        else
            warn "Restart failed — run manually: openmono start"
            return 1
        fi
    else
        other="$(lsof -ti ":${port}" 2>/dev/null | head -n 1 || true)"
        if [[ -n "$other" ]]; then
            warn "Port ${port} is in use by PID $other — not llama-server"
            warn "Manually restart llama-server after verifying port is free: openmono restart"
        else
            warn "llama-server is not running — start it with: openmono start"
        fi
    fi

    # Print config commands for agent box (Tailscale preferred over relay)
    local endpoint=""
    local ts_cache="$HOME/.openmono/tailscale-tunnel.json"
    local relay_cache="$HOME/.openmono/relay.json"
    if [[ -f "$ts_cache" ]]; then
        endpoint="$(jq -r '.endpoint // empty' "$ts_cache" 2>/dev/null || true)"
    fi
    if [[ -z "$endpoint" && -f "$relay_cache" ]]; then
        local frps_addr remote_port
        frps_addr="$(jq -r '.frpsAddress // empty' "$relay_cache" 2>/dev/null)"
        remote_port="$(jq -r '.remotePort // empty' "$relay_cache" 2>/dev/null)"
        [[ -n "$frps_addr" && -n "$remote_port" ]] && \
            endpoint="http://$frps_addr:$remote_port"
    fi

    echo ""
    printf "${BLUE}%s${NC}\n" "$(printf '─%.0s' $(seq 1 60))"
    printf "${BLUE}${BOLD}  API Key Rotated${NC}\n"
    printf "${BLUE}%s${NC}\n" "$(printf '─%.0s' $(seq 1 60))"
    echo ""
    echo "  Run the following on the agent box to apply the new key:"
    echo ""
    [[ -n "$endpoint" ]] && \
        printf "    ${BOLD}openmono config set llm.endpoint  %s${NC}\n" "$endpoint"
    printf "    ${BOLD}openmono config set llm.api_key   %s${NC}\n" "$new_key"
    echo ""
}
