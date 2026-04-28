/**
 * Stress Test — ramp to 100 VUs to find the breaking point.
 * Cache metrics tagged per endpoint so you can see which endpoint's
 * hit rate degrades first under stress.
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
    { duration: '2m',  target: 20  },
    { duration: '2m',  target: 50  },
    { duration: '2m',  target: 100 },
    { duration: '2m',  target: 50  },
    { duration: '1m',  target: 0   },
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000', 'p(99)<5000'],
    http_req_failed:   ['rate<0.05'],
    cache_hit_rate:    ['rate>0.3'],
  },
};

export default function () {
  const id = randomProductId();

  group('GET /api/product/:id', () => {
    const res = http.get(`${BASE_URL}/api/product/${id}`);
    checkProductResponse(res, `Stress/Get/${id}`, CACHE_TRACKED_ENDPOINTS.GET_BY_ID);
  });

  sleep(0.1);

  if (Math.random() < 0.15) {
    group('GET /api/product', () => {
      const res = http.get(`${BASE_URL}/api/product`);
      checkAllProductsResponse(res, 'Stress/GetAll', CACHE_TRACKED_ENDPOINTS.GET_ALL);
    });
  }

  if (Math.random() < 0.10) {
    group('POST /api/product/:id/views', () => {
      const res = http.post(`${BASE_URL}/api/product/${id}/views`);
      checkViewsResponse(res, id, `Stress/Views/${id}`);
    });
  }

  if (Math.random() < 0.05) {
    group('PUT /api/product/:id', () => {
      const payload = JSON.stringify({ ...UPDATED_PRODUCT, id });
      const res = http.put(`${BASE_URL}/api/product/${id}`, payload, { headers: jsonHeaders() });
      checkUpdateResponse(res, id, `Stress/Update/${id}`);
    });
  }

  sleep(Math.random() * 0.5);
}
