/**
 * Smoke Test — 1 VU, 30s
 * Validates all endpoints are reachable and returning correct shapes.
 * Run before any load test to confirm the API is healthy.
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
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_duration: ['p(95)<1000'],
    http_req_failed: ['rate<0.05'],
  },
};

export default function () {
  const id = randomProductId();

  group('GET all products', () => {
    const res = http.get(`${BASE_URL}/api/product`);
    checkAllProductsResponse(res, 'Smoke/GetAll', CACHE_TRACKED_ENDPOINTS.GET_ALL);
  });

  sleep(0.5);

  group('GET single product', () => {
    const res = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(res, `Smoke/Get/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
  });

  sleep(0.5);

  group('GET same product again (expect cache hit)', () => {
    const res = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(res, `Smoke/Get/${id}/CacheHit`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
  });

  sleep(0.5);

  group('POST increment views', () => {
    const res = http.post(`${BASE_URL}/api/product/${id}/views`);
    checkViewsResponse(res, id, `Smoke/Views/${id}`);
    // No cache tracking — POST /views writes to Redis but has no cache-aside source field
  });

  sleep(0.5);

  group('PUT update product', () => {
    const payload = JSON.stringify({ ...UPDATED_PRODUCT, id });
    const res = http.put(`${BASE_URL}/api/product/${id}`, payload, { headers: jsonHeaders() });
    checkUpdateResponse(res, id, `Smoke/Update/${id}`);
    // No cache tracking — PUT invalidates cache, does not read it
  });

  sleep(0.5);

  group('DELETE evict cache', () => {
    const res = http.del(`${BASE_URL}/api/product/${id}/cache`);
    checkEvictResponse(res, id, `Smoke/Evict/${id}`);
    // No cache tracking — DELETE removes from cache, no source field
  });

  sleep(1);
}
