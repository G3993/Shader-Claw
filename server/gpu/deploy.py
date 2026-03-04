#!/usr/bin/env python3
"""
Deploy Shader-Claw (headless Chrome + NDI) to Verda GPU pod.

Runs server.js + headless Chrome on an RTX 6000 Ada. The local browser
connects via WS to ws://<pod-ip>:7777/mirror to mirror state directly.
No pod-server.js needed — single process architecture.

Usage:
    python deploy.py list              # Show running instances
    python deploy.py gpu-types         # Show available GPU types
    python deploy.py deploy [--gpu-type TYPE]  # Deploy new instance
    python deploy.py setup <ip>        # Setup on existing instance
    python deploy.py terminate <id>    # Terminate instance
    python deploy.py status <ip>       # Check server status
    python deploy.py logs <ip>         # Tail logs

Requires:
    pip install verda python-dotenv

Environment variables (or .env file):
    VERDA_CLIENT_ID     — Verda API client ID
    VERDA_CLIENT_SECRET — Verda API client secret
"""

from __future__ import annotations
import argparse
import json
import os
import subprocess
import sys
import time
from pathlib import Path
from typing import Optional, Tuple

# Load .env from etherea-ai (where Verda creds live)
try:
    from dotenv import load_dotenv
    # Check multiple locations for .env
    for env_path in [
        Path(__file__).parent / ".env",
        Path.home() / "etherea-ai" / ".env",
        Path.home() / "Shader-Claw" / ".env",
    ]:
        if env_path.exists():
            load_dotenv(env_path)
            break
except ImportError:
    pass

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

DEFAULT_GPU = '1RTX6000ADA.10V'  # RTX 6000 Ada 48GB — $0.83/hr, plenty for ISF
DEFAULT_IMAGE = 'ubuntu-24.04-cuda-12.8-open-docker'
SC_PORT = 7777                              # Shader-Claw server.js port

# Local paths
HERE = Path(__file__).parent
REPO_ROOT = HERE.parent.parent              # Shader-Claw/
SHADER_DIR = REPO_ROOT / "shaders"

# Shader-Claw repo files/dirs to upload for headless Chrome mode
SC_UPLOAD_FILES = [
    REPO_ROOT / "server.js",
    REPO_ROOT / "package.json",
    REPO_ROOT / "package-lock.json",
    REPO_ROOT / "index.html",
    REPO_ROOT / "rc.mjs",
]
SC_UPLOAD_DIRS = [
    REPO_ROOT / "js",
    REPO_ROOT / "css",
    REPO_ROOT / "shaders",
    REPO_ROOT / "scenes",
]


# ---------------------------------------------------------------------------
# SSH helpers
# ---------------------------------------------------------------------------

def ssh(ip: str, command: str, timeout: int = 60) -> Tuple[int, str, str]:
    """Run a command on the remote instance via SSH."""
    result = subprocess.run(
        ["ssh", "-o", "StrictHostKeyChecking=no", "-o", "ConnectTimeout=10",
         f"root@{ip}", command],
        capture_output=True, text=True, timeout=timeout,
    )
    return result.returncode, result.stdout.strip(), result.stderr.strip()


def scp_to(local_path: str, ip: str, remote_path: str, recursive: bool = False) -> bool:
    """Copy file or directory to remote instance."""
    args = ["scp", "-o", "StrictHostKeyChecking=no", "-o", "ConnectTimeout=10"]
    if recursive:
        args.append("-r")
    args.extend([local_path, f"root@{ip}:{remote_path}"])
    result = subprocess.run(args, capture_output=True, text=True, timeout=120)
    if result.returncode != 0:
        print(f"  SCP failed: {result.stderr}")
    return result.returncode == 0


def wait_for_ssh(ip: str, timeout: int = 120) -> bool:
    """Wait until SSH is available on the instance."""
    start = time.time()
    while time.time() - start < timeout:
        try:
            rc, _, _ = ssh(ip, "echo ok", timeout=10)
            if rc == 0:
                return True
        except (subprocess.TimeoutExpired, Exception):
            pass
        time.sleep(5)
        print("  Waiting for SSH...")
    return False


# ---------------------------------------------------------------------------
# Verda API
# ---------------------------------------------------------------------------

def get_verda_client():
    """Initialize Verda SDK client."""
    client_id = os.environ.get('VERDA_CLIENT_ID')
    client_secret = os.environ.get('VERDA_CLIENT_SECRET')
    if not client_id or not client_secret:
        print("Error: VERDA_CLIENT_ID and VERDA_CLIENT_SECRET required")
        print("Set them as env vars or in .env file")
        sys.exit(1)
    try:
        from verda import VerdaClient
        return VerdaClient(client_id, client_secret)
    except ImportError:
        print("Error: verda SDK not installed. Run: pip install verda")
        sys.exit(1)


# ---------------------------------------------------------------------------
# Commands
# ---------------------------------------------------------------------------

def cmd_list(args):
    """List running Verda instances."""
    client = get_verda_client()
    instances = client.instances.get()
    running = [i for i in instances if getattr(i, 'status', '').lower() in ('running', 'starting')]

    if not running:
        print("No running instances")
        return

    print(f"\n{'ID':<40} {'Status':<12} {'GPU':<25} {'IP':<18}")
    print("-" * 95)
    for inst in running:
        ip = getattr(inst, 'ip', 'N/A') or 'N/A'
        gpu = getattr(inst, 'instance_type', 'N/A') or 'N/A'
        status = getattr(inst, 'status', 'N/A') or 'N/A'
        print(f"{inst.id:<40} {status:<12} {gpu:<25} {ip:<18}")

    # Check if renderer is running on each
    print()
    for inst in running:
        ip = getattr(inst, 'ip', None)
        if ip:
            try:
                import urllib.request
                url = f"http://{ip}:{SC_PORT}/api/mirror/status"
                req = urllib.request.Request(url)
                with urllib.request.urlopen(req, timeout=3) as resp:
                    data = json.loads(resp.read())
                    connected = data.get('connected', False)
                    print(f"  {ip}: Shader-Claw {'connected' if connected else 'waiting for Chrome'}")
            except Exception:
                print(f"  {ip}: Shader-Claw not responding on port {SC_PORT}")


def cmd_gpu_types(args):
    """List available GPU types from Verda API."""
    client = get_verda_client()
    types = client.instance_types.get()

    print(f"\n{'Type ID':<25} {'Description':<45} {'$/hr':<8}")
    print("-" * 78)
    for t in types:
        type_id = getattr(t, 'instance_type', '?')
        desc = getattr(t, 'description', '?')
        price = getattr(t, 'price_per_hour', '?')
        spot = getattr(t, 'spot_price_per_hour', None)
        price_str = f"${price}" if price else "?"
        if spot:
            price_str += f" (spot: ${spot})"
        print(f"{type_id:<25} {desc:<45} {price_str:<8}")


def cmd_deploy(args):
    """Deploy ISF GPU Renderer to a new Verda instance."""
    gpu_type = args.gpu_type or DEFAULT_GPU
    client = get_verda_client()

    print(f"Deploying Shader-Claw (headless Chrome) on {gpu_type}...")

    # Get SSH keys
    ssh_keys = client.ssh_keys.get()
    if not ssh_keys:
        print("Error: No SSH keys registered on Verda")
        print("Add one at https://cloud.verda.ai/account/ssh-keys")
        sys.exit(1)
    ssh_key_ids = [k.id for k in ssh_keys]

    # Create instance (no startup script — we'll set up via SSH)
    print(f"  Creating instance (GPU: {gpu_type})...")
    instance = client.instances.create(
        instance_type=gpu_type,
        image=DEFAULT_IMAGE,
        ssh_key_ids=ssh_key_ids,
        hostname='shaderclaw-gpu',
        description='Shader-Claw headless Chrome + NDI',
    )
    print(f"  Instance ID: {instance.id}")

    # Wait for IP
    print("  Waiting for IP assignment...")
    ip = None
    for _ in range(60):
        time.sleep(5)
        instances = client.instances.get()
        for inst in instances:
            if inst.id == instance.id:
                ip = getattr(inst, 'ip', None)
                if ip:
                    break
        if ip:
            break
        print("  .", end="", flush=True)
    print()

    if not ip:
        print("Error: Timed out waiting for IP")
        return

    print(f"  Instance IP: {ip}")

    # Wait for SSH
    print("  Waiting for SSH...")
    if not wait_for_ssh(ip):
        print("Error: SSH not available after timeout")
        return

    # Run setup
    setup_instance(ip)

    print(f"\n{'='*60}")
    print(f"Shader-Claw (headless Chrome) deployed!")
    print(f"  Instance: {instance.id}")
    print(f"  IP:       {ip}")
    print(f"  GPU:      {gpu_type}")
    print(f"  Server:   http://{ip}:{SC_PORT}")
    print(f"  Mirror:   ws://{ip}:{SC_PORT}/mirror")
    print(f"  Stream:   http://{ip}:{SC_PORT}/api/mirror/frame/stream")
    print(f"  Status:   http://{ip}:{SC_PORT}/api/mirror/status")
    print(f"\nConnect from local UI: enter {ip}:{SC_PORT} in GPU Pod panel")
    print(f"{'='*60}")


def cmd_setup(args):
    """Set up renderer on an existing instance (by IP)."""
    setup_instance(args.ip)


def setup_instance(ip: str):
    """Install Node.js, Chrome, upload Shader-Claw repo, start server.js + headless Chrome."""

    print(f"\n--- Setting up Shader-Claw (headless Chrome) on {ip} ---")

    # Step 1: Install system deps — Node.js 20+, Google Chrome, EGL libs
    print("\n[1/5] Installing system dependencies (Node.js, Chrome, EGL)...")
    rc, out, err = ssh(ip, (
        "apt-get update -qq && "
        "apt-get install -y -qq ca-certificates curl gnupg wget fonts-liberation > /dev/null 2>&1 && "
        # Node.js 20 via NodeSource
        "mkdir -p /etc/apt/keyrings && "
        "curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg 2>/dev/null && "
        "echo 'deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_20.x nodistro main' > /etc/apt/sources.list.d/nodesource.list && "
        "apt-get update -qq && "
        "apt-get install -y -qq nodejs libegl1 libgl1 libgles2 libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libgbm1 > /dev/null 2>&1 && "
        # Google Chrome (not snap Chromium — snap has GPU access issues in headless)
        "if ! command -v google-chrome-stable >/dev/null 2>&1; then "
        "  wget -q -O /tmp/chrome.deb https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb && "
        "  dpkg -i /tmp/chrome.deb 2>/dev/null; apt-get install -f -y -qq 2>/dev/null; "
        "fi && "
        "node --version && google-chrome-stable --version && "
        "echo 'OK'"
    ), timeout=180)
    if "OK" not in out:
        print(f"  Warning: install may have failed: {err[:300]}")
    else:
        node_ver = [l for l in out.split('\n') if l.startswith('v')]
        print(f"  Done — Node {node_ver[0] if node_ver else '?'}")

    # Step 2: Create project directory
    print("\n[2/5] Creating project directory...")
    ssh(ip, "mkdir -p /opt/shaderclaw /opt/shaderclaw/js /opt/shaderclaw/css /opt/shaderclaw/shaders /opt/shaderclaw/scenes")
    print("  Done")

    # Step 3: Upload Shader-Claw repo files
    print("\n[3/5] Uploading Shader-Claw repo...")
    for f in SC_UPLOAD_FILES:
        if f.exists():
            scp_to(str(f), ip, f"/opt/shaderclaw/{f.name}")
            print(f"  {f.name}")
        else:
            print(f"  (skip) {f.name} — not found")
    for d in SC_UPLOAD_DIRS:
        if d.exists():
            scp_to(str(d), ip, "/opt/shaderclaw/", recursive=True)
            print(f"  {d.name}/")
        else:
            print(f"  (skip) {d.name}/ — not found")

    # Step 4: npm install (sharp for JPEG mirror preview)
    print("\n[4/5] Installing Node.js dependencies...")
    rc, out, err = ssh(ip, (
        "cd /opt/shaderclaw && "
        "npm install --omit=dev 2>&1 && "
        "npm install sharp 2>&1 && "
        "echo 'DEPS_OK'"
    ), timeout=180)
    if "DEPS_OK" not in out:
        print(f"  Warning: npm install may have failed:\n  {err[:400]}")
    else:
        print("  Done")

    # Step 5: Start server.js + headless Chrome
    print("\n[5/5] Starting server.js + headless Chrome...")

    # Kill only our specific processes (don't touch other services like Longlive/Scope)
    ssh(ip, "pkill -f '/opt/shaderclaw/server.js' || true; pkill -f 'chrome.*localhost.*7777' || true")
    time.sleep(1)

    # Use ssh -f -n to fork SSH to background (avoids SSH detach issues with node)
    def ssh_bg(ip, cmd):
        """Launch a command on the remote host without blocking."""
        subprocess.Popen(
            ["ssh", "-f", "-n", "-o", "StrictHostKeyChecking=no", "-o", "ConnectTimeout=10",
             f"root@{ip}", cmd],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )

    # Start server.js on :7777 (Shader-Claw core — static files, WS, NDI, mirror)
    # NO_MCP=1 skips stdio MCP transport (not needed on pod)
    print(f"  Starting server.js on :{SC_PORT}...")
    ssh_bg(ip, (
        f"cd /opt/shaderclaw && PORT={SC_PORT} NO_MCP=1 "
        "node server.js > /var/log/shaderclaw.log 2>&1 &"
    ))
    time.sleep(4)

    # Launch headless Chrome directly (no Puppeteer needed)
    print("  Launching headless Chrome...")
    ssh_bg(ip, (
        "google-chrome-stable --headless=new --no-sandbox "
        "--use-gl=angle --use-angle=gl-egl "
        "--enable-gpu-rasterization --ignore-gpu-blocklist --disable-software-rasterizer "
        "--disable-dev-shm-usage --enable-webgl --enable-webgl2 "
        "--window-size=1920,1080 "
        f"http://localhost:{SC_PORT} "
        "> /var/log/shaderclaw-chrome.log 2>&1 &"
    ))

    # Wait for server to be ready (mirror WS endpoint)
    time.sleep(5)
    for _ in range(10):
        try:
            import urllib.request
            url = f"http://{ip}:{SC_PORT}/api/mirror/status"
            req = urllib.request.Request(url)
            with urllib.request.urlopen(req, timeout=5) as resp:
                data = json.loads(resp.read())
                connected = data.get("connected", False)
                if connected:
                    print(f"  Server ready! Bridge connected.")
                    return
                else:
                    print(f"  Server up, waiting for Chrome to connect...")
        except Exception:
            pass
        time.sleep(3)

    # Check logs if server didn't start
    print("  Warning: Server may not have started. Checking logs...")
    rc, out, err = ssh(ip, "tail -20 /var/log/shaderclaw.log")
    print(out[-300:] if out else "(no server logs)")
    rc, out, err = ssh(ip, "tail -20 /var/log/shaderclaw-chrome.log")
    print(out[-300:] if out else "(no chrome logs)")


def cmd_terminate(args):
    """Terminate a Verda instance."""
    client = get_verda_client()
    from verda.constants import Actions

    print(f"Terminating instance {args.instance_id}...")
    try:
        client.instances.action(args.instance_id, Actions.DELETE)
        print("Done")
    except Exception as e:
        print(f"Error: {e}")


def cmd_status(args):
    """Check renderer status on an instance."""
    ip = args.ip
    try:
        import urllib.request
        url = f"http://{ip}:{SC_PORT}/api/mirror/status"
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.loads(resp.read())
            print(f"Shader-Claw on {ip}:{SC_PORT}")
            print(f"  Connected:  {data.get('connected', '?')}")
            print(f"  GPU:        {data.get('gpu', '?')}")
            print(f"  Shader:     {data.get('shader', 'none')}")
    except Exception as e:
        print(f"Error: Cannot reach server at http://{ip}:{SC_PORT} — {e}")

    # Also check SSH and GPU
    try:
        rc, out, _ = ssh(ip, "nvidia-smi --query-gpu=name,memory.total,memory.used,utilization.gpu --format=csv,noheader", timeout=10)
        if rc == 0 and out:
            print(f"\n  nvidia-smi: {out}")
    except Exception:
        pass


def cmd_logs(args):
    """Show logs from instance."""
    ip = args.ip
    lines = args.lines or 50
    print("=== server.js (Shader-Claw) ===")
    rc, out, err = ssh(ip, f"tail -{lines} /var/log/shaderclaw.log")
    if out:
        print(out)
    else:
        print(f"No server logs found (error: {err[:200]})")
    print(f"\n=== headless Chrome ===")
    rc, out, err = ssh(ip, f"tail -{lines} /var/log/shaderclaw-chrome.log")
    if out:
        print(out)
    else:
        print(f"No chrome logs found (error: {err[:200]})")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Deploy Shader-Claw (headless Chrome) to Verda")
    sub = parser.add_subparsers(dest="command")

    sub.add_parser("list", help="List running Verda instances")
    sub.add_parser("gpu-types", help="List available GPU types")

    p_deploy = sub.add_parser("deploy", help="Deploy new instance")
    p_deploy.add_argument("--gpu-type", default=DEFAULT_GPU,
                          help=f"GPU type (default: {DEFAULT_GPU})")

    p_setup = sub.add_parser("setup", help="Setup renderer on existing instance")
    p_setup.add_argument("ip", help="Instance IP address")

    p_term = sub.add_parser("terminate", help="Terminate instance")
    p_term.add_argument("instance_id", help="Verda instance ID")

    p_status = sub.add_parser("status", help="Check renderer status")
    p_status.add_argument("ip", help="Instance IP address")

    p_logs = sub.add_parser("logs", help="Show renderer logs")
    p_logs.add_argument("ip", help="Instance IP address")
    p_logs.add_argument("--lines", "-n", type=int, default=50, help="Number of lines")

    args = parser.parse_args()

    commands = {
        "list": cmd_list,
        "gpu-types": cmd_gpu_types,
        "deploy": cmd_deploy,
        "setup": cmd_setup,
        "terminate": cmd_terminate,
        "status": cmd_status,
        "logs": cmd_logs,
    }

    if args.command in commands:
        commands[args.command](args)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
