#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# OpenMono.ai macOS Tunnel Commands
#
# Manages the frpc tunnel via Homebrew services on macOS.
# Sourced by openmono CLI. Expects info/ok/warn/err and color vars to be defined.
# ──────────────────────────────────────────────────────────────────────────────

FRP_CONFIG_FILE="${FRP_CONFIG_FILE:-$HOME/.config/frp/frpc.toml}"

macos_tunnel_cmd_start() {
    if ! command -v brew &>/dev/null; then
        err "Homebrew not found. Install frpc with: brew install frpc"
        return 1
    fi
    if ! brew list frpc &>/dev/null 2>&1; then
        err "frpc not installed. Run: openmono tunnel setup"
        return 1
    fi

    # Check if already running
    if brew services list 2>/dev/null | grep -q "frpc.*started"; then
        ok "frpc is already running"
        return 0
    fi

    info "Starting frpc tunnel..."
    if ! brew services start frpc >/dev/null 2>&1; then
        err "Failed to start frpc. Check: brew services list"
        return 1
    fi
    sleep 1
    if brew services list 2>/dev/null | grep -q "frpc.*started"; then
        ok "frpc is running"
    else
        err "frpc failed to start. Check: brew services list"
        return 1
    fi
}

macos_tunnel_cmd_stop() {
    info "Stopping frpc tunnel..."
    brew services stop frpc >/dev/null 2>&1 || true
    ok "Stopped"
}

macos_tunnel_cmd_restart() {
    info "Restarting frpc tunnel..."
    if ! brew services restart frpc >/dev/null 2>&1; then
        err "Failed to restart frpc. Check: brew services list"
        return 1
    fi
    sleep 1
    if brew services list 2>/dev/null | grep -q "frpc.*started"; then
        ok "frpc is running"
    else
        err "frpc failed to restart"
        return 1
    fi
}

macos_tunnel_cmd_status() {
    echo ""
    echo -e "${BLUE}── frpc (tunnel) service ────────────────────────────${NC}"
    if ! command -v brew &>/dev/null; then
        warn "Homebrew not found — frpc is not installed"
        return 0
    fi
    if ! brew list frpc &>/dev/null 2>&1; then
        warn "frpc not installed. Run: openmono tunnel setup"
        return 0
    fi
    if brew services list 2>/dev/null | grep -q "frpc.*started"; then
        ok "frpc → running (brew services)"
    else
        warn "frpc → stopped"
    fi

    echo ""
    echo -e "${BLUE}── Configured target ────────────────────────────────${NC}"
    # Check both config locations
    local cfg="$FRP_CONFIG_FILE"
    local brew_cfg
    brew_cfg="$(brew --prefix 2>/dev/null)/etc/frp/frpc.toml"
    [[ ! -f "$cfg" && -f "$brew_cfg" ]] && cfg="$brew_cfg"

    if [[ -f "$cfg" ]]; then
        local server_addr remote_port
        server_addr="$(awk -F'"' '/^serverAddr/{print $2; exit}' "$cfg" 2>/dev/null)"
        remote_port="$(awk -F'= *' '/^remotePort/{print $2; exit}' "$cfg" 2>/dev/null | tr -d ' ')"
        [[ -n "$server_addr" ]] && info "serverAddr = $server_addr"
        [[ -n "$remote_port" ]] && info "remotePort = $remote_port"
        if [[ -n "$server_addr" && -n "$remote_port" ]]; then
            info "Public endpoint: http://$server_addr:$remote_port"
        fi
    else
        warn "$cfg not found"
    fi
    echo ""
}

macos_tunnel_cmd_logs() {
    info "Streaming frpc logs (press Ctrl+C to stop)..."
    log stream --predicate 'process=="frpc"' --level debug
}
