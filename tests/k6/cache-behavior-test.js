/**
 * Cache Behavior Test — dedicated test for Redis cache-aside pattern correctness.
 * Validates: cold start miss → warm hit → invalidation miss → re-warm hit → eviction.
 * Uses sequential scenarios to control execution order.
 */
import http from 'k6/http';
import { sleep, check } from 'k6';
import { group } from 'k6';
import { BASE_URL, UPDATED_PRODUCT, CACHE_TRACKED_ENDPOINTS } from './config.js';
import {
  jsonHeaders,
  checkProductResponse,
  checkUpdateResponse,
  checkEvictResponse,
  checkViewsResponse,
  cacheHits,
  cacheMisses,
  cacheWarmupTime,
} from './helpers.js';

export const options = {
  scenarios: {
    // Scenario 1: warm the cache with sequential reads
    cache_warm: {
      executor: 'per-vu-iterations',
      vus: 5,
      iterations: 10,
      startTime: '0s',
      tags: { scenario: 'cache_warm' },
    },
    // Scenario 2: steady reads after warm-up — should be all cache hits
    cache_hit_validation: {
      executor: 'constant-vus',
      vus: 10,
      duration: '1m',
      startTime: '30s',
      tags: { scenario: 'cache_hit_validation' },
    },
    // Scenario 3: mixed read/write to test invalidation
    cache_invalidation: {
      executor: 'constant-vus',
      vus: 5,
      duration: '1m',
      startTime: '2m',
      tags: { scenario: 'cache_invalidation' },
    },
    // Scenario 4: high-frequency evict+read cycles
    cache_eviction_cycle: {
      executor: 'per-vu-iterations',
      vus: 3,
      iterations: 20,
      startTime: '3m30s',
      tags: { scenario: 'cache_eviction_cycle' },
    },
  },
  thresholds: {
    http_req_duration:                       ['p(95)<500'],
    http_req_failed:                         ['rate<0.01'],
    'http_req_duration{scenario:cache_hit_validation}': ['p(95)<100'],   // cache hits must be fast
    cache_hit_rate:                          ['rate>0.6'],
  },
};

// Scenario functions — k6 picks by exported name matching scenario executor default fn
export default function () {
  cacheWarmScenario();
}

export function cache_warm() {
  cacheWarmScenario();
}

export function cache_hit_validation() {
  cacheHitValidationScenario();
}

export function cache_invalidation() {
  cacheInvalidationScenario();
}

export function cache_eviction_cycle() {
  cacheEvictionCycleScenario();
}

// ─── Scenario Implementations ──────────────────────────────────────────────

function cacheWarmScenario() {
  // Cold read all 5 products — first time fetches from DB and populates cache
  group('Cold Start — Warm Cache', () => {
    for (let id = 1; id <= 5; id++) {
      const start = Date.now();
      const res = http.get(`${BASE_URL}/api/product/${id}`);
      const elapsed = Date.now() - start;

      checkProductResponse(res, `CacheWarm/Get/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);

      const body = JSON.parse(res.body);
      if (body.source === 'database') {
        cacheWarmupTime.add(elapsed);
      }
      sleep(0.1);
    }
  });
}

function cacheHitValidationScenario() {
  // All reads should be cache hits — cache was populated in warm scenario
  group('Cache Hit Validation', () => {
    const id = Math.floor(Math.random() * 5) + 1;
    const res = http.get(`${BASE_URL}/api/product/${id}`);
    // endpoint arg records cache hit/miss tagged to GET_BY_ID in Prometheus
    checkProductResponse(res, `CacheHit/Get/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);

    const body = JSON.parse(res.body);
    check(res, {
      'source is cache': () => body.source === 'cache',
    });
  });
  sleep(0.5);
}

function cacheInvalidationScenario() {
  const id = Math.floor(Math.random() * 5) + 1;

  group('Cache Invalidation via Update', () => {
    // 1. Read — should be cache hit
    const before = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(before, `CacheInval/Before/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
    sleep(0.2);

    // 2. Update — invalidates cache
    const payload = JSON.stringify({ ...UPDATED_PRODUCT, id });
    const update = http.put(`${BASE_URL}/api/product/${id}`, payload, { headers: jsonHeaders() });
    checkUpdateResponse(update, id, `CacheInval/Update/${id}`);
    sleep(0.1);

    // 3. Read after update — must be DB source (cache invalidated)
    const after = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(after, `CacheInval/After/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);

    const afterBody = JSON.parse(after.body);
    check(after, {
      'post-update source is database': () => afterBody.source === 'database',
    });
    sleep(0.2);

    // 4. Read again — cache is now re-populated
    const rewarmed = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(rewarmed, `CacheInval/Rewarmed/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);

    const rewarmedBody = JSON.parse(rewarmed.body);
    check(rewarmed, {
      'rewarmed source is cache': () => rewarmedBody.source === 'cache',
    });
  });

  sleep(1);
}

function cacheEvictionCycleScenario() {
  const id = Math.floor(Math.random() * 5) + 1;

  group('Evict + Re-warm Cycle', () => {
    // 1. Ensure product is cached
    http.get(`${BASE_URL}/api/product/${id}`);
    sleep(0.1);

    // 2. Explicit eviction
    const evict = http.del(`${BASE_URL}/api/product/${id}/cache`);
    checkEvictResponse(evict, id, `EvictCycle/Evict/${id}`);

    const evictBody = JSON.parse(evict.body);
    check(evict, {
      'cache was evicted': () => evictBody.cacheEvicted === true,
    });
    sleep(0.1);

    // 3. First read post-eviction → must be DB
    const miss = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(miss, `EvictCycle/Miss/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);

    const missBody = JSON.parse(miss.body);
    check(miss, {
      'post-evict source is database': () => missBody.source === 'database',
    });
    sleep(0.1);

    // 4. Second read → must be cache (re-warmed)
    const hit = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(hit, `EvictCycle/Hit/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);

    const hitBody = JSON.parse(hit.body);
    check(hit, {
      'post-evict re-read source is cache': () => hitBody.source === 'cache',
    });

    // 5. Verify view counter still works after eviction cycle
    const views = http.post(`${BASE_URL}/api/product/${id}/views`);
    checkViewsResponse(views, id, `EvictCycle/Views/${id}`);
  });

  sleep(1);
}
