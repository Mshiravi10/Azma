(() => {
  const hookSource = 'AZMA_PAGE_HOOK';
  const maxSummaryLength = 4000;
  const sensitive = ['password', 'passwd', 'secret', 'token', 'authorization', 'cookie', 'refresh'];

  function mask(value: string | null | undefined) {
    if (!value) return value ?? null;
    return sensitive.some((key) => value.toLowerCase().includes(key)) ? '***' : value.slice(0, maxSummaryLength);
  }

  function post(event: Record<string, unknown>) {
    window.postMessage({ source: hookSource, event }, '*');
  }

  function afterCurrentTask(callback: () => void) {
    if (typeof queueMicrotask === 'function') {
      queueMicrotask(callback);
      return;
    }

    window.setTimeout(callback, 0);
  }

  function requestSummary(body: unknown) {
    if (typeof body === 'string') return mask(body);
    if (body instanceof URLSearchParams) return mask(body.toString());
    if (body instanceof FormData) return '[form-data]';
    if (body instanceof Blob) return `[blob:${body.type || 'unknown'}]`;
    return body ? '[body]' : null;
  }

  async function responseSummary(response: Response) {
    try {
      const contentType = response.headers.get('content-type') ?? '';
      if (!contentType.includes('json') && !contentType.includes('text')) return null;
      return mask(await response.clone().text());
    } catch {
      return null;
    }
  }

  const originalFetch = window.fetch.bind(window);
  window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const started = performance.now();
    const method = init?.method ?? (input instanceof Request ? input.method : 'GET');
    const url = input instanceof Request ? input.url : String(input);
    const body = init?.body ?? (input instanceof Request ? '[request-body]' : null);
    try {
      const response = await originalFetch(input, init);
      post({
        type: 'network',
        method,
        url,
        urlPattern: url,
        statusCode: response.status,
        expectedStatus: response.status,
        durationMs: Math.round(performance.now() - started),
        requestSummary: requestSummary(body),
        responseSummary: await responseSummary(response),
      });
      return response;
    } catch (error) {
      post({
        type: 'network',
        method,
        url,
        urlPattern: url,
        durationMs: Math.round(performance.now() - started),
        requestSummary: requestSummary(body),
        responseSummary: mask(error instanceof Error ? error.message : String(error)),
      });
      throw error;
    }
  };

  const originalOpen = XMLHttpRequest.prototype.open;
  const originalSend = XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.open = function patchedOpen(method: string, url: string | URL) {
    (this as XMLHttpRequest & { __azmaMethod?: string; __azmaUrl?: string }).__azmaMethod = method;
    (this as XMLHttpRequest & { __azmaMethod?: string; __azmaUrl?: string }).__azmaUrl = String(url);
    return originalOpen.apply(this, arguments as any);
  };

  XMLHttpRequest.prototype.send = function patchedSend(body?: Document | XMLHttpRequestBodyInit | null) {
    const xhr = this as XMLHttpRequest & { __azmaMethod?: string; __azmaUrl?: string };
    const started = performance.now();
    xhr.addEventListener('loadend', () => {
      let responseText: string | null = null;
      try {
        responseText = typeof xhr.responseText === 'string' ? xhr.responseText : null;
      } catch {
        responseText = null;
      }
      post({
        type: 'network',
        method: xhr.__azmaMethod ?? 'GET',
        url: xhr.__azmaUrl ?? '',
        urlPattern: xhr.__azmaUrl ?? '*',
        statusCode: xhr.status || null,
        expectedStatus: xhr.status || null,
        durationMs: Math.round(performance.now() - started),
        requestSummary: requestSummary(body),
        responseSummary: mask(responseText),
      });
    });
    return originalSend.apply(this, arguments as any);
  };

  function postNavigation() {
    post({
      type: 'action',
      actionType: 'Navigate',
      url: window.location.href,
      label: document.title || window.location.href,
    });
  }

  const originalPushState = history.pushState;
  history.pushState = function patchedPushState() {
    const result = originalPushState.apply(this, arguments as unknown as [unknown, string, string | URL | null | undefined]);
    afterCurrentTask(postNavigation);
    return result;
  };

  const originalReplaceState = history.replaceState;
  history.replaceState = function patchedReplaceState() {
    const result = originalReplaceState.apply(this, arguments as unknown as [unknown, string, string | URL | null | undefined]);
    afterCurrentTask(postNavigation);
    return result;
  };

  window.addEventListener('popstate', postNavigation);
  window.addEventListener('hashchange', postNavigation);
})();
