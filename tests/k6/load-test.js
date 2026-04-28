/**
 * Load Test — ramp to 20 VUs over 1 min, hold 3 min, ramp down 1 min
 * Simulates realistic production traffic across all Product API endpoints.
 * Cache metrics are tagged per endpoint — visible in Grafana by endpoint filter.
 */
import http from 'k6/http';
import { sleep, group } from 'k6';
import { BASE_URL, UPDATED_PRODUCT, CACHE_TRACKED_ENDPOINTS } from './config.js';
import {
  randomProductId,
  jsonHeaders,
  checkProductResponse,
  checkAllProductsResponse,
  checkUpdateResponse,
  checkEvictResponse,
  checkViewsResponse,
} from './helpers.js';

export const options = {
  stages: [
    { duration: '1m',  target: 5  },
    { duration: '2m',  target: 20 },
    { duration: '3m',  target: 20 },
    { duration: '1m',  target: 0  },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    http_req_failed:   ['rate<0.01'],
    http_req_waiting:  ['p(95)<400'],
    cache_hit_rate:    ['rate>0.5'],
  },
};

export default function () {
  const id = randomProductId();

  // 60% of traffic — single product reads (primary cache-aside path)
  group('GET /api/product/:id', () => {
    const res = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(res, `Load/Get/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
    sleep(0.3);

    // Second read → should be a cache hit
    const res2 = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(res2, `Load/Get/${id}/Cached`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
    sleep(0.3);
  });

  // 20% — get all products (each item individually cached)
  if (Math.random() < 0.20) {
    group('GET /api/product', () => {
      const res = http.get(`${BASE_URL}/api/product`);
      checkAllProductsResponse(res, 'Load/GetAll', CACHE_TRACKED_ENDPOINTS.GET_ALL);
      sleep(0.5);
    });
  }

  // 10% — view increments (no cache source field — not tracked)
  if (Math.random() < 0.10) {
    group('POST /api/product/:id/views', () => {
      const res = http.post(`${BASE_URL}/api/product/${id}/views`);
      checkViewsResponse(res, id, `Load/Views/${id}`);
      sleep(0.2);
    });
  }

  // 7% — update product (invalidates cache — no cache source field — not tracked)
  if (Math.random() < 0.07) {
    group('PUT /api/product/:id', () => {
      const payload = JSON.stringify({ ...UPDATED_PRODUCT, id });
      const res = http.put(`${BASE_URL}/api/product/${id}`, payload, { headers: jsonHeaders() });
      checkUpdateResponse(res, id, `Load/Update/${id}`);
      sleep(0.5);
    });
  }

  // 3% — explicit cache eviction (no cache source field — not tracked)
  if (Math.random() < 0.03) {
    group('DELETE /api/product/:id/cache', () => {
      const res = http.del(`${BASE_URL}/api/product/${id}/cache`);
      checkEvictResponse(res, id, `Load/Evict/${id}`);
      sleep(0.2);
    });
  }

  sleep(Math.random() * 1 + 0.5);
}
