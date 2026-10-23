declare var chrome: any;

(() => {
  const webSource = 'AZMA_WEB';
  const extensionSource = 'AZMA_RECORDER_EXTENSION';

  function postToPage(type: string, payload?: Record<string, unknown>) {
    window.postMessage({ source: extensionSource, type, payload }, window.location.origin);
  }

  window.addEventListener('message', (event: MessageEvent) => {
    if (event.source !== window) return;
    const data = event.data;
    if (!data || data.source !== webSource) return;

    if (data.type === 'AZMA_RECORDER_PING') {
      postToPage('AZMA_RECORDER_AVAILABLE');
      chrome.runtime.sendMessage(
        { source: extensionSource, type: 'get-recorder-state' },
        (response: { ok?: boolean; state?: Record<string, unknown> | null } | undefined) => {
          void chrome.runtime.lastError;
          if (response?.state) postToPage('AZMA_RECORDER_STATE', { state: response.state });
        },
      );
      return;
    }

    if (data.type === 'AZMA_RECORDER_START' || data.type === 'AZMA_RECORDER_STOP') {
      if (data.type === 'AZMA_RECORDER_START') {
        try {
          if (new URL(data.payload?.gatewayBaseUrl).origin !== window.location.origin) {
            postToPage('AZMA_RECORDER_ERROR', {
              message: 'Recorder gateway must use the same origin as the Azma page.',
              requestId: data.payload?.requestId,
            });
            return;
          }
        } catch {
          postToPage('AZMA_RECORDER_ERROR', {
            message: 'Recorder gateway URL is invalid.',
            requestId: data.payload?.requestId,
          });
          return;
        }
      }
      const messageType = data.type === 'AZMA_RECORDER_START'
        ? 'start-recording'
        : 'stop-recording';

      chrome.runtime.sendMessage(
        { source: extensionSource, type: messageType, payload: data.payload },
        (response: { ok?: boolean; error?: string } | undefined) => {
          const error = chrome.runtime.lastError?.message ?? response?.error;
          if (error) {
            postToPage('AZMA_RECORDER_ERROR', {
              message: error,
              requestId: data.payload?.requestId,
            });
          }
        },
      );
    }
  });

  chrome.runtime.onMessage.addListener((message: { source?: string; type?: string; payload?: Record<string, unknown> }) => {
    if (message?.source !== extensionSource || !message.type) return;
    postToPage(message.type, message.payload);
  });
})();
