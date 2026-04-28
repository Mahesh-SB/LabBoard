/**
 * Soak / Endurance Test — 10 VUs for 30 minutes.
 * Cache metrics tagged per endpoint so you can correlate TTL expiry
 * to hit rate drops on specific endpoints over time.
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
    { duration: '2m',  target: 10 },
    { duration: '26m', target: 10 },
    { duration: '2m',  target: 0  },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    http_req_failed:   ['rate<0.01'],
  },
};

export default function () {
  const id = randomProductId();

  group('GET /api/product/:id (TTL aware)', () => {
    const res = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(res, `Soak/Get/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
    sleep(1);

    const res2 = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(res2, `Soak/Get/${id}/Retry`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
  });

  sleep(1);

  if (Math.random() < 0.20) {
    group('GET /api/product', () => {
      const res = http.get(`${BASE_URL}/api/product`);
      checkAllProductsResponse(res, 'Soak/GetAll', CACHE_TRACKED_ENDPOINTS.GET_ALL);
      sleep(1);
    });
  }

  if (Math.random() < 0.10) {
    group('POST /api/product/:id/views', () => {
      const res = http.post(`${BASE_URL}/api/product/${id}/views`);
      checkViewsResponse(res, id, `Soak/Views/${id}`);
      sleep(0.5);
    });
  }

  if (Math.random() < 0.08) {
    group('PUT /api/product/:id + re-read', () => {
      const payload = JSON.stringify({ ...UPDATED_PRODUCT, id });
      const updateRes = http.put(`${BASE_URL}/api/product/${id}`, payload, { headers: jsonHeaders() });
      checkUpdateResponse(updateRes, id, `Soak/Update/${id}`);
      sleep(0.3);

      // Post-update: cache invalidated → DB miss → re-populate
      const getRes = http.get(`${BASE_URL}/api/product/${id}`);
      checkProductResponse(getRes, `Soak/Get/${id}/PostUpdate`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
    });
  }

  if (Math.random() < 0.05) {
    group('DELETE /api/product/:id/cache + re-warm', () => {
      const evictRes = http.del(`${BASE_URL}/api/product/${id}/cache`);
      checkEvictResponse(evictRes, id, `Soak/Evict/${id}`);
      sleep(0.2);

      const warmRes = http.get(`${BASE_URL}/api/product/${id}`);
      checkProductResponse(warmRes, `Soak/Warm/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
    });
  }

  sleep(2);
}
