# Setup & Commands

## Requirements

| | |
|--|--|
| **OS** | Ubuntu 26.04 LTS (recommended) · 25.10 · macOS 14+ on Apple Silicon |
| **GPU mode** | NVIDIA GPU · 12 GB VRAM minimum · 24 GB recommended |
| **Mac inference** | Apple Silicon · Metal via Homebrew llama.cpp · 16 GB unified memory minimum |
| **CPU mode** | 24 GB RAM |
| **Disk** | ~20 GB free for the model download |

---

## Step 1 — Install

```bash
bash <(curl -fsSL https://raw.githubusercontent.com/StartupHakk/OpenMonoAgent.ai/refs/heads/main/get-openmono.sh)
```

The installer will ask you one question before it does anything:

> [!IMPORTANT]
> ### What do you want to install on this machine?
>
> 1. **Both** — agent + inference server on one box (single-box mode)
> 2. **Inference server only** — inference box that runs the model (dual-box mode)
> 3. **Agent only** — laptop/workstation that talks to a remote inference server
>
> *Picking **2** or **3**? See the [Dual-box setup](#dual-box-setup) section below for the final connection steps.*


| Option | Pick this if… |
|--------|--------------|
| **1 — Both** | You have one machine and want everything on it |
| **2 — Inference** | Dedicated inference box — do this first, then run option 3 on your agent box |
| **3 — Agent** | Your agent box — run this after option 2 is set up on the inference box |

> [!TIP]
> Not sure? Pick **1**. If you have a GPU, this is the best starting point. If you're on CPU, make sure you have at least 24 GB RAM — the machine needs to run both the model and the agent. If you'd rather keep model load on a separate machine, go for **2** and **3**.

---

## Step 2 — During setup

The install runs in two phases, each with its own `[1/8]` → `[8/8]` progress.

**Phase 1** installs prerequisites: Docker, .NET 10, build tools, and the NVIDIA stack if a GPU is found.

**Phase 2** downloads the model, builds Docker images, and starts the inference server.

### GPU prompt

If an NVIDIA GPU is detected, Phase 1 asks:

```
  NVIDIA GPU Detected
  Would you like to install on GPU? (Y/n):
```

Say **Y**.

### Reboot (GPU driver installs only)

If the NVIDIA drivers are being installed fresh, a reboot is required:

```
  NVIDIA drivers installed — reboot required
  Would you like to reboot now? (Y/n):
  ℹ  After reboot, run: ~/openmono.ai/openmono setup
```

> [!IMPORTANT]
> After rebooting, use the **full path** — `openmono` won't be on your PATH until setup fully completes:
> ```bash
> ~/openmono.ai/openmono setup
> ```
> The installer picks up at Phase 2 automatically.

### Model download

The only slow step. The installer picks the right model based on your VRAM:

| VRAM | Model | Size | Accuracy |
|------|-------|------|----------|
| 24 GB+ | Qwen3.6-27B-Q4_K_M | ~15.5 GB | Full |
| 16 GB | Qwen3.6-35B-A3B-UD-IQ3_S | ~12 GB | Lower |
| 12 GB | Qwen3.5-9B-Q4_K_M | ~5 GB | Lower |
| CPU | Qwen3.6-35B-A3B-UD-Q4_K_XL | ~17.6 GB | Full |

> [!NOTE]
> These are the default models for each tier. If you have more VRAM or RAM available, you can swap to a higher quant for better accuracy — or a lower quant to free up memory. Context size is also configurable: a larger window gives the agent more working memory but requires more RAM. Both can be changed in `settings.json` via `llm.model` and `llm.contextSize`, or by editing `docker/docker-compose.override.yml` directly.

To override auto-detection:

```bash
openmono setup --gpu     # force GPU (NVIDIA only)
openmono setup --cpu     # force CPU
```

> [!NOTE]
> You may see a `.NET SDK not installed` warning at the start of Phase 2 — safe to ignore. The SDK was just installed but the current shell session hasn't loaded it yet.

> [!TIP]
> Full install log is saved to `~/.openmono/logs/setup-<timestamp>.log`

---

## Step 3 — After install

When setup finishes you'll see:

```text
────────────────────────────────────────────────────────────
  Setup Complete
────────────────────────────────────────────────────────────

  ✓ OpenMono.ai is ready to use!

  Your machine is configured for single-box mode (agent + inference).

  Next steps:
    1. cd your-project/
    2. openmono agent                 # Start the agent

  Other commands:
    openmono status              # Show llama-server status
    openmono config             # Configure settings

  Troubleshooting:
    If openmono or docker are not found, reload your shell:
      newgrp docker     # Activate docker group (Linux only)
      source ~/.bashrc  # Reload shell config (bash)
      exec $SHELL       # Reload shell

  Full help: openmono --help
────────────────────────────────────────────────────────────
```

Reload your shell so the `openmono` command is on your PATH:

```bash
source ~/.bashrc
```

If `openmono` or `docker` are still not found after that:

```bash
newgrp docker      # activate docker group without logging out
exec $SHELL        # reload shell
```

Confirm the inference server is running:

```bash
openmono status
```

---

## Step 4 — Run the agent

Navigate to any project and start the agent:

```bash
cd your-project/

openmono agent            # TUI — interactive panel layout (default)
openmono agent --classic  # CLI — plain scrolling terminal
```

Once it's running, just type what you need in plain English:

```
Explain what this codebase does
Find all usages of AuthService
Fix the failing tests in UserController
Refactor this function to be async
Add error handling to the payment flow
```

OpenMono navigates your codebase, proposes solutions, and executes changes with full transparency. You stay in control throughout — the agent shows its work at every step and asks before making any major actions, including file reads, edits, and running commands.

> [!TIP]
> Type `/think` or press `Ctrl+T` to enable step-by-step reasoning mode — best for complex bugs, large refactors, and architecture decisions. Turn it off for simple lookups and quick edits.

---

## Step 5 — Daily use

```bash
openmono start      # start the inference server
openmono stop       # stop everything
openmono restart    # restart the inference server
openmono status     # container · model status
openmono logs       # tail live inference logs
openmono help       # list all commands
```

---

## Dual-box setup

Run the model on a dedicated inference box and connect from your laptop over the internet. No port forwarding required — the tunnel is established outbound from the inference box.

![Dual-box setup diagram](assets/dual-box-server.png)

### Step 1 — Install on the inference box (option 2)

On the inference box, run the installer and pick **2 — Inference server only**:

```bash
bash <(curl -fsSL https://raw.githubusercontent.com/StartupHakk/OpenMonoAgent.ai/refs/heads/main/get-openmono.sh)
```

Select **2** when prompted. The installer downloads the model and starts llama-server. No agent is installed on this machine.

Confirm it's running:

```bash
openmono status
```

### Step 2 — Register the inference box with the relay

Still on the inference box, run tunnel setup:

```bash
openmono tunnel setup
```

You'll receive a one-time verification code. Enter it at [app.openmonoagent.ai](https://app.openmonoagent.ai) — you'll get an email with a step-by-step guide including your relay endpoint and API key.

> [!NOTE]
> The code expires in 15 minutes.

Then start the tunnel:

```bash
openmono tunnel start
```

Confirm the tunnel is up:

```bash
openmono tunnel status
```

### Step 3 — Install on the laptop (option 3)

Once the inference box is running and the tunnel is up, switch to your laptop and run the installer there:

```bash
bash <(curl -fsSL https://raw.githubusercontent.com/StartupHakk/OpenMonoAgent.ai/refs/heads/main/get-openmono.sh)
```

Select **3** when prompted. This installs the agent but skips Docker, model download, and llama-server — the laptop needs no GPU.

### Step 4 — Point the agent at the relay

Using the endpoint and API key from the email in Step 2:

```bash
openmono config set llm.endpoint http://relay.openmonoagent.ai:<port>
openmono config set llm.api_key <token>
```

### Step 5 — Run the agent

```bash
cd your-project/
openmono agent
```

The agent on your laptop sends requests through the relay to the inference box.

> [!NOTE]
> Don't have a relay account? Sign up free at [app.openmonoagent.ai](https://app.openmonoagent.ai).

### Troubleshooting (relay and Tailscale)

> [!CAUTION]
> **401 Unauthorized** — the API key on your laptop doesn't match the one on the inference box.
>
> Check both values:
> ```bash
> # On the inference box
> grep LLAMA_API_KEY docker/.env
>
> # On the laptop
> openmono config get llm.api_key
> ```
> If they differ, copy the inference box value to the laptop:
> ```bash
> openmono config set llm.api_key <value-from-inference-box>
> ```

---

### Tunnel commands (inference box)

```bash
openmono tunnel setup              # frp relay (Linux/systemd)
openmono tunnel setup --tailscale  # Tailscale MagicDNS / 100.x (macOS + Linux)
openmono tunnel start              # start the frpc tunnel (frp only)
openmono tunnel stop               # stop the tunnel (frp only)
openmono tunnel restart            # restart (frp only)
openmono tunnel status             # show frpc + Tailscale state / endpoints
openmono tunnel logs               # tail frpc logs (frp only)
```

> [!NOTE]
> `start` / `stop` / `restart` / `logs` apply only to the frp relay service. Tailscale is managed by its own daemon (`tailscaled`).

---

## Dual-box via Tailscale

Use Tailscale instead of the OpenMonoAgent frp relay when both boxes are on the same tailnet. Works on **macOS and Linux** (no systemd required for the tunnel itself).

### Step 1 — Install Tailscale on both boxes

Install and join the **same** tailnet on the inference box and the agent box:

- macOS: `brew install --cask tailscale` or [tailscale.com/download/mac](https://tailscale.com/download/mac)
- Linux: `curl -fsSL https://tailscale.com/install.sh | sh` or [tailscale.com/download/linux](https://tailscale.com/download/linux)

Join with browser login, or with an auth key (see [Creating Tailscale keys](#creating-tailscale-keys) below).

Before installing OpenMono, you can run a readiness check on each Mac:

```bash
openmono tunnel prepare              # auto: arm64 → inference, else agent
openmono tunnel prepare --inference  # force Mini checklist
openmono tunnel prepare --agent      # force agent checklist
```

### Step 2 — Inference box: run Tailscale tunnel setup

With llama-server already installed (`openmono setup` role **2 — Inference server only**, or full/single-box):

```bash
openmono tunnel setup --tailscale
# optional non-interactive join:
# openmono tunnel setup --tailscale --auth-key tskey-auth-...
# or: TS_AUTHKEY=tskey-auth-... openmono tunnel setup --tailscale
```

The script reuses an existing Tailscale session when possible, ensures `LLAMA_API_KEY` exists, and prints the MagicDNS (preferred) or `100.x` endpoint.

**Manual alternative** (no helper script):

```bash
tailscale up                    # or: tailscale up --auth-key tskey-auth-...
tailscale ip -4                 # note the 100.x address
# MagicDNS name also appears in: tailscale status
openmono start                  # ensure llama-server is listening on :7474
grep LLAMA_API_KEY docker/.env  # or: openmono config get llm.api_key
```

### Step 3 — Agent box: point at the Tailscale endpoint

On the laptop (same tailnet):

```bash
openmono config set llm.endpoint  http://<magicdns-or-100.x>:7474
openmono config set llm.api_key   <token-from-inference-box>
cd your-project/
openmono agent
```

### Creating Tailscale keys

Create keys in the admin console: [login.tailscale.com/admin/settings/keys](https://login.tailscale.com/admin/settings/keys)

| Kind | Prefix | Use with OpenMono |
|------|--------|-------------------|
| **Auth key** | `tskey-auth-` | Join devices (`tailscale up --auth-key=...` / `openmono tunnel setup --tailscale`). This is what dual-box setup needs. |
| **API access token** | `tskey-api-` | Call Tailscale’s admin API (e.g. mint auth keys programmatically). **Not** required for day-to-day dual-box. |

Recommended auth-key settings for an always-on inference box:

- **Not ephemeral** (keep the node after reboot/offline)
- **Pre-approved** if your tailnet has device approval enabled
- Reusable only if you intentionally want one key for multiple machines

Advanced (optional): mint an auth key from an API access token:

```bash
curl -s -u "tskey-api-XXXX:" \
  -H 'Content-Type: application/json' \
  -d '{"capabilities":{"devices":{"create":{"reusable":false,"ephemeral":false,"preauthorized":true}}},"expirySeconds":86400}' \
  "https://api.tailscale.com/api/v2/tailnet/-/keys"
```

Use the returned `key` (`tskey-auth-...`) with `openmono tunnel setup --tailscale --auth-key ...`.

### Tailscale troubleshooting

> [!CAUTION]
> **Logged out / Failed to load preferences** — open Tailscale.app and complete browser login. `openmono tunnel status` prints the live login URL when the node is in `NeedsLogin`.

> [!CAUTION]
> **Cannot reach endpoint** — confirm both machines show `Running` in `tailscale status`, use the inference box MagicDNS name or `100.x` IP (not a LAN IP), and check tailnet ACLs allow TCP **7474** between agent and inference tags/users.

> [!CAUTION]
> **401 Unauthorized** — same as the relay path: `LLAMA_API_KEY` on the inference box must match `llm.api_key` on the agent. See [Troubleshooting (relay and Tailscale)](#troubleshooting-relay-and-tailscale) above.

See also [docs/SECURITY.md](SECURITY.md) for dual-box hardening (API keys, bind host, ACLs).

---

## Slash commands

| Command | What it does |
|---------|-------------|
| `/help` | List all commands and keyboard shortcuts |
| `/think` | Toggle step-by-step reasoning mode |
| `/plan` | Restrict agent to read-only tools for safe exploration |
| `/model <name>` | Switch model mid-session |
| `/compact [focus]` | Summarize history to free up context |
| `/checkpoint` | Save a named checkpoint in the conversation |
| `/undo [n]` | Revert the last n file changes |
| `/resume [id]` | Resume a previous session |
| `/export [format] [path]` | Export as `markdown`, `json`, or `html` |
| `/status` | Turn count, token usage, model, working directory |
| `/stats` | Token and tool call statistics |
| `/init` | Generate an `OPENMONO.md` for the current project |
| `/clear` | Clear context and start fresh |
| `/debug` | Toggle verbose debug output |
| `/retry` | Resend the last message |
| `/quit` | Exit OpenMono |

---

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| <kbd>Ctrl</kbd>+<kbd>C</kbd> | Cancel active turn · double-tap to exit |
| <kbd>Ctrl</kbd>+<kbd>U</kbd> | Clear input line |
| <kbd>Ctrl</kbd>+<kbd>W</kbd> | Delete last word |
| <kbd>Ctrl</kbd>+<kbd>P</kbd> | Open command picker |
| <kbd>Tab</kbd> | Autocomplete command or file path |
| <kbd>Esc</kbd> | Cancel active request · dismiss suggestions |
| <kbd>F1</kbd> | Help overlay |
| <kbd>↑</kbd> / <kbd>↓</kbd> | Navigate input history |
| <kbd>PageUp</kbd> / <kbd>PageDown</kbd> | Scroll conversation |

Shortcuts can be customised in `~/.openmono/tui.json` (user-wide) or `.openmono/tui.json` (per project).

---

## Configuration

Settings live in `~/.openmono/settings.json` (user-wide) or `.openmono/settings.json` (per project):

```bash
openmono config set llm.endpoint http://localhost:7474
openmono config set llm.model qwen3.6-27b
openmono config get llm.endpoint
```

→ [Full configuration reference](CONFIG.md)
