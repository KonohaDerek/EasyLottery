#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
port="${DEVSERVER_SMOKE_PORT:-18943}"
log="$(mktemp)"
pid=""
cleanup() {
  if [ -n "$pid" ]; then
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  fi
  cat "$log"
  rm -f "$log"
}
trap cleanup EXIT
# Build-only CI misses a DevServer package requiring a newer shared framework.
dotnet run --project src/EasyLotteryWasm/EasyLotteryWasm.csproj \
  --configuration Release --no-build --no-launch-profile -- \
  --urls "http://127.0.0.1:$port" >"$log" 2>&1 &
pid=$!
for _ in $(seq 1 60); do
  if ! kill -0 "$pid" 2>/dev/null; then
    echo "DevServer exited before becoming ready" >&2
    exit 1
  fi
  if grep -Fq "Now listening on: http://127.0.0.1:$port" "$log" && \
      curl --fail --silent --max-time 5 "http://127.0.0.1:$port/" >/dev/null; then
    curl --fail --silent --max-time 5 "http://127.0.0.1:$port/_framework/blazor.webassembly.js" >/dev/null
    echo "DevServer and Blazor framework assets are responding"
    exit 0
  fi
  sleep 1
done
echo "DevServer did not become ready" >&2
exit 1
