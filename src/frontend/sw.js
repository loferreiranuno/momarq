// ============================================================
// Service Worker for Visual Search - Model Caching
// ============================================================

const CACHE_NAME = 'visual-search-v1';
const MODEL_CACHE_NAME = 'visual-search-models-v1';

// Static assets to cache
const STATIC_ASSETS = [
    '/',
    '/index.html'
];

// Model URLs patterns to cache
const MODEL_URL_PATTERNS = [
    'cdn.jsdelivr.net',
    'huggingface.co',
    'cdn-lfs.huggingface.co'
];

// Install event - cache static assets
self.addEventListener('install', (event) => {
    console.log('[SW] Installing Service Worker');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(STATIC_ASSETS))
            .then(() => self.skipWaiting())
    );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
    console.log('[SW] Activating Service Worker');
    event.waitUntil(
        caches.keys()
            .then(cacheNames => {
                return Promise.all(
                    cacheNames
                        .filter(name => name !== CACHE_NAME && name !== MODEL_CACHE_NAME)
                        .map(name => caches.delete(name))
                );
            })
            .then(() => self.clients.claim())
    );
});

// Fetch event - serve from cache or network
self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    // Check if this is a model-related request
    const isModelRequest = MODEL_URL_PATTERNS.some(pattern => url.href.includes(pattern));

    if (isModelRequest) {
        // Cache-first strategy for models
        event.respondWith(
            caches.open(MODEL_CACHE_NAME)
                .then(cache => {
                    return cache.match(event.request)
                        .then(cachedResponse => {
                            if (cachedResponse) {
                                console.log('[SW] Serving model from cache:', url.pathname);
                                return cachedResponse;
                            }

                            console.log('[SW] Fetching model from network:', url.pathname);
                            return fetch(event.request)
                                .then(networkResponse => {
                                    // Clone the response before caching
                                    if (networkResponse.ok) {
                                        const responseToCache = networkResponse.clone();
                                        cache.put(event.request, responseToCache);
                                    }
                                    return networkResponse;
                                });
                        });
                })
        );
    } else if (url.pathname.startsWith('/api/')) {
        // Network-only for API calls
        event.respondWith(fetch(event.request));
    } else {
        // Network-first for static assets
        event.respondWith(
            fetch(event.request)
                .then(networkResponse => {
                    // Update cache with fresh response
                    if (networkResponse.ok && event.request.method === 'GET') {
                        const responseToCache = networkResponse.clone();
                        caches.open(CACHE_NAME)
                            .then(cache => cache.put(event.request, responseToCache));
                    }
                    return networkResponse;
                })
                .catch(() => {
                    // Fallback to cache
                    return caches.match(event.request);
                })
        );
    }
});

// Message handler for cache management
self.addEventListener('message', (event) => {
    if (event.data.type === 'CLEAR_MODEL_CACHE') {
        caches.delete(MODEL_CACHE_NAME)
            .then(() => {
                console.log('[SW] Model cache cleared');
                event.ports[0].postMessage({ success: true });
            });
    }
    
    if (event.data.type === 'GET_CACHE_SIZE') {
        getCacheSize()
            .then(size => {
                event.ports[0].postMessage({ size });
            });
    }
});

// Helper to get cache size
async function getCacheSize() {
    const cacheNames = await caches.keys();
    let totalSize = 0;

    for (const name of cacheNames) {
        const cache = await caches.open(name);
        const requests = await cache.keys();
        
        for (const request of requests) {
            const response = await cache.match(request);
            if (response) {
                const blob = await response.clone().blob();
                totalSize += blob.size;
            }
        }
    }

    return totalSize;
}
