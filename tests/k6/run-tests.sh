#!/usr/bin/env bash
# Run k6 tests via Docker — no local k6 install required.
# Joins the redislab-net Docker network so it reaches API + Prometheus directly.

set -euo pipefail

SCRIPT="${1:-smoke-test.js}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# On Windows Git Bash, convert /c/... path to C:/... for Docker volume mounts
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
  MOUNT_DIR="$(cd "$SCRIPT_DIR" && pwd -W)"
else
  MOUNT_DIR="$SCRIPT_DIR"
fi

echo ""
echo "========================================"
echo "  k6 Product API Test Runner (Docker)"
echo "========================================"
echo "  Script  : $SCRIPT"
echo "  API     : http://redislab-api:8080"
echo "  Metrics : http://prometheus:9090/api/v1/write"
echo "========================================"
echo ""

docker run --rm -i \
  --network redislab-net \
  -v "${MOUNT_DIR}:/tests" \
  -w /tests \
  -e BASE_URL=http://redislab-api:8080 \
  -e K6_PROMETHEUS_RW_SERVER_URL=http://prometheus:9090/api/v1/write \
  -e K6_PROMETHEUS_RW_PUSH_INTERVAL=5s \
  -e K6_PROMETHEUS_RW_TREND_STATS="p(50),p(95),p(99),min,max" \
  grafana/k6:latest run \
  --out experimental-prometheus-rw \
  "/tests/$SCRIPT"
