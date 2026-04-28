# k6 Load Tests — Product API

Performance and behavior tests for the RedisLab Product API.  
Runs entirely via Docker — no local k6 install required.

## Architecture

```
k6 (Docker) → Prometheus Remote Write → Prometheus → Grafana
```

k6 joins the `redislab-net` Docker network so it talks directly to the API  
and Prometheus containers by name. Results appear live in Grafana.

## Prerequisites

- Docker Desktop running
- Stack running: `docker compose up -d` from the repo root

## Running Tests

### Option A — `docker compose run` (simplest)

```bash
# from the repo root

# Smoke test (always run first)
docker compose run --rm k6 smoke-test.js

# Load test
docker compose run --rm k6 load-test.js

# Stress test
docker compose run --rm k6 stress-test.js

# Soak test (30 min)
docker compose run --rm k6 soak-test.js

# Cache behaviour test
docker compose run --rm k6 cache-behavior-test.js
```

### Option B — `run-tests.sh` helper (Git Bash / WSL)

```bash
cd tests/k6

bash run-tests.sh smoke-test.js
bash run-tests.sh load-test.js
bash run-tests.sh stress-test.js
bash run-tests.sh soak-test.js
bash run-tests.sh cache-behavior-test.js
```

### Option C — raw `docker run`

```bash
docker run --rm -i \
  --network redislab-net \
  -v "%cd%\tests\k6:/tests" \
  -e BASE_URL=http://redislab-api:8080 \
  -e K6_PROMETHEUS_RW_SERVER_URL=http://prometheus:9090/api/v1/write \
  -e K6_PROMETHEUS_RW_TREND_STATS="p(50),p(95),p(99),min,max" \
  grafana/k6:latest run --out experimental-prometheus-rw /tests/smoke-test.js
```

> **Note:** On Windows CMD use `%cd%`; on Git Bash/PowerShell use `$(pwd)`.

## Grafana Dashboard

Open **http://localhost:3000** → Dashboards → RedisLab → **k6 — Product API Load Test**

The dashboard is auto-provisioned when the stack starts. It shows results as soon as a test begins pushing metrics.

## Test Files

| File | VUs | Duration | Purpose |
|------|-----|----------|---------|
| `smoke-test.js` | 1 | 30s | Sanity — all endpoints reachable, correct response shape |
| `load-test.js` | 5→20 | ~7m | Realistic traffic mix, validates p95 < 500ms |
| `stress-test.js` | 20→100 | ~9m | Find breaking point, observe cache under pressure |
| `soak-test.js` | 10 | 30m | TTL expiry, memory leak detection, connection stability |
| `cache-behavior-test.js` | 3–10 | ~5m | Cache-aside correctness: cold→warm→invalidate→re-warm→evict |

## Custom Metrics (visible in Grafana)

| Metric | Type | Description |
|--------|------|-------------|
| `k6_cache_hits_total` | Counter | Redis cache hits |
| `k6_cache_misses_total` | Counter | Redis cache misses (DB fallback) |
| `k6_cache_hit_rate` | Rate | Ratio of hits to total reads |
| `k6_cache_evictions_total` | Counter | Explicit `DELETE /cache` calls |
| `k6_view_increments_total` | Counter | Successful `POST /views` calls |
| `k6_product_updates_total` | Counter | Successful `PUT /product` calls |
| `k6_api_errors_total` | Counter | Any non-2xx response |
| `k6_cache_warmup_time_ms` | Trend | DB fetch latency (cold-start reads) |

## SLO Thresholds

Tests report PASS/FAIL against these thresholds:

| Metric | Threshold |
|--------|-----------|
| `http_req_duration` p95 | < 500ms |
| `http_req_duration` p99 | < 1000ms |
| `http_req_failed` | < 1% |
| `cache_hit_rate` (load test) | > 50% |
