#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

image_name="easy-lottery-api-smoke"
container_name="easy-lottery-api-smoke"

cleanup() {
  docker rm -f "$container_name" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker build -f src/EasyLotteryAPI/Dockerfile -t "$image_name" .
docker run -d --name "$container_name" -p 18930:18930 -e ASPNETCORE_ENVIRONMENT=Production -e Storage__Directory=/data "$image_name"

for _ in $(seq 1 30); do
  if curl --fail --silent http://localhost:18930/health/live >/dev/null 2>&1; then
    break
  fi
  sleep 2
  if ! docker ps --format '{{.Names}}' | grep -qx "$container_name"; then
    echo "Container exited before becoming healthy" >&2
    docker logs "$container_name" >&2 || true
    exit 1
  fi
done

curl --fail http://localhost:18930/health/live >/dev/null
curl --fail http://localhost:18930/health/ready >/dev/null
curl --fail --silent http://localhost:18930/ >/dev/null
curl --fail --silent http://localhost:18930/index.html >/dev/null
curl --fail --silent http://localhost:18930/_framework/blazor.webassembly.js >/dev/null

echo "Health endpoints and Blazor static assets are responding"
