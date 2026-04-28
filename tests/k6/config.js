export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const PRODUCT_IDS = [1, 2, 3, 4, 5];

export const THRESHOLDS = {
  http_req_duration: ['p(95)<500', 'p(99)<1000'],
  http_req_failed: ['rate<0.01'],
  http_req_waiting: ['p(95)<400'],
};

export const UPDATED_PRODUCT = {
  id: 0,
  name: 'Updated Product',
  price: 99.99,
  stock: 100,
};

// ─── Cache tracking configuration ────────────────────────────────────────────
// Only the endpoints listed here will have cache hit/miss metrics emitted.
// These are the two GET endpoints that use the cache-aside pattern.
// Remove an entry to stop tracking cache data for that endpoint.
export const CACHE_TRACKED_ENDPOINTS = {
  GET_BY_ID: 'GET /api/product/:id',   // single product — primary cache path
  GET_ALL:   'GET /api/product',        // all products — per-item cache tracking
};
