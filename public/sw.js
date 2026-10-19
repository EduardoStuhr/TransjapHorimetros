/**
 * Transjap App-Shell Service Worker (Fase 7 - Offline First).
 *
 * DIRETRIZES E GARANTIAS DE SEGURANÇA:
 * 1. NUNCA realiza cache de endpoints autenticados ou de APIs (/api/*):
 *    - /api/auth (tokens, login, sessões, cookies HttpOnly)
 *    - /api/readings (leituras operacionais, metadados)
 *    - /api/readings/{readingId}/photo/content (conteúdo de fotos de evidência)
 *    - /api/readings/{readingId}/photo/ocr (dados e anomalias de OCR)
 *    - Qualquer resposta de API autenticada ou privada
 * 2. O cache é estritamente restrito ao App Shell e recursos estáticos seguros:
 *    - Rotas de navegação de campo: /coleta e /sincronizacao
 *    - Assets estáticos com hash: /_next/static/*
 *    - Identidade visual estática pública: /logo-transjap.png e /simbolo-transjap.png
 * 3. Todas as requisições não-GET ou iniciadas para /api/ são ignoradas pelo Service Worker
 *    e repassadas diretamente à rede sem interceptação nem armazenamento.
 */
const CACHE_NAME = "transjap-field-shell-v1";
const SHELL_ROUTES = new Set(["/coleta", "/sincronizacao"]);
const SAFE_ASSETS = ["/logo-transjap.png", "/simbolo-transjap.png"];

async function cacheFieldShell() {
  const cache = await caches.open(CACHE_NAME);
  for (const route of SHELL_ROUTES) {
    try {
      const response = await fetch(route, { cache: "reload", credentials: "same-origin" });
      if (!response.ok) continue;
      const html = await response.clone().text();
      await cache.put(route, response);
      const assetPaths = [...html.matchAll(/(?:src|href)="([^"?]*\/_next\/static\/[^"?]+)"/g)]
        .map((match) => new URL(match[1], self.location.origin).pathname);
      await Promise.all([...new Set(assetPaths)].map(async (asset) => {
        try {
          const assetResponse = await fetch(asset, { cache: "reload" });
          if (assetResponse.ok) await cache.put(asset, assetResponse);
        } catch {
          // Mantém os demais recursos já armazenados.
        }
      }));
    } catch {
      // A fila IndexedDB continua íntegra mesmo se o shell não puder ser renovado.
    }
  }
}

self.addEventListener("install", (event) => {
  event.waitUntil((async () => {
    const cache = await caches.open(CACHE_NAME);
    await Promise.all(SAFE_ASSETS.map(async (asset) => {
      try {
        const response = await fetch(asset, { cache: "reload" });
        if (response.ok) await cache.put(asset, response);
      } catch {
        // A próxima navegação online completa o cache.
      }
    }));
    self.skipWaiting();
  })());
});

self.addEventListener("activate", (event) => {
  event.waitUntil((async () => {
    const names = await caches.keys();
    await Promise.all(names.filter((name) => name !== CACHE_NAME).map((name) => caches.delete(name)));
    await self.clients.claim();
  })());
});

self.addEventListener("message", (event) => {
  if (event.data?.type === "CACHE_FIELD_SHELL") {
    event.waitUntil(cacheFieldShell());
  }
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin || url.pathname.startsWith("/api/")) return;

  if (request.mode === "navigate") {
    if (!SHELL_ROUTES.has(url.pathname)) return;
    event.respondWith((async () => {
      const cache = await caches.open(CACHE_NAME);
      try {
        const response = await fetch(request);
        if (response.ok) await cache.put(url.pathname, response.clone());
        return response;
      } catch {
        return (await cache.match(url.pathname)) || Response.error();
      }
    })());
    return;
  }

  const isStaticAsset = url.pathname.startsWith("/_next/static/") || SAFE_ASSETS.includes(url.pathname);
  if (!isStaticAsset) return;
  event.respondWith((async () => {
    const cached = await caches.match(request);
    if (cached) return cached;
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(CACHE_NAME);
      await cache.put(request, response.clone());
    }
    return response;
  })());
});
