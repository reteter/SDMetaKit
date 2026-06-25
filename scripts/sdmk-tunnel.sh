#!/usr/bin/env bash
set -euo pipefail

# ── Konfiguracja ──────────────────────────────────────────────────────────
API_PORT="${API_PORT:-5000}"
CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/sdmk"
URL_FILE="$CONFIG_DIR/tunnel-url"
LOG_FILE="$CONFIG_DIR/tunnel.log"
PID_FILE="$CONFIG_DIR/tunnel.pid"
LOCK_FILE="$CONFIG_DIR/tunnel.lock"

# ── Funkcje ───────────────────────────────────────────────────────────────

start_tunnel() {
    mkdir -p "$CONFIG_DIR"

    echo "[$(date +%T)] Starting cloudflared tunnel → localhost:$API_PORT ..."

    # Uruchom cloudflared, wyciągnij URL z outputu
    cloudflared tunnel --url "http://localhost:$API_PORT" \
        --no-autoupdate \
        --metrics localhost:63542 \
        2>&1 \
    | tee -a "$LOG_FILE" \
    | while IFS= read -r line; do
        if [[ "$line" =~ https://[a-zA-Z0-9.-]+\.trycloudflare\.com ]]; then
            url="${BASH_REMATCH[0]}"
            echo "$url" > "$URL_FILE"
            echo "[$(date +%T)] Tunnel URL: $url (saved to $URL_FILE)"
        fi
      done &

    TUNNEL_PID=$!
    echo "$TUNNEL_PID" > "$PID_FILE"
    echo "[$(date +%T)] Started (PID $TUNNEL_PID)"
}

stop_tunnel() {
    if [[ -f "$PID_FILE" ]]; then
        pid=$(cat "$PID_FILE")
        echo "[$(date +%T)] Stopping tunnel (PID $pid) ..."
        kill "$pid" 2>/dev/null || true
        pkill -f "cloudflared tunnel" 2>/dev/null || true
        rm -f "$PID_FILE"
    fi
    rm -f "$LOCK_FILE"
}

cleanup() {
    stop_tunnel
    exit 0
}

show_url() {
    if [[ -f "$URL_FILE" ]]; then
        echo "Tunnel URL: $(cat "$URL_FILE")"
    else
        echo "No tunnel URL saved yet."
    fi
}

# ── Główna logika ─────────────────────────────────────────────────────────

case "${1:-start}" in
    start)
        trap cleanup SIGINT SIGTERM EXIT

        if [[ -f "$LOCK_FILE" ]]; then
            echo "Already running (lock file: $LOCK_FILE)"
            show_url
            exit 1
        fi
        touch "$LOCK_FILE"

        # Watchdog - restartuje gdy padnie
        while true; do
            start_tunnel
            wait $TUNNEL_PID 2>/dev/null || true
            echo "[$(date +%T)] Tunnel process exited. Restarting in 3s ..."
            sleep 3
        done
        ;;

    stop)
        stop_tunnel
        echo "Stopped."
        ;;

    restart)
        "$0" stop
        sleep 1
        "$0" start
        ;;

    url)
        show_url
        ;;

    log)
        tail -f "$LOG_FILE" 2>/dev/null || echo "No log file yet."
        ;;

    *)
        echo "Usage: $0 {start|stop|restart|url|log}"
        exit 2
        ;;
esac
