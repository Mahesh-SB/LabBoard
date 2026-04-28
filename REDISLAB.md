# RedisLab — Redis + Observability Practice Solution

A two-project .NET 8 solution for hands-on Redis and observability practice using
Prometheus and Grafana, all wired together with Docker Compose.

---

## Table of Contents

1. [Solution Structure](#solution-structure)
2. [Projects](#projects)
   - [RedisLab.Api](#redislabapi)
   - [RedisLab.Observability](#redislabobservability)
3. [Config Folder Deep-Dive](#config-folder-deep-dive)
   - [Folder Layout](#folder-layout)
   - [prometheus/prometheus.yml](#prometheusprometheusynml)
   - [grafana/provisioning/datasources/datasource.yml](#grafanaprovisioningdatasourcesdatasourceyml)
   - [grafana/provisioning/dashboards/dashboards.yml](#grafanaprovisioningdashboardsdashboardsyml)
   - [grafana/dashboards/redis-api.json](#grafanadashboardsredis-apijson)
4. [Infrastructure](#infrastructure)
   - [Redis](#redis)
   - [Prometheus](#prometheus)
   - [Grafana](#grafana)
5. [NuGet Packages](#nuget-packages)
6. [Prometheus Metrics Reference](#prometheus-metrics-reference)
7. [Grafana Dashboard Panels](#grafana-dashboard-panels)
8. [API Endpoints Reference](#api-endpoints-reference)
9. [How to Run](#how-to-run)
10. [Practice Scenarios](#practice-scenarios)
11. [Architecture Diagram](#architecture-diagram)

---

## Solution Structure

```
RedisLab/
├── RedisLab.sln
├── docker-compose.yml
│
├── src/
│   ├── RedisLab.Api/
│   │   ├── Controllers/
│   │   │   ├── CacheController.cs
│   │   │   └── ProductController.cs
│   │   ├── Services/
│   │   │   ├── IRedisCacheService.cs
│   │   │   └── RedisCacheService.cs
│   │   ├── Models/
│   │   │   └── Product.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Dockerfile
│   │   └── RedisLab.Api.csproj
│   │
│   └── RedisLab.Observability/
│       ├── Collectors/
│       │   └── RedisInfoCollector.cs
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Dockerfile
│       └── RedisLab.Observability.csproj
│
└── config/
    ├── prometheus/
    │   └── prometheus.yml
    └── grafana/
        ├── provisioning/
        │   ├── datasources/
        │   │   └── datasource.yml
        │   └── dashboards/
        │       └── dashboards.yml
        └── dashboards/
            └── redis-api.json
```

---

## Config Folder Deep-Dive

Everything under `config/` is **mounted read-only** into the Docker containers at
startup. No code changes are needed to modify scrape intervals, add dashboards, or
point Grafana at a different datasource — just edit these files and restart the
relevant container.

### Folder Layout

```
config/
│
├── prometheus/
│   └── prometheus.yml              ← Prometheus scrape configuration
│
└── grafana/
    ├── provisioning/               ← Grafana reads this on startup (auto-wiring)
    │   ├── datasources/
    │   │   └── datasource.yml      ← Declares Prometheus as the default datasource
    │   └── dashboards/
    │       └── dashboards.yml      ← Tells Grafana where to load dashboard JSON files
    └── dashboards/
        └── redis-api.json          ← The actual 10-panel dashboard definition
```

> **Why provisioning?** Without provisioning you would have to manually click
> through the Grafana UI to add a datasource and import a dashboard every time
> you do `docker compose down -v`. With these files Grafana bootstraps itself
> automatically on every fresh start.

---

### `prometheus/prometheus.yml`

**Mounted at:** `/etc/prometheus/prometheus.yml` inside the `prometheus` container.

```yaml
global:
  scrape_interval: 15s       # default — how often to pull metrics from every target
  evaluation_interval: 15s   # how often to evaluate alerting/recording rules

scrape_configs:

  - job_name: 'redis-api'
    static_configs:
      - targets: ['redis-api:8080']   # Docker service name resolves via DNS
    metrics_path: '/metrics'          # endpoint exposed by prometheus-net
    scrape_interval: 10s              # overrides global — faster for the API

  - job_name: 'redis-observability'
    static_configs:
      - targets: ['redis-observability:8080']
    metrics_path: '/metrics'
    scrape_interval: 15s              # matches Redis INFO poll interval

  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']   # Prometheus scraping itself
```

**Key concepts:**

| Field | Meaning |
|---|---|
| `global.scrape_interval` | Fallback interval used by any job that does not set its own |
| `global.evaluation_interval` | How often alert rules are re-evaluated (no rules yet, but the field is required) |
| `job_name` | Becomes the `job` label on every metric from that target — used in Grafana queries as `{job="redis-api"}` |
| `static_configs.targets` | Host:port of the `/metrics` endpoint. Inside Docker Compose, service names act as DNS hostnames |
| `metrics_path` | The HTTP path Prometheus GETs. prometheus-net defaults to `/metrics` |
| `scrape_interval` per job | Overrides the global interval for that specific target only |

**To add a new scrape target** (e.g., a third .NET service on port 5002):
```yaml
  - job_name: 'redis-worker'
    static_configs:
      - targets: ['redis-worker:8080']
    metrics_path: '/metrics'
    scrape_interval: 30s
```

---

### `grafana/provisioning/datasources/datasource.yml`

**Mounted at:** `/etc/grafana/provisioning/datasources/` inside the `grafana` container.

Grafana reads every `.yml` file in this directory at startup and registers the
datasources automatically — no manual UI steps needed.

```yaml
apiVersion: 1

datasources:
  - name: Prometheus          # display name shown in Grafana UI
    type: prometheus          # plugin type
    uid: prometheus           # stable ID used inside dashboard JSON (targets reference this)
    access: proxy             # Grafana server proxies requests to Prometheus (not the browser)
    url: http://prometheus:9090   # Docker service name
    isDefault: true           # pre-selected when you create a new panel
    editable: false           # prevents accidental changes through the UI
```

**Key fields explained:**

| Field | Why it matters |
|---|---|
| `uid: prometheus` | Must match exactly what is written in `redis-api.json` panel targets (`"uid": "prometheus"`). If they differ, all panels show "datasource not found" |
| `access: proxy` | The Grafana backend contacts Prometheus. Use `direct` only if the browser has network access to Prometheus (not the case inside Docker) |
| `isDefault: true` | New panels automatically use this datasource — saves manual selection |
| `editable: false` | Provisioned datasources are managed by code; disabling edits prevents config drift |

---

### `grafana/provisioning/dashboards/dashboards.yml`

**Mounted at:** `/etc/grafana/provisioning/dashboards/` inside the `grafana` container.

This file is a **provider** — it tells Grafana *where* to look for dashboard JSON
files, not what those dashboards contain.

```yaml
apiVersion: 1

providers:
  - name: RedisLab              # logical name for this provider (any string)
    orgId: 1                    # Grafana organisation ID (default org = 1)
    folder: RedisLab            # UI folder dashboards are grouped under
    type: file                  # load dashboards from the local filesystem
    disableDeletion: false      # allow deleting dashboards via UI (they will reappear on restart)
    updateIntervalSeconds: 10   # re-scan the folder every 10 s for changed JSON files
    options:
      path: /var/lib/grafana/dashboards   # container path where JSON files live
```

**Key fields explained:**

| Field | Why it matters |
|---|---|
| `folder` | Creates a folder called **RedisLab** in the Grafana UI — keeps dashboards organised |
| `type: file` | Instructs Grafana to read `.json` files from `options.path` |
| `updateIntervalSeconds: 10` | Hot-reload — edit `redis-api.json` on the host and Grafana picks it up within 10 s without a restart |
| `options.path` | Must match the volume mount target in `docker-compose.yml`: `./config/grafana/dashboards:/var/lib/grafana/dashboards` |

---

### `grafana/dashboards/redis-api.json`

**Mounted at:** `/var/lib/grafana/dashboards/redis-api.json` inside the `grafana` container.

This is the full Grafana dashboard definition. Grafana uses **schemaVersion 38**
(Grafana 10+). The file has four top-level sections:

```
redis-api.json
├── metadata        (title, uid, tags, refresh, time range)
├── templating      (variables — none currently, easy to add)
├── annotations     (event overlays — none currently)
└── panels[]        (10 visualisation panels)
```

#### Dashboard Metadata

| Field | Value | Meaning |
|---|---|---|
| `title` | `Redis Lab Dashboard` | Shown in the Grafana UI |
| `uid` | `redislab` | Stable URL: `http://localhost:3000/d/redislab` |
| `schemaVersion` | `38` | Grafana 10+ panel format |
| `refresh` | `5s` | Auto-refresh every 5 seconds |
| `time.from` | `now-15m` | Default time window is last 15 minutes |
| `tags` | `["redis","dotnet"]` | Searchable labels in the dashboard browser |

#### Grid Layout System

Grafana uses a **24-column grid**. Each panel's position is defined by `gridPos`:

```json
"gridPos": { "h": 8, "w": 12, "x": 0, "y": 0 }
```

| Property | Meaning |
|---|---|
| `w` | Width in grid columns (max 24). `12` = half the screen |
| `h` | Height in grid rows. `8` ≈ a standard chart height |
| `x` | Horizontal offset (0 = left edge, 12 = right half) |
| `y` | Vertical offset in rows — panels stack downward |

#### Panel Breakdown — all 10 panels

**Row 1 (y=0) — HTTP layer**

| ID | Title | Type | gridPos (w×h @ x,y) | PromQL |
|---|---|---|---|---|
| 1 | HTTP Request Rate | timeseries | 12×8 @ 0,0 | `sum(rate(http_requests_received_total{job="redis-api"}[1m])) by (method, handler, code)` |
| 2 | HTTP Request Duration P95/P99 | timeseries | 12×8 @ 12,0 | `histogram_quantile(0.95/0.99, rate(http_request_duration_seconds_bucket[5m]))` |

**Row 2 (y=8) — Cache layer**

| ID | Title | Type | gridPos (w×h @ x,y) | PromQL |
|---|---|---|---|---|
| 3 | Cache Hit / Miss Rate | timeseries | 12×8 @ 0,8 | `rate(redislab_cache_hits_total[1m])` + misses |
| 4 | Cache Hit Ratio % | gauge | 12×8 @ 12,8 | `100 * hits / (hits + misses)` over 5m |

**Row 3 (y=16) — Redis memory + client stats**

| ID | Title | Type | gridPos (w×h @ x,y) | PromQL |
|---|---|---|---|---|
| 5 | Redis Used Memory | timeseries | 12×8 @ 0,16 | `used_memory_bytes` + `used_memory_rss_bytes` |
| 6 | Redis Connected Clients | stat | 6×4 @ 12,16 | `redislab_redis_connected_clients` |
| 7 | Redis Blocked Clients | stat | 6×4 @ 18,16 | `redislab_redis_blocked_clients` |

**Row 4 (y=20/24) — Redis server internals**

| ID | Title | Type | gridPos (w×h @ x,y) | PromQL |
|---|---|---|---|---|
| 8 | Keyspace Hits vs Misses | timeseries | 12×8 @ 12,20 | `rate(keyspace_hits_total[1m])` + misses |
| 9 | Commands Processed Rate | timeseries | 12×8 @ 0,24 | `rate(commands_processed_total[1m])` |
| 10 | Redis Op Duration P50/P99 | timeseries | 12×8 @ 12,24 | `histogram_quantile(0.99/0.50, redis_op_duration_seconds_bucket)` |

#### Threshold Colours (panels 4, 6, 7)

| Panel | Green | Yellow | Red |
|---|---|---|---|
| Cache Hit Ratio | > 80% | 50–80% | < 50% |
| Connected Clients | 0–49 | 50–99 | ≥ 100 |
| Blocked Clients | 0 | — | ≥ 1 |

#### How to add a new panel

1. Edit `config/grafana/dashboards/redis-api.json`
2. Add a new object to the `panels` array with a unique `id` and a `gridPos` that
   doesn't overlap existing panels (increment `y` past the last row)
3. Grafana hot-reloads the file within 10 seconds — no restart needed

Example skeleton for a new stat panel:
```json
{
  "id": 11,
  "title": "Redis Uptime (seconds)",
  "type": "stat",
  "datasource": { "type": "prometheus", "uid": "prometheus" },
  "gridPos": { "h": 4, "w": 6, "x": 0, "y": 32 },
  "targets": [
    {
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "expr": "redislab_redis_uptime_seconds{job=\"redis-observability\"}",
      "legendFormat": "Uptime",
      "refId": "A"
    }
  ],
  "fieldConfig": {
    "defaults": { "unit": "s" },
    "overrides": []
  }
}
```

---

## Projects

### RedisLab.Api

**Purpose:** A .NET 8 Web API that demonstrates all common Redis data-type operations
and instruments every operation with Prometheus metrics.

**Port:** `5000` (host) → `8080` (container)

**Key files:**

| File | Responsibility |
|---|---|
| `Program.cs` | Registers Redis, health checks, Swagger, Prometheus middleware |
| `Services/IRedisCacheService.cs` | Interface — string, hash, list, counter, TTL ops |
| `Services/RedisCacheService.cs` | Implementation wrapping StackExchange.Redis + Prometheus instrumentation |
| `Controllers/CacheController.cs` | REST endpoints to exercise all Redis data types directly |
| `Controllers/ProductController.cs` | Real-world cache-aside pattern with a fake product database |
| `Models/Product.cs` | Simple `record Product(int Id, string Name, decimal Price, int Stock)` |

#### RedisCacheService — operations implemented

| Redis Command | Method | Notes |
|---|---|---|
| `GET` | `GetStringAsync(key)` | Records hit/miss metric |
| `SET` / `SETEX` | `SetStringAsync(key, value, ttl?)` | TTL optional |
| `DEL` | `DeleteAsync(key)` | Returns bool |
| `EXISTS` | `ExistsAsync(key)` | Returns bool |
| `INCR` | `IncrementAsync(key, by)` | Default step = 1 |
| `HSET` | `SetHashFieldAsync(key, field, value)` | |
| `HGET` | `GetHashFieldAsync(key, field)` | Records hit/miss |
| `HGETALL` | `GetAllHashFieldsAsync(key)` | Returns `Dictionary<string,string>` |
| `LPUSH` | `PushToListAsync(key, value)` | Pushes to list head |
| `RPOP` | `PopFromListAsync(key)` | Pops from list tail |
| `LRANGE` | `GetListRangeAsync(key, start, stop)` | Default full range |
| `TTL` | `GetTtlAsync(key)` | Returns `TimeSpan?` |

#### ProductController — cache-aside pattern

```
GET /api/product/{id}
  1. Check Redis cache  →  Hit: return with source="cache"
  2. Miss → query FakeDb → write to Redis (TTL 2 min) → return with source="database"

PUT /api/product/{id}
  1. Update FakeDb
  2. DEL cache key  (cache invalidation)

POST /api/product/{id}/views
  INCR product:views:{id}  (counter with no TTL)
```

---

### RedisLab.Observability

**Purpose:** A .NET 8 background-service app that acts as a Redis metrics exporter.
It calls `Redis INFO` every 10 seconds and publishes server-level stats as
Prometheus gauges and counters so Grafana can visualize Redis health independently
from the API.

**Port:** `5001` (host) → `8080` (container)

**Key files:**

| File | Responsibility |
|---|---|
| `Program.cs` | Minimal host — registers Redis, background service, `/metrics`, `/health` |
| `Collectors/RedisInfoCollector.cs` | `BackgroundService` — polls `Redis INFO`, updates Prometheus metrics |

#### RedisInfoCollector — Redis INFO sections parsed

| INFO Section | Field | Prometheus Metric |
|---|---|---|
| `# clients` | `connected_clients` | `redislab_redis_connected_clients` (Gauge) |
| `# clients` | `blocked_clients` | `redislab_redis_blocked_clients` (Gauge) |
| `# memory` | `used_memory` | `redislab_redis_used_memory_bytes` (Gauge) |
| `# memory` | `used_memory_rss` | `redislab_redis_used_memory_rss_bytes` (Gauge) |
| `# stats` | `total_commands_processed` | `redislab_redis_commands_processed_total` (Counter) |
| `# stats` | `keyspace_hits` | `redislab_redis_keyspace_hits_total` (Counter) |
| `# stats` | `keyspace_misses` | `redislab_redis_keyspace_misses_total` (Counter) |
| `# stats` | `total_connections_received` | `redislab_redis_connections_received_total` (Counter) |
| `# stats` | `rejected_connections` | `redislab_redis_rejected_connections_total` (Counter) |
| `# server` | `uptime_in_seconds` | `redislab_redis_uptime_seconds` (Gauge) |

> **Counter delta logic:** Redis INFO always returns cumulative totals. The collector
> tracks the previous value and calls `counter.Inc(current - previous)` so Prometheus
> counters only ever increase.

---

## Infrastructure

### Redis

| Property | Value |
|---|---|
| Image | `redis:7-alpine` |
| Host port | `6379` |
| Persistence | `BGSAVE` every 60s if ≥1 key changed |
| Network | `redislab-net` (bridge) |

### Prometheus

| Property | Value |
|---|---|
| Image | `prom/prometheus:latest` |
| Host port | `9090` |
| Config | `config/prometheus/prometheus.yml` |
| Scrape interval | 15s global, 10s for redis-api |

**Scrape targets:**

```yaml
- job_name: 'redis-api'          # targets: redis-api:8080/metrics
- job_name: 'redis-observability' # targets: redis-observability:8080/metrics
- job_name: 'prometheus'          # self-scrape
```

### Grafana

| Property | Value |
|---|---|
| Image | `grafana/grafana:latest` |
| Host port | `3000` |
| Credentials | `admin` / `admin` |
| Datasource | Auto-provisioned Prometheus at `http://prometheus:9090` |
| Dashboard | Auto-provisioned from `config/grafana/dashboards/redis-api.json` |

---

## NuGet Packages

### RedisLab.Api

| Package | Version | Purpose |
|---|---|---|
| `StackExchange.Redis` | 2.8.16 | Redis client |
| `prometheus-net.AspNetCore` | 8.2.1 | HTTP metrics middleware + `/metrics` endpoint |
| `AspNetCore.HealthChecks.Redis` | 8.0.1 | Redis health check at `/health` |
| `Swashbuckle.AspNetCore` | 6.9.0 | Swagger UI at `/swagger` |

### RedisLab.Observability

| Package | Version | Purpose |
|---|---|---|
| `StackExchange.Redis` | 2.8.16 | Redis client (to call INFO) |
| `prometheus-net.AspNetCore` | 8.2.1 | Prometheus metrics + `/metrics` endpoint |

---

## Prometheus Metrics Reference

### From RedisLab.Api (`job="redis-api"`)

| Metric | Type | Labels | Description |
|---|---|---|---|
| `http_requests_received_total` | Counter | `method`, `handler`, `code` | Auto — every HTTP request |
| `http_request_duration_seconds` | Histogram | `method`, `handler`, `code` | Auto — HTTP latency |
| `http_requests_in_progress` | Gauge | `method`, `handler` | Auto — concurrent requests |
| `redislab_cache_hits_total` | Counter | `operation` | Key found in Redis |
| `redislab_cache_misses_total` | Counter | `operation` | Key not found in Redis |
| `redislab_redis_op_duration_seconds` | Histogram | `operation` | Per-command Redis latency |

`operation` label values: `GET`, `SET`, `DEL`, `EXISTS`, `INCR`, `HSET`, `HGET`, `HGETALL`, `LPUSH`, `RPOP`, `LRANGE`, `TTL`

### From RedisLab.Observability (`job="redis-observability"`)

| Metric | Type | Description |
|---|---|---|
| `redislab_redis_connected_clients` | Gauge | Active client connections |
| `redislab_redis_blocked_clients` | Gauge | Clients waiting on blocking commands |
| `redislab_redis_used_memory_bytes` | Gauge | Bytes allocated by Redis allocator |
| `redislab_redis_used_memory_rss_bytes` | Gauge | OS-visible RSS memory |
| `redislab_redis_commands_processed_total` | Counter | Cumulative commands executed |
| `redislab_redis_keyspace_hits_total` | Counter | Successful key lookups |
| `redislab_redis_keyspace_misses_total` | Counter | Failed key lookups |
| `redislab_redis_connections_received_total` | Counter | Total client connections accepted |
| `redislab_redis_rejected_connections_total` | Counter | Connections rejected (maxclients) |
| `redislab_redis_uptime_seconds` | Gauge | Seconds since Redis started |

---

## Grafana Dashboard Panels

Dashboard UID: `redislab` — auto-loaded at startup.

| Panel | Type | Query summary |
|---|---|---|
| HTTP Request Rate | Timeseries | `rate(http_requests_received_total[1m])` by method/route/code |
| HTTP Duration P95/P99 | Timeseries | `histogram_quantile(0.95/0.99, ...)` by handler |
| Cache Hit / Miss Rate | Timeseries | `rate(redislab_cache_hits_total[1m])` vs misses |
| Cache Hit Ratio % | Gauge | `hits / (hits + misses) * 100` — green >80%, yellow >50%, red <50% |
| Redis Used Memory | Timeseries | `used_memory_bytes` and `used_memory_rss_bytes` |
| Connected Clients | Stat | `redislab_redis_connected_clients` |
| Blocked Clients | Stat | `redislab_redis_blocked_clients` — red when > 0 |
| Keyspace Hits vs Misses | Timeseries | `rate(keyspace_hits_total[1m])` vs misses |
| Commands Processed Rate | Timeseries | `rate(commands_processed_total[1m])` |
| Redis Op Duration P50/P99 | Timeseries | `histogram_quantile(0.50/0.99, redislab_redis_op_duration_seconds_bucket)` |

---

## API Endpoints Reference

### CacheController — `GET /api/cache/...`

#### String operations

```
GET    /api/cache/string/{key}               Get a string value
POST   /api/cache/string/{key}?ttlSeconds=N  Set a string value (body: "value")
DELETE /api/cache/{key}                      Delete a key
GET    /api/cache/{key}/exists               Check if key exists
GET    /api/cache/{key}/ttl                  Get remaining TTL in seconds
```

#### Counter

```
POST   /api/cache/counter/{key}/incr?by=N    Increment counter (default by=1)
```

#### Hash

```
POST   /api/cache/hash/{key}/{field}         HSET — set one hash field (body: "value")
GET    /api/cache/hash/{key}/{field}         HGET — get one hash field
GET    /api/cache/hash/{key}                 HGETALL — get all fields as object
```

#### List

```
POST   /api/cache/list/{key}                 LPUSH — push to head (body: "value")
DELETE /api/cache/list/{key}/pop             RPOP — pop from tail
GET    /api/cache/list/{key}                 LRANGE 0 -1 — all items
```

### ProductController — `GET /api/product/...`

```
GET    /api/product          List all products (shows cache source per item)
GET    /api/product/{id}     Get product — cache-aside (shows source: cache/database)
PUT    /api/product/{id}     Update product + invalidate cache (body: Product JSON)
DELETE /api/product/{id}/cache   Evict from cache only
POST   /api/product/{id}/views   Increment view counter (INCR with no TTL)
```

**Example response — cache miss then hit:**
```json
// First request
{ "source": "database", "product": { "id": 1, "name": "Keyboard", "price": 79.99, "stock": 150 } }

// Second request (within 2 min TTL)
{ "source": "cache", "product": { "id": 1, "name": "Keyboard", "price": 79.99, "stock": 150 } }
```

---

## How to Run

### Prerequisites

- Docker Desktop running

### Start everything

```bash
cd "C:/D Drive/Project/BackEND/RedisLab"
docker compose up --build
```

### Access the services

| Service | URL | Notes |
|---|---|---|
| API Swagger | http://localhost:5000/swagger | Try all endpoints |
| API metrics | http://localhost:5000/metrics | Raw Prometheus text |
| Observability | http://localhost:5001/metrics | Redis server metrics |
| Prometheus | http://localhost:9090 | Query metrics, check targets |
| Grafana | http://localhost:3000 | Login: admin / admin |

### Verify Prometheus targets

Open http://localhost:9090/targets — all three targets should show **UP**.

### Open the dashboard in Grafana

1. Login → Dashboards → Browse → **RedisLab** folder → **Redis Lab Dashboard**

### Stop everything

```bash
docker compose down          # keep volumes
docker compose down -v       # also delete grafana data volume
```

### Run locally (without Docker)

Start Redis first:
```bash
docker run -p 6379:6379 redis:7-alpine
```

Then run each project:
```bash
# Terminal 1
cd src/RedisLab.Api
dotnet run

# Terminal 2
cd src/RedisLab.Observability
dotnet run
```

---

## Practice Scenarios

### Scenario 1 — Observe cache hit/miss in Grafana

1. Open Grafana → Redis Lab Dashboard
2. Call `GET /api/product/1` several times with a cold cache
3. Watch **Cache Hit / Miss Rate** panel flip from misses to hits
4. Call `PUT /api/product/1` to invalidate — next GET becomes a miss again

### Scenario 2 — String SET with TTL

```http
POST /api/cache/string/session:abc?ttlSeconds=30
Body: "user-data-here"

GET /api/cache/string/session:abc/ttl   ← watch TTL count down
```

After 30 seconds the key disappears automatically.

### Scenario 3 — Hash as a user profile

```http
POST /api/cache/hash/user:42/name        "Alice"
POST /api/cache/hash/user:42/email       "alice@example.com"
POST /api/cache/hash/user:42/role        "admin"
GET  /api/cache/hash/user:42             ← returns all three fields
```

### Scenario 4 — List as a queue

```http
POST /api/cache/list/jobs     "job-1"
POST /api/cache/list/jobs     "job-2"
POST /api/cache/list/jobs     "job-3"
GET  /api/cache/list/jobs               ← ["job-3","job-2","job-1"] (LPUSH order)
DELETE /api/cache/list/jobs/pop         ← pops "job-1" (RPOP = FIFO)
```

### Scenario 5 — Counter (INCR)

```http
POST /api/cache/counter/page:home/incr?by=1   ← returns 1
POST /api/cache/counter/page:home/incr?by=1   ← returns 2
POST /api/cache/counter/page:home/incr?by=10  ← returns 12
GET  /api/cache/string/page:home               ← "12"
```

### Scenario 6 — Redis memory in Grafana

1. Push thousands of keys via the API
2. Watch **Redis Used Memory** panel in Grafana rise in real time
3. Delete keys — watch memory drop

### Scenario 7 — Prometheus query practice

Open http://localhost:9090 and try:

```promql
# Cache hit ratio
rate(redislab_cache_hits_total[1m])
  /
(rate(redislab_cache_hits_total[1m]) + rate(redislab_cache_misses_total[1m]))

# P99 Redis op latency
histogram_quantile(0.99,
  rate(redislab_redis_op_duration_seconds_bucket[5m])
)

# HTTP error rate
rate(http_requests_received_total{code=~"5.."}[1m])
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Docker network: redislab-net             │
│                                                                 │
│  ┌──────────────────────┐      ┌──────────────────────────┐     │
│  │   RedisLab.Api       │      │  RedisLab.Observability  │     │
│  │   :5000 / :8080      │      │  :5001 / :8080           │     │
│  │                      │      │                          │     │
│  │  CacheController     │      │  RedisInfoCollector      │     │
│  │  ProductController   │ ───► │  (BackgroundService)     │     │
│  │  RedisCacheService   │      │  polls Redis INFO /10s   │     │
│  │                      │      │                          │     │
│  │  /metrics ◄──────────┼──┐   │  /metrics ◄─────────────┼──┐  │
│  └──────────┬───────────┘  │   └────────────┬────────────┘  │  │
│             │               │                │               │  │
│             ▼               │                ▼               │  │
│  ┌──────────────────────┐   │   ┌────────────────────────┐  │  │
│  │       Redis 7        │◄──┘   │      Prometheus         │◄─┘  │
│  │       :6379          │       │      :9090              │     │
│  └──────────────────────┘       └──────────┬─────────────┘     │
│                                             │                   │
│                                             ▼                   │
│                                  ┌─────────────────────┐        │
│                                  │       Grafana        │        │
│                                  │       :3000          │        │
│                                  │  10-panel dashboard  │        │
│                                  └─────────────────────┘        │
└─────────────────────────────────────────────────────────────────┘

Host machine:
  http://localhost:5000  → API + Swagger
  http://localhost:5001  → Observability status
  http://localhost:9090  → Prometheus
  http://localhost:3000  → Grafana (admin/admin)
  localhost:6379         → Redis (direct CLI access)
```

---

## Metric Flow Summary

```
API call arrives
    │
    ▼
UseHttpMetrics()  →  http_requests_received_total
                      http_request_duration_seconds
    │
    ▼
RedisCacheService.GetStringAsync()
    │
    ├── Redis HIT  →  redislab_cache_hits_total{operation="GET"}.Inc()
    └── Redis MISS →  redislab_cache_misses_total{operation="GET"}.Inc()
                       redislab_redis_op_duration_seconds{operation="GET"}.Observe()
    │
    ▼
/metrics endpoint  ←  Prometheus scrapes every 10s
    │
    ▼
Grafana queries Prometheus  →  Dashboard panels update every 5s
```
