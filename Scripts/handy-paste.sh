#!/usr/bin/env bash
# handy-paste.sh - Handy "External Script" paste hook (Linux).
#
# Handy invokes this as:   handy-paste.sh "<transcript>"
# and blocks until it exits. On this paste path Handy pastes nothing itself,
# so this script both (1) hands the text to the game and (2) performs the paste
# unless the game answered {"handled":true}, meaning it consumed the text.
#
# Ships with the se-speech-to-text plugin; the plugin's config dialog
# shows the full path to copy.
#
# Only needed if Handy has no Webhook paste method: a Handy that offers one
# can post to the plugin directly, using the URL from the same dialog.
#
# Install:
#   chmod +x handy-paste.sh
#   Handy -> Settings -> Advanced -> Paste Method -> External Script
#           -> <path from the plugin's config dialog>
#
# Config via environment (Handy passes its own environment through):
#   HANDY_HOOK_PORT     listener port                       (default 5115)
#   HANDY_HOOK_URL      full endpoint, overrides PORT
#   HANDY_HOOK_TIMEOUT  seconds to wait for the API         (default 1)
#   HANDY_HOOK_LOG      log file; Handy discards our stdio  (default none)
#   HANDY_HOOK_PASTE    0 = notify only, do not paste       (default 1)
#   HANDY_HOOK_DELAY    seconds between clipboard and ctrl+v (default 0.05)

set -uo pipefail

PORT="${HANDY_HOOK_PORT:-5115}"
URL="${HANDY_HOOK_URL:-http://127.0.0.1:${PORT}/handy}"
TIMEOUT="${HANDY_HOOK_TIMEOUT:-1}"
LOG="${HANDY_HOOK_LOG:-}"
DO_PASTE="${HANDY_HOOK_PASTE:-1}"
DELAY="${HANDY_HOOK_DELAY:-0.05}"

text="${1-}"
[[ -z "$text" ]] && exit 0

log() { [[ -n "$LOG" ]] && printf '%s %s\n' "$(date -Is)" "$*" >>"$LOG"; return 0; }

build_json() {
  if command -v jq >/dev/null 2>&1; then
    jq -nc --arg t "$text" --arg ts "$(date -Is)" \
      '{source:"handy",event:"pre-paste",text:$t,timestamp:$ts}'
  else
    python3 - "$text" <<'PY'
import datetime, json, sys
print(json.dumps({
    "source": "handy",
    "event": "pre-paste",
    "text": sys.argv[1],
    "timestamp": datetime.datetime.now().astimezone().isoformat(),
}, ensure_ascii=False))
PY
  fi
}

# --- 1. Hand the text to the game, strictly before it reaches the clipboard ---
handled=0
if payload="$(build_json 2>>"${LOG:-/dev/null}")"; then
  if reply="$(curl -sS -m "$TIMEOUT" -X POST "$URL" \
              -H 'Content-Type: application/json; charset=utf-8' \
              -H 'Expect:' \
              --data-binary "$payload" 2>>"${LOG:-/dev/null}")"; then
    case "$reply" in *'"handled":true'*) handled=1 ;; esac
    log "notified $URL -> $reply (${#text} chars)"
  else
    log "notify failed: $URL unreachable - pasting anyway"
  fi
else
  log "payload build failed (need jq or python3) - pasting anyway"
fi

# --- 2. The paste itself, unless the game took the text ---
[[ "$DO_PASTE" == "1" && "$handled" == "0" ]] || exit 0

if [[ -n "${WAYLAND_DISPLAY:-}" || "${XDG_SESSION_TYPE:-}" == "wayland" ]]; then
  wl-copy -- "$text" || { log "wl-copy failed"; exit 1; }
  sleep "$DELAY"
  if command -v ydotool >/dev/null 2>&1; then
    ydotool key 29:1 47:1 47:0 29:0        # ctrl+v
  elif command -v wtype >/dev/null 2>&1; then
    wtype -M ctrl -k v -m ctrl
  else
    log "no ydotool/wtype - text left on the clipboard, paste manually"
  fi
else
  printf '%s' "$text" | xclip -selection clipboard \
    || printf '%s' "$text" | xsel --clipboard --input \
    || { log "xclip/xsel failed"; exit 1; }
  sleep "$DELAY"
  xdotool key --clearmodifiers ctrl+v
fi

exit 0
