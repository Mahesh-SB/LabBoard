import { check } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { PRODUCT_IDS } from './config.js';

// Custom metrics — all tagged with { endpoint } so Grafana can filter per endpoint.
export const cacheHits      = new Counter('cache_hits');
export const cacheMisses    = new Counter('cache_misses');
export const cacheHitRate   = new Rate('cache_hit_rate');
export const cacheEvictions = new Counter('cache_evictions');
export const viewIncrements = new Counter('view_increments');
export const productUpdates = new Counter('product_updates');
export const apiErrors      = new Counter('api_errors');
export const cacheWarmupTime = new Trend('cache_warmup_time_ms');

export function randomProductId() {
  return PRODUCT_IDS[Math.floor(Math.random() * PRODUCT_IDS.length)];
}

export function jsonHeaders() {
  return { 'Content-Type': 'application/json' };
}

export function checkStatus(res, expectedStatus = 200, label = '') {
  const tag = label ? `[${label}] ` : '';
  return check(res, {
    [`${tag}status is ${expectedStatus}`]: (r) => r.status === expectedStatus,
    [`${tag}response time < 500ms`]: (r) => r.timings.duration < 500,
    [`${tag}body is not empty`]: (r) => r.body && r.body.length > 0,
  });
}

/**
 * Check a single-product response and record cache hit/miss tagged by endpoint.
 * Pass `endpoint` from CACHE_TRACKED_ENDPOINTS to enable per-endpoint metrics,
 * or omit it to skip cache tracking for this call.
 */
export function checkProductResponse(res, label = '', endpoint = null) {
  const passed = checkStatus(res, 200, label);
  if (!passed || res.status !== 200) {
    apiErrors.add(1, { endpoint: endpoint || 'unknown' });
    return false;
  }

  const body = JSON.parse(res.body);
  const hasProduct = check(res, {
    [`[${label}] has product field`]: () => body.product !== undefined,
    [`[${label}] has source field`]: () => body.source === 'cache' || body.source === 'database',
    [`[${label}] product has id`]:   () => body.product && body.product.id !== undefined,
    [`[${label}] product has name`]: () => body.product && body.product.name !== undefined,
    [`[${label}] product has price`]:() => body.product && body.product.price !== undefined,
  });

  if (endpoint) trackCacheSource(body.source, endpoint);
  return hasProduct;
}

/**
 * Check the GET /api/product (all products) response.
 * Tracks cache hits/misses per item in the array, tagged by endpoint.
 * Pass `endpoint` from CACHE_TRACKED_ENDPOINTS to enable per-endpoint metrics.
 */
export function checkAllProductsResponse(res, label = 'GetAll', endpoint = null) {
  const passed = checkStatus(res, 200, label);
  if (!passed) {
    apiErrors.add(1, { endpoint: endpoint || 'unknown' });
    return false;
  }

  const body = JSON.parse(res.body);
  const ok = check(res, {
    [`[${label}] returns array`]:        () => Array.isArray(body),
    [`[${label}] returns 5 products`]:   () => body.length === 5,
    [`[${label}] each item has product`]:() => body.every(item => item.product !== undefined),
  });

  // Track cache per item only when endpoint tracking is configured
  if (endpoint && Array.isArray(body)) {
    body.forEach(item => trackCacheSource(item.source, endpoint));
  }

  return ok;
}

export function checkUpdateResponse(res, id, label = 'Update') {
  const passed = checkStatus(res, 200, label);
  if (!passed) {
    apiErrors.add(1);
    return false;
  }

  const body = JSON.parse(res.body);
  const ok = check(res, {
    [`[${label}] has updated field`]:       () => body.updated !== undefined,
    [`[${label}] cacheInvalidated is bool`]:() => typeof body.cacheInvalidated === 'boolean',
    [`[${label}] updated id matches`]:      () => body.updated && body.updated.id === id,
  });

  if (ok) productUpdates.add(1);
  return ok;
}

export function checkEvictResponse(res, id, label = 'Evict') {
  const passed = checkStatus(res, 200, label);
  if (!passed) {
    apiErrors.add(1);
    return false;
  }

  const body = JSON.parse(res.body);
  const ok = check(res, {
    [`[${label}] has id field`]:          () => body.id === id,
    [`[${label}] has cacheEvicted field`]:() => typeof body.cacheEvicted === 'boolean',
  });

  if (ok) cacheEvictions.add(1);
  return ok;
}

export function checkViewsResponse(res, id, label = 'Views') {
  const passed = checkStatus(res, 200, label);
  if (!passed) {
    apiErrors.add(1);
    return false;
  }

  const body = JSON.parse(res.body);
  const ok = check(res, {
    [`[${label}] has productId`]:   () => body.productId === id,
    [`[${label}] totalViews >= 1`]: () => body.totalViews >= 1,
  });

  if (ok) viewIncrements.add(1);
  return ok;
}

// ─── Internal ─────────────────────────────────────────────────────────────────

function trackCacheSource(source, endpoint) {
  if (!source) return;
  const tags = { endpoint };
  if (source === 'cache') {
    cacheHits.add(1, tags);
    cacheHitRate.add(true, tags);
  } else {
    cacheMisses.add(1, tags);
    cacheHitRate.add(false, tags);
  }
}
