declare var chrome: any;

type TargetAction = Record<string, any> & {
  type: string;
  selector?: string | null;
  selectorAlternates?: string[] | null;
  selectorStrategy?: string | null;
  selectorConfidence?: number | null;
  label?: string | null;
};

type TargetAssertion = Record<string, any> & {
  type: string;
  selector?: string | null;
  selectorAlternates?: string[] | null;
  label?: string | null;
};

type TargetStep = {
  id: string;
  title: string | null;
  actionText: string | null;
  expectedResult: string | null;
  actions: TargetAction[];
  assertions: TargetAssertion[];
  networkCaptures: Array<Record<string, any>>;
  evidence: { url?: string | null; pageTitle?: string | null; capturedAtUtc?: string | null };
};

type TargetScenario = Record<string, any> & {
  schemaVersion: number;
  name?: string | null;
  startUrl?: string | null;
  actions: TargetAction[];
  networkCaptures: Array<Record<string, any>>;
  expectations: TargetAssertion[];
  variables: Record<string, string>;
  timeoutSeconds: number;
  steps: TargetStep[];
};

(() => {
  const extensionSource = 'AZMA_RECORDER_EXTENSION';
  const hookSource = 'AZMA_PAGE_HOOK';
  const rootId = 'azma-recorder-root';
  const fillTimers = new WeakMap<Element, number>();
  const pendingFills = new Set<HTMLInputElement | HTMLTextAreaElement>();
  const recentNetwork: Array<{ method?: string; url?: string; statusCode?: number; responseSummary?: string | null }> = [];
  const history: TargetScenario[] = [];

  let active = false;
  let paused = false;
  let minimized = false;
  let showVariables = false;
  let pendingElementExpectation = false;
  let eventCount = 0;
  let lastRecordedUrl: string | null = null;
  let targetOrigin = window.location.origin;
  let scenario: TargetScenario;
  let shadow: ShadowRoot | null = null;
  let syncTimer: number | null = null;

  function id() {
    return typeof globalThis.crypto?.randomUUID === 'function'
      ? globalThis.crypto.randomUUID()
      : `step-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  }

  function clone<T>(value: T): T {
    return JSON.parse(JSON.stringify(value)) as T;
  }

  function send(type: string, payload?: Record<string, unknown>, callback?: (response: any) => void) {
    chrome.runtime.sendMessage({ source: extensionSource, type, ...payload }, (response: any) => {
      void chrome.runtime.lastError;
      callback?.(response);
    });
  }

  function syncScenario(immediate = false, callback?: () => void) {
    if (!active || !scenario) return callback?.();
    if (syncTimer !== null) window.clearTimeout(syncTimer);
    const sync = () => {
      syncTimer = null;
      send('sync-scenario', { scenario: clone(scenario) }, () => callback?.());
    };
    if (immediate) sync();
    else syncTimer = window.setTimeout(sync, 180);
  }

  function remember() {
    history.push(clone(scenario));
    if (history.length > 30) history.shift();
  }

  function mutateUser(change: () => void) {
    remember();
    change();
    render();
    syncScenario();
  }

  function esc(value: unknown) {
    return String(value ?? '')
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
  }

  function portableUrl(value: string) {
    try {
      const url = new URL(value, window.location.href);
      return url.origin === targetOrigin
        ? `{{baseUrl}}${url.pathname}${url.search}${url.hash}`
        : url.href;
    } catch {
      return value;
    }
  }

  function humanAction(action: TargetAction) {
    const label = action.label || action.selector || '';
    switch (action.type) {
      case 'Navigate': return `باز کردن ${action.url ?? ''}`;
      case 'Click': return `کلیک روی ${label}`;
      case 'Fill': return `وارد کردن مقدار در ${label}`;
      case 'Select': return `انتخاب مقدار در ${label}`;
      case 'Submit': return `ارسال ${label || 'فرم'}`;
      case 'Wait': return `انتظار به مدت ${action.delayMs ?? 0} میلی‌ثانیه`;
      default: return label || action.type;
    }
  }

  function humanExpectation(assertion: TargetAssertion) {
    switch (assertion.type) {
      case 'VisibleText': return `متن «${assertion.text ?? ''}» نمایش داده شود`;
      case 'ElementVisible': return `عنصر ${assertion.label || assertion.selector || ''} نمایش داده شود`;
      case 'UrlMatches': return `آدرس صفحه با ${assertion.urlPattern ?? ''} مطابقت داشته باشد`;
      case 'ApiStatus': return `پاسخ API ${assertion.urlPattern ?? ''} دارای وضعیت ${assertion.statusCode ?? ''} باشد`;
      case 'ApiBodyContains': return `بدنه پاسخ API شامل ${assertion.bodyContains ?? ''} باشد`;
      case 'ApiJsonPathEquals': return `مقدار ${assertion.jsonPath ?? ''} برابر ${assertion.expectedValue ?? ''} باشد`;
      default: return assertion.label || assertion.type;
    }
  }

  function emptyScenario(targetUrl: string): TargetScenario {
    let origin = window.location.origin;
    try { origin = new URL(targetUrl).origin; } catch { /* use current origin */ }
    targetOrigin = origin;
    return {
      schemaVersion: 2,
      name: document.title || null,
      startUrl: portableUrl(targetUrl),
      actions: [],
      networkCaptures: [],
      expectations: [],
      variables: { baseUrl: origin },
      timeoutSeconds: 60,
      steps: [],
    };
  }

  function stepFromAction(action: TargetAction, stepId = id()): TargetStep {
    return {
      id: stepId,
      title: action.label || action.type,
      actionText: humanAction(action),
      expectedResult: null,
      actions: [action],
      assertions: [],
      networkCaptures: [],
      evidence: { url: window.location.href, pageTitle: document.title, capturedAtUtc: new Date().toISOString() },
    };
  }

  function normalizeScenario(
    stored: any,
    initial: any,
    manualSteps: Array<{ action?: string; expectedResult?: string | null }> | undefined,
    targetUrl: string,
  ) {
    const source = stored ?? initial;
    const result = emptyScenario(targetUrl);
    if (source && typeof source === 'object') {
      let recordedOrigin: string | null = null;
      try {
        const base = source.variables?.baseUrl || source.startUrl;
        if (typeof base === 'string' && !base.includes('{{')) recordedOrigin = new URL(base).origin;
      } catch { recordedOrigin = null; }
      const migrateUrl = (value: any) => {
        if (typeof value !== 'string' || value.includes('{{')) return value;
        if (value === '*') return value;
        try {
          const url = new URL(value);
          if (recordedOrigin && url.origin === recordedOrigin)
            return `{{baseUrl}}${url.pathname}${url.search}${url.hash}`;
        } catch { /* keep non-absolute patterns */ }
        return portableUrl(value);
      };
      const migrateAction = (action: TargetAction) => ({
        ...action, url: action.url ? migrateUrl(action.url) : action.url,
      });
      const migrateAssertion = (assertion: TargetAssertion) => ({
        ...assertion, urlPattern: assertion.urlPattern ? migrateUrl(assertion.urlPattern) : assertion.urlPattern,
      });
      result.name = source.name ?? result.name;
      result.startUrl = migrateUrl(source.startUrl) ?? result.startUrl;
      result.timeoutSeconds = source.timeoutSeconds ?? 60;
      result.variables = { ...(source.variables ?? {}), baseUrl: targetOrigin };
      result.networkCaptures = Array.isArray(source.networkCaptures)
        ? source.networkCaptures.map((capture: any) => ({
            ...capture,
            urlPattern: migrateUrl(capture.urlPattern),
          }))
        : [];
      if (Array.isArray(source.steps) && source.steps.length > 0) {
        result.steps = source.steps.map((step: any) => ({
          id: step.id || id(), title: step.title ?? null, actionText: step.actionText ?? null,
          expectedResult: step.expectedResult ?? null,
          actions: Array.isArray(step.actions) ? step.actions.map(migrateAction) : [],
          assertions: Array.isArray(step.assertions) ? step.assertions.map(migrateAssertion) : [],
          networkCaptures: Array.isArray(step.networkCaptures)
            ? step.networkCaptures.map((capture: any) => ({
                ...capture, urlPattern: migrateUrl(capture.urlPattern),
              }))
            : [],
          evidence: step.evidence ?? {},
        }));
      } else if (Array.isArray(source.actions) && source.actions.length > 0) {
        result.steps = source.actions.map((action: TargetAction) => stepFromAction(migrateAction(action)));
        const assertions = Array.isArray(source.expectations) ? source.expectations.map(migrateAssertion) : [];
        if (assertions.length > 0) {
          const holder = result.steps[result.steps.length - 1] ?? {
            id: id(), title: 'بررسی نتیجه', actionText: 'بررسی نتیجه', expectedResult: null,
            actions: [], assertions: [], networkCaptures: [], evidence: {},
          };
          holder.assertions.push(...assertions);
          holder.expectedResult = assertions.map(humanExpectation).join('؛ ');
          if (result.steps.length === 0) result.steps.push(holder);
        }
      }
    }

    if (result.steps.length === 0 && Array.isArray(manualSteps)) {
      result.steps = manualSteps.filter(step => step.action?.trim()).map(step => ({
        id: id(), title: null, actionText: step.action!.trim(), expectedResult: step.expectedResult?.trim() || null,
        actions: [], assertions: [], networkCaptures: [], evidence: {},
      }));
    }
    result.schemaVersion = 2;
    result.actions = [];
    result.expectations = [];
    return result;
  }

  function integrateAction(action: TargetAction, stepId?: string) {
    const latest = scenario.steps[scenario.steps.length - 1];
    if (action.type === 'Fill' && latest?.actions.length === 1 && latest.actions[0].type === 'Fill' &&
        latest.actions[0].selector === action.selector) {
      latest.actions[0] = action;
      latest.actionText = humanAction(action);
      latest.evidence = { url: window.location.href, pageTitle: document.title, capturedAtUtc: new Date().toISOString() };
      return latest.id;
    }
    const step = stepFromAction(action, stepId);
    scenario.steps.push(step);
    return step.id;
  }

  function integrateAssertion(assertion: TargetAssertion, stepId?: string) {
    let step = stepId ? scenario.steps.find(item => item.id === stepId) : scenario.steps[scenario.steps.length - 1];
    if (!step) {
      step = {
        id: stepId || id(), title: 'بررسی نتیجه', actionText: 'بررسی نتیجه', expectedResult: null,
        actions: [], assertions: [], networkCaptures: [],
        evidence: { url: window.location.href, pageTitle: document.title, capturedAtUtc: new Date().toISOString() },
      };
      scenario.steps.push(step);
    }
    step.assertions.push(assertion);
    const text = humanExpectation(assertion);
    step.expectedResult = step.expectedResult ? `${step.expectedResult}؛ ${text}` : text;
    return step.id;
  }

  function integrateNetwork(capture: Record<string, any>) {
    capture.urlPattern = portableUrl(capture.urlPattern || capture.url || '*');
    scenario.networkCaptures.push(capture);
    if (scenario.networkCaptures.length > 120) scenario.networkCaptures.shift();
    const latest = scenario.steps[scenario.steps.length - 1];
    if (latest) {
      latest.networkCaptures.push(capture);
      if (latest.networkCaptures.length > 20) latest.networkCaptures.shift();
    }
  }

  function record(events: Array<Record<string, any>>) {
    if (!active || paused || events.length === 0) return;
    let needsRender = false;
    const enriched = events.map(event => {
      const next: Record<string, any> = {
        ...event, eventId: event.eventId || id(), recordedAtUtc: new Date().toISOString(),
      };
      if (next.type === 'action') {
        const action: TargetAction = { ...next, type: next.actionType };
        delete action.actionType;
        if (action.type === 'Navigate' && action.url) action.url = portableUrl(action.url);
        next.stepId = integrateAction(action, next.stepId);
        needsRender = true;
      } else if (next.type === 'expectation') {
        const assertion: TargetAssertion = { ...next, type: next.expectationType };
        delete assertion.expectationType;
        if (assertion.urlPattern) assertion.urlPattern = portableUrl(assertion.urlPattern);
        next.stepId = integrateAssertion(assertion, next.stepId);
        needsRender = true;
      } else if (next.type === 'network') {
        integrateNetwork({
          method: next.method || 'GET', urlPattern: next.urlPattern || next.url || '*',
          expectedStatus: next.expectedStatus ?? next.statusCode ?? null, durationMs: next.durationMs ?? null,
          requestSummary: next.requestSummary ?? null, responseSummary: next.responseSummary ?? null,
        });
      }
      return next;
    });
    eventCount += enriched.length;
    if (needsRender) render(); else updateStatus();
    syncScenario();
    send('record-events', { events: enriched });
  }

  function navigationEvent(url = window.location.href) {
    return { type: 'action', actionType: 'Navigate', url, label: document.title || url };
  }

  function recordNavigation(url = window.location.href) {
    if (lastRecordedUrl === url) return;
    lastRecordedUrl = url;
    record([navigationEvent(url)]);
  }

  function cssEscape(value: string) {
    return value.replace(/["\\]/g, '\\$&').replace(/^\d/, '\\3$& ');
  }

  function attr(name: string, value: string) { return `[${name}="${cssEscape(value)}"]`; }
  function visibleText(element: Element) { return (element.textContent ?? '').replace(/\s+/g, ' ').trim().slice(0, 120); }

  function clickTarget(element: Element) {
    return element.closest('input, textarea, select, button, a, [role="button"], [role="checkbox"], [role="radio"], [role="switch"], [role="tab"], [role="menuitem"], [role="link"], summary')
      ?? element.closest('label') ?? element;
  }

  function controlLabel(element: Element, fallback: string) {
    const html = element as HTMLElement;
    const icon = element.matches('svg[name]') ? element : element.querySelector('svg[name]');
    return html.getAttribute('aria-label') || html.getAttribute('title') || visibleText(element)
      || icon?.getAttribute('name') || fallback;
  }

  function cssPath(element: Element) {
    const parts: string[] = [];
    let current: Element | null = element;
    while (current && current !== document.body) {
      const parent = current.parentElement;
      if (!parent) break;
      const siblings = (Array.prototype.slice.call(parent.children) as Element[])
        .filter(child => child.tagName === current!.tagName);
      parts.unshift(`${current.tagName.toLowerCase()}:nth-of-type(${siblings.indexOf(current) + 1})`);
      current = parent;
    }
    return parts.length ? parts.join(' > ') : element.tagName.toLowerCase();
  }

  function selectorsFor(element: Element) {
    const candidates: Array<{ selector: string; strategy: string; score: number }> = [];
    const html = element as HTMLElement;
    for (const name of ['data-testid', 'data-test', 'data-qa']) {
      const value = html.getAttribute(name);
      if (value) candidates.push({ selector: attr(name, value), strategy: name, score: 100 });
    }
    if (html.id) candidates.push({ selector: `#${cssEscape(html.id)}`, strategy: 'id', score: 92 });
    const name = html.getAttribute('name');
    if (name) candidates.push({ selector: `${element.tagName.toLowerCase()}${attr('name', name)}`, strategy: 'name', score: 84 });
    const aria = html.getAttribute('aria-label');
    if (aria) candidates.push({ selector: attr('aria-label', aria), strategy: 'aria-label', score: 82 });
    const title = html.getAttribute('title');
    if (title) candidates.push({ selector: attr('title', title), strategy: 'title', score: 72 });
    const text = visibleText(element);
    const containsControl = element.querySelector('button, a, input, select, textarea, [role="button"]');
    if (text && text.length <= 40 && !containsControl)
      candidates.push({ selector: `text=${text}`, strategy: 'text', score: 65 });
    candidates.push({ selector: cssPath(element), strategy: 'css-path', score: 30 });
    const distinct = candidates.filter((item, index) =>
      candidates.findIndex(candidate => candidate.selector === item.selector) === index);
    return {
      selector: distinct[0].selector,
      selectorAlternates: distinct.slice(1).map(item => item.selector),
      selectorStrategy: distinct[0].strategy,
      selectorConfidence: distinct[0].score,
    };
  }

  function isSensitive(element: Element) {
    const html = element as HTMLInputElement;
    const probe = [html.type, html.name, html.id, html.autocomplete,
      html.getAttribute('aria-label'), html.getAttribute('placeholder')].filter(Boolean).join(' ').toLowerCase();
    return ['password', 'passwd', 'secret', 'token', 'authorization', 'cookie'].some(key => probe.includes(key));
  }

  function injectPageHook() {
    if (document.documentElement.dataset.azmaRecorderHooked === 'true') return;
    document.documentElement.dataset.azmaRecorderHooked = 'true';
    const script = document.createElement('script');
    script.src = chrome.runtime.getURL('pageHook.js');
    script.onload = () => script.remove();
    (document.head || document.documentElement).appendChild(script);
  }

  function stepCard(step: TargetStep, index: number) {
    const confidences = step.actions.map(action => action.selectorConfidence).filter(Number.isFinite) as number[];
    const confidence = confidences.length ? Math.min(...confidences) : null;
    const confidenceClass = confidence === null ? 'neutral' : confidence >= 80 ? 'good' : confidence >= 60 ? 'warn' : 'bad';
    const technical = step.actions.map(action => `${action.type}: ${action.label || action.selector || ''}`).join(' · ');
    return `<article class="step" draggable="true" data-step-id="${esc(step.id)}">
      <div class="step-head"><span class="step-number">${index + 1}</span>
        <input data-field="title" value="${esc(step.title)}" placeholder="عنوان گام" />
        <span class="confidence ${confidenceClass}" title="Selector confidence">${confidence === null ? 'Manual' : `${confidence}%`}</span>
      </div>
      <label>عمل کاربر<textarea data-field="actionText" rows="2">${esc(step.actionText)}</textarea></label>
      <label>نتیجه مورد انتظار<textarea data-field="expectedResult" rows="2" placeholder="نتیجه مورد انتظار را بنویسید">${esc(step.expectedResult)}</textarea></label>
      <div class="technical" title="${esc(technical)}">${esc(technical || 'گام دستی')} · ${step.assertions.length} assertion · ${step.networkCaptures.length} network</div>
      <div class="step-actions">
        <button data-command="up" title="بالا">↑</button><button data-command="down" title="پایین">↓</button>
        <button data-command="merge" title="ادغام با قبلی">Merge</button>
        ${step.actions.length > 1 ? '<button data-command="split">Split</button>' : ''}
        ${step.actions[0]?.selector ? '<button data-command="highlight">Highlight</button>' : ''}
        <button class="danger" data-command="delete">حذف</button>
      </div>
    </article>`;
  }

  function render() {
    if (!shadow || !scenario) return;
    const panel = shadow.querySelector<HTMLElement>('.studio');
    const scroll = shadow.querySelector<HTMLElement>('.timeline')?.scrollTop ?? 0;
    if (!panel) return;
    panel.classList.toggle('minimized', minimized);
    panel.innerHTML = `<header><div><strong>Azma Scenario Studio</strong><small class="status"></small></div>
      <div><button data-command="undo" ${history.length ? '' : 'disabled'}>Undo</button><button data-command="minimize">${minimized ? '□' : '—'}</button></div></header>
      <section class="studio-body">
        <div class="toolbar">
          <button data-command="pause">${paused ? 'Resume' : 'Pause'}</button><button data-command="manual">+ گام دستی</button>
          <button data-command="expect-text">✓ متن</button><button data-command="expect-element">✓ عنصر</button>
          <button data-command="expect-url">✓ URL</button><button data-command="expect-api">✓ API</button>
          <button data-command="wait">⏱ Wait</button><button data-command="smart-group">Smart group</button>
          <button data-command="variables">{{ }} Variables</button>
        </div>
        ${showVariables ? `<div class="variables"><div class="section-title">متغیرهای سناریو <button data-command="add-variable">+</button></div>
          ${Object.entries(scenario.variables).map(([key, value]) => `<div class="variable-row" data-variable="${esc(key)}">
            <input data-variable-key value="${esc(key)}" ${key === 'baseUrl' ? 'readonly' : ''}/><input data-variable-value value="${esc(value)}"/>
            ${key === 'baseUrl' ? '' : '<button data-command="remove-variable">×</button>'}</div>`).join('')}</div>` : ''}
        <div class="timeline">${scenario.steps.length
          ? scenario.steps.map(stepCard).join('')
          : '<div class="empty">در صفحه کار کنید یا یک گام دستی بسازید.</div>'}</div>
        <footer><span>${scenario.steps.length} گام</span><button class="stop" data-command="stop">Stop & Save Draft</button></footer>
      </section>`;
    updateStatus();
    const timeline = shadow.querySelector<HTMLElement>('.timeline');
    if (timeline) timeline.scrollTop = scroll;
  }

  function updateStatus(text?: string) {
    const node = shadow?.querySelector<HTMLElement>('.status');
    if (node) node.textContent = text ?? (paused ? 'Paused' : `${eventCount} events · ${scenario?.steps.length ?? 0} steps`);
  }

  function mountStudio() {
    if (document.getElementById(rootId)) return;
    const host = document.createElement('div');
    host.id = rootId;
    document.documentElement.appendChild(host);
    shadow = host.attachShadow({ mode: 'open' });
    const style = document.createElement('link');
    style.rel = 'stylesheet';
    style.href = chrome.runtime.getURL('overlay.css');
    const panel = document.createElement('aside');
    panel.className = 'studio';
    shadow.append(style, panel);
    shadow.addEventListener('click', onStudioClick);
    shadow.addEventListener('change', onStudioChange);
    shadow.addEventListener('dragstart', onDragStart);
    shadow.addEventListener('dragover', event => event.preventDefault());
    shadow.addEventListener('drop', onDrop);
    render();
  }

  function selectedStep(target: Element) {
    const idValue = target.closest<HTMLElement>('[data-step-id]')?.dataset.stepId;
    return idValue ? scenario.steps.find(step => step.id === idValue) : undefined;
  }

  function onStudioClick(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    const button = (event.target as Element).closest<HTMLButtonElement>('button[data-command]');
    if (!button) return;
    const command = button.dataset.command;
    const step = selectedStep(button);
    const index = step ? scenario.steps.indexOf(step) : -1;
    switch (command) {
      case 'pause': paused = !paused; send('pause-recording', { paused }); render(); break;
      case 'minimize': minimized = !minimized; render(); break;
      case 'variables': showVariables = !showVariables; render(); break;
      case 'undo': {
        const previous = history.pop();
        if (previous) { scenario = previous; render(); syncScenario(); }
        break;
      }
      case 'manual': mutateUser(() => scenario.steps.push({
        id: id(), title: 'گام دستی', actionText: '', expectedResult: '', actions: [], assertions: [],
        networkCaptures: [], evidence: { url: window.location.href, pageTitle: document.title, capturedAtUtc: new Date().toISOString() },
      })); break;
      case 'expect-text': addTextExpectation(); break;
      case 'expect-element': pendingElementExpectation = true; updateStatus('عنصر مورد انتظار را در صفحه انتخاب کنید'); break;
      case 'expect-url': addUrlExpectation(); break;
      case 'expect-api': addApiExpectation(); break;
      case 'wait': addWait(); break;
      case 'smart-group': smartGroupFormSteps(); break;
      case 'add-variable': addVariable(); break;
      case 'remove-variable': {
        const key = button.closest<HTMLElement>('[data-variable]')?.dataset.variable;
        if (key) mutateUser(() => delete scenario.variables[key]);
        break;
      }
      case 'up': if (index > 0) mutateUser(() => { const [item] = scenario.steps.splice(index, 1); scenario.steps.splice(index - 1, 0, item); }); break;
      case 'down': if (index >= 0 && index < scenario.steps.length - 1) mutateUser(() => { const [item] = scenario.steps.splice(index, 1); scenario.steps.splice(index + 1, 0, item); }); break;
      case 'merge': if (step && index > 0) mutateUser(() => {
        const previous = scenario.steps[index - 1];
        previous.actions.push(...step.actions); previous.assertions.push(...step.assertions);
        previous.networkCaptures.push(...step.networkCaptures);
        previous.actionText = [previous.actionText, step.actionText].filter(Boolean).join('؛ ');
        previous.expectedResult = [previous.expectedResult, step.expectedResult].filter(Boolean).join('؛ ');
        scenario.steps.splice(index, 1);
      }); break;
      case 'split': if (step && step.actions.length > 1) mutateUser(() => {
        const extras = step.actions.splice(1).map(action => stepFromAction(action));
        scenario.steps.splice(index + 1, 0, ...extras);
        step.actionText = humanAction(step.actions[0]);
      }); break;
      case 'highlight': if (step?.actions[0]?.selector) highlight(step.actions[0].selector); break;
      case 'delete': if (index >= 0) mutateUser(() => scenario.steps.splice(index, 1)); break;
      case 'stop':
        flushPendingFills(); updateStatus('در حال ذخیره Draft…');
        syncScenario(true, () => send('stop-recording'));
        break;
    }
  }

  function onStudioChange(event: Event) {
    const input = event.target as HTMLInputElement | HTMLTextAreaElement;
    const step = selectedStep(input);
    const field = input.dataset.field as 'title' | 'actionText' | 'expectedResult' | undefined;
    if (step && field) mutateUser(() => { step[field] = input.value.trim() || null; });
    const row = input.closest<HTMLElement>('[data-variable]');
    if (row && input.hasAttribute('data-variable-value')) {
      const key = row.dataset.variable!;
      mutateUser(() => { scenario.variables[key] = input.value; });
    }
    if (row && input.hasAttribute('data-variable-key')) {
      const oldKey = row.dataset.variable!;
      const newKey = input.value.trim();
      if (newKey && newKey !== oldKey) mutateUser(() => {
        const value = scenario.variables[oldKey]; delete scenario.variables[oldKey]; scenario.variables[newKey] = value;
      });
    }
  }

  function onDragStart(event: Event) {
    const drag = event as DragEvent;
    const stepId = (drag.target as Element).closest<HTMLElement>('[data-step-id]')?.dataset.stepId;
    if (stepId) drag.dataTransfer?.setData('text/azma-step', stepId);
  }

  function onDrop(event: Event) {
    const drag = event as DragEvent;
    const sourceId = drag.dataTransfer?.getData('text/azma-step');
    const targetId = (drag.target as Element).closest<HTMLElement>('[data-step-id]')?.dataset.stepId;
    if (!sourceId || !targetId || sourceId === targetId) return;
    const from = scenario.steps.findIndex(step => step.id === sourceId);
    const to = scenario.steps.findIndex(step => step.id === targetId);
    if (from < 0 || to < 0) return;
    mutateUser(() => { const [item] = scenario.steps.splice(from, 1); scenario.steps.splice(to, 0, item); });
  }

  function addTextExpectation() {
    const text = window.prompt('متن قابل مشاهده', window.getSelection()?.toString() || '')?.trim();
    if (text) mutateUser(() => integrateAssertion({ type: 'VisibleText', text, label: text.slice(0, 80) }));
  }

  function addUrlExpectation() {
    const pattern = window.prompt('الگوی URL', portableUrl(window.location.href))?.trim();
    if (pattern) mutateUser(() => integrateAssertion({ type: 'UrlMatches', urlPattern: pattern, label: 'URL match' }));
  }

  function addApiExpectation() {
    const last = recentNetwork[recentNetwork.length - 1];
    const kind = window.prompt('نوع انتظار API: status, body, json', 'status')?.trim().toLowerCase();
    if (!kind) return;
    const urlPattern = window.prompt('الگوی URL', last?.url ? portableUrl(last.url) : '*')?.trim();
    if (!urlPattern) return;
    if (kind === 'status') {
      const statusCode = Number(window.prompt('Status code', String(last?.statusCode ?? 200)));
      if (Number.isFinite(statusCode)) mutateUser(() => integrateAssertion({
        type: 'ApiStatus', urlPattern, statusCode, method: last?.method ?? null, label: `${urlPattern} ${statusCode}`,
      }));
    } else if (kind === 'body') {
      const bodyContains = window.prompt('Body contains', '')?.trim();
      if (bodyContains) mutateUser(() => integrateAssertion({
        type: 'ApiBodyContains', urlPattern, bodyContains, method: last?.method ?? null, label: bodyContains.slice(0, 80),
      }));
    } else if (kind === 'json') {
      const jsonPath = window.prompt('JSON path', '$.')?.trim();
      const expectedValue = window.prompt('Expected value', '') ?? '';
      if (jsonPath) mutateUser(() => integrateAssertion({
        type: 'ApiJsonPathEquals', urlPattern, jsonPath, expectedValue, method: last?.method ?? null, label: jsonPath,
      }));
    }
  }

  function addWait() {
    const delay = Number(window.prompt('زمان انتظار (میلی‌ثانیه)', '1000'));
    if (Number.isFinite(delay) && delay >= 0) mutateUser(() => integrateAction({
      type: 'Wait', delayMs: Math.min(delay, 120000), label: `Wait ${delay}ms`,
    }));
  }

  function smartGroupFormSteps() {
    mutateUser(() => {
      const grouped: TargetStep[] = [];
      for (let index = 0; index < scenario.steps.length; index++) {
        const step = scenario.steps[index];
        const firstType = step.actions[0]?.type;
        if (!['Fill', 'Select'].includes(firstType) || step.actions.length !== 1) {
          grouped.push(step);
          continue;
        }

        const members = [step];
        while (index + 1 < scenario.steps.length) {
          const candidate = scenario.steps[index + 1];
          const type = candidate.actions[0]?.type;
          const samePage = candidate.evidence?.url === step.evidence?.url;
          if (!samePage || candidate.actions.length !== 1 || !['Fill', 'Select'].includes(type)) break;
          members.push(candidate); index++;
        }
        if (members.length === 1) {
          grouped.push(step);
          continue;
        }

        const fields = members.map(member => member.actions[0]?.label).filter(Boolean).join('، ');
        grouped.push({
          ...step,
          title: 'تکمیل فرم',
          actionText: fields ? `تکمیل فیلدهای ${fields}` : 'تکمیل فرم',
          expectedResult: members.map(member => member.expectedResult).filter(Boolean).join('؛ ') || null,
          actions: members.flatMap(member => member.actions),
          assertions: members.flatMap(member => member.assertions),
          networkCaptures: members.flatMap(member => member.networkCaptures),
        });
      }
      scenario.steps = grouped;
    });
  }

  function addVariable() {
    const key = window.prompt('نام متغیر (برای استفاده به شکل {{name}})', '')?.trim();
    if (!key || !/^[A-Za-z0-9_.-]+$/.test(key)) return;
    const value = window.prompt('مقدار پیش‌فرض', '') ?? '';
    mutateUser(() => { scenario.variables[key] = value; });
  }

  function highlight(selector: string) {
    if (selector.startsWith('text=')) return updateStatus('Highlight برای selector متنی در دسترس نیست');
    try {
      const element = document.querySelector<HTMLElement>(selector);
      if (!element) return updateStatus('Selector در صفحه فعلی پیدا نشد');
      const previous = element.style.outline;
      element.scrollIntoView({ block: 'center', behavior: 'smooth' });
      element.style.outline = '4px solid #7c3aed';
      window.setTimeout(() => { element.style.outline = previous; }, 1800);
    } catch { updateStatus('Selector معتبر نیست'); }
  }

  function recordFill(target: HTMLInputElement | HTMLTextAreaElement) {
    const selectors = selectorsFor(target);
    record([{ type: 'action', actionType: 'Fill', ...selectors, value: target.value,
      sensitive: isSensitive(target),
      label: target.getAttribute('aria-label') || target.getAttribute('placeholder') || selectors.selector }]);
  }

  function flushPendingFills() {
    for (const target of pendingFills) { window.clearTimeout(fillTimers.get(target)); recordFill(target); }
    pendingFills.clear();
  }

  function onClick(event: MouseEvent) {
    const target = event.target as Element | null;
    if (!target || target.closest(`#${rootId}`)) return;
    const control = clickTarget(target);
    const selectors = selectorsFor(control);
    const label = controlLabel(control, selectors.selector);
    if (pendingElementExpectation) {
      pendingElementExpectation = false; event.preventDefault(); event.stopPropagation();
      mutateUser(() => integrateAssertion({ type: 'ElementVisible', ...selectors, label }));
      return;
    }
    if (control.matches('button[type="submit"], input[type="submit"]') && control.closest('form')) return;
    // Capture before the page can navigate or a SPA can detach the clicked node.
    flushPendingFills();
    record([{ type: 'action', actionType: 'Click', ...selectors, text: visibleText(control), label }]);
  }

  function onInput(event: Event) {
    const target = event.target as HTMLInputElement | HTMLTextAreaElement | null;
    if (!target || target.closest(`#${rootId}`) || !['INPUT', 'TEXTAREA'].includes(target.tagName)) return;
    pendingFills.add(target); window.clearTimeout(fillTimers.get(target));
    fillTimers.set(target, window.setTimeout(() => { pendingFills.delete(target); recordFill(target); }, 450));
  }

  function onChange(event: Event) {
    const target = event.target as HTMLSelectElement | null;
    if (!target || target.closest(`#${rootId}`) || target.tagName !== 'SELECT') return;
    const selectors = selectorsFor(target);
    record([{ type: 'action', actionType: 'Select', ...selectors, value: target.value,
      label: target.getAttribute('aria-label') || target.name || selectors.selector }]);
  }

  function onSubmit(event: SubmitEvent) {
    const target = event.target as HTMLFormElement | null;
    if (!target || target.closest(`#${rootId}`)) return;
    flushPendingFills(); const selectors = selectorsFor(target);
    record([{ type: 'action', actionType: 'Submit', ...selectors,
      label: target.getAttribute('aria-label') || target.id || 'Submit form' }]);
  }

  function start(payload?: any) {
    if (active) return;
    active = true; paused = Boolean(payload?.paused); eventCount = payload?.eventCount ?? 0;
    try { targetOrigin = new URL(payload?.targetUrl ?? window.location.href).origin; } catch { targetOrigin = window.location.origin; }
    scenario = normalizeScenario(payload?.scenario, payload?.initialDefinition, payload?.initialManualSteps, payload?.targetUrl ?? window.location.href);
    injectPageHook(); mountStudio();
    document.addEventListener('click', onClick, true); document.addEventListener('input', onInput, true);
    document.addEventListener('change', onChange, true); document.addEventListener('submit', onSubmit, true);
    if (eventCount === 0) record([{ type: 'session', startUrl: scenario.startUrl, targetUrl: payload?.targetUrl }]);
    recordNavigation(); syncScenario();
  }

  function stop() {
    active = false; paused = false; pendingElementExpectation = false;
    document.removeEventListener('click', onClick, true); document.removeEventListener('input', onInput, true);
    document.removeEventListener('change', onChange, true); document.removeEventListener('submit', onSubmit, true);
    document.getElementById(rootId)?.remove(); shadow = null;
  }

  window.addEventListener('message', (event: MessageEvent) => {
    if (!active || event.source !== window || event.data?.source !== hookSource) return;
    const hookEvent = event.data.event;
    if (!hookEvent) return;
    if (hookEvent.type === 'network') {
      recentNetwork.push(hookEvent); if (recentNetwork.length > 50) recentNetwork.shift();
    }
    if (hookEvent.type === 'action' && hookEvent.actionType === 'Navigate' && typeof hookEvent.url === 'string')
      recordNavigation(hookEvent.url);
    else record([hookEvent]);
  });

  chrome.runtime.onMessage.addListener((
    message: { source?: string; type?: string; payload?: Record<string, unknown> },
    _sender: any,
    sendResponse: (response: unknown) => void,
  ) => {
    if (message?.source !== extensionSource) return;
    switch (message.type) {
      case 'AZMA_TARGET_ACTIVATE': start(message.payload); break;
      case 'AZMA_TARGET_PAUSE': paused = Boolean(message.payload?.paused); render(); break;
      case 'AZMA_TARGET_PREPARE_STOP':
        flushPendingFills();
        sendResponse({ scenario: clone(scenario) });
        return false;
      case 'AZMA_TARGET_STOP': stop(); break;
    }
    return false;
  });

  chrome.runtime.sendMessage({ source: extensionSource, type: 'target-ready', url: window.location.href }, () => {
    void chrome.runtime.lastError;
  });
})();
