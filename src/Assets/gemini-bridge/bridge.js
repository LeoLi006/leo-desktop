/* Leo Gemini bridge: no network requests, cookies, account access or persisted prompts. */
(() => {
  'use strict';
  const MAX = 2000, TTL = 120;
  const parse = (href, now = Date.now()) => {
    const url = new URL(href), p = new URLSearchParams(url.hash.slice(1));
    if (url.origin !== 'https://gemini.google.com' || !/^\/(?:u\/\d+\/)?app\/?$/.test(url.pathname)) return null;
    const id = p.get('leo-id'), prompt = p.get('leo-prompt'), at = Number(p.get('leo-at'));
    if (!/^[a-f0-9]{32}$/.test(id || '') || !prompt?.trim() || prompt.length > MAX || !Number.isFinite(at) || Math.abs(now / 1000 - at) > TTL) return null;
    return { id, prompt };
  };
  const visible = el => !!el && el.isConnected && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
  const editor = doc => {
    const found = [...doc.querySelectorAll('rich-textarea .ql-editor[contenteditable="true"], [contenteditable="true"][role="textbox"]')].filter(visible);
    return found.length === 1 ? found[0] : null;
  };
  const sendButton = doc => {
    const found = [...doc.querySelectorAll('button.send-button, button[data-test-id="send-button"]')].filter(visible);
    return found.length === 1 && !found[0].disabled && found[0].getAttribute('aria-disabled') !== 'true' ? found[0] : null;
  };
  const clean = s => s.replace(/\r\n/g, '\n').replace(/\u00a0/g, ' ').trim();
  // Exposed only in the repository's Node fixture, never to the page world.
  if (typeof module !== 'undefined' && module.exports) { module.exports = { parse, editor, sendButton, clean }; return; }
  if (window.top !== window) return;
  let cancelCurrent = () => {};
  async function launch() {
    cancelCurrent();
    const request = parse(location.href);
    if (!request) return;
    // Consume the hand-off immediately: refresh/back must not send the prompt again.
    history.replaceState(history.state, '', location.pathname + location.search);
    const seenKey = 'leo-gemini-consumed';
    let seen;
    try { seen = JSON.parse(sessionStorage.getItem(seenKey) || '[]'); if (!Array.isArray(seen)) seen = []; }
    catch { return; } // Cannot guarantee deduplication: do not submit.
    if (seen.includes(request.id)) return;
    try { sessionStorage.setItem(seenKey, JSON.stringify([...seen, request.id].slice(-64))); }
    catch { return; }
    let stopped = false, clicked = false, panel;
    cancelCurrent = () => { stopped = true; panel?.remove(); };
    const chinese = (document.documentElement?.lang || navigator.language || 'en').startsWith('zh');
    const t = (zh, en) => chinese ? zh : en;
    const sleep = () => new Promise(resolve => setTimeout(resolve, 200));
    const deadline = Date.now() + 60000;
    const status = message => {
      if (!document.body) return;
      if (!panel) {
        panel = document.createElement('div');
        panel.style.cssText = 'position:fixed;right:20px;bottom:24px;z-index:2147483647;max-width:340px';
        const root = panel.attachShadow({ mode: 'closed' });
        const box = document.createElement('div');
        box.style.cssText = 'font:14px/1.5 system-ui;padding:12px 16px;border-radius:14px;background:#102139;color:#edf2ff;border:1px solid #d7b774;box-shadow:0 8px 24px #0005';
        const label = document.createElement('div'); label.id = 'status'; box.append(label);
        for (const [name, action] of [
          [t('复制内容', 'Copy prompt'), () => navigator.clipboard.writeText(request.prompt).catch(() => { label.textContent = t('复制失败，请回到 Leo 重新输入。', 'Copy failed. Return to Leo to enter your prompt again.'); })],
          [t('取消 / 关闭', 'Cancel / close'), () => { stopped = true; panel?.remove(); }]
        ]) { const button = document.createElement('button'); button.textContent = name; button.style.cssText = 'margin:8px 8px 0 0;cursor:pointer'; button.onclick = action; box.append(button); }
        root.append(box); document.body.append(panel); panel.update = value => { label.textContent = value; };
      }
      panel.update(message);
    };
    try {
      while (!stopped && Date.now() < deadline) {
        status(t('Leo · 正在等待 Gemini 输入框，可取消。请先完成登录。', 'Leo · Waiting for Gemini. Sign in if needed; cancel anytime.'));
        const input = editor(document);
        if (!input || !document.hasFocus() || document.visibilityState !== 'visible') { await sleep(); continue; }
        if (clean(input.textContent || '')) { status(t('Gemini 已有草稿，未覆盖或发送。可复制 Leo 内容后手动处理。', 'Gemini contains a draft. Nothing overwritten or sent; copy the Leo prompt if needed.')); return; }
        input.focus();
        // Target only the verified editor. No keyboard/clipboard injection into other windows.
        const selection = window.getSelection(), range = document.createRange();
        range.selectNodeContents(input); selection.removeAllRanges(); selection.addRange(range);
        if (!document.execCommand('insertText', false, request.prompt)) {
          input.textContent = request.prompt;
          input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: request.prompt }));
        }
        const onEdit = event => { if (event.isTrusted) { stopped = true; status(t('你正在编辑，已停止自动发送。', 'You are editing. Auto-send stopped.')); } };
        input.addEventListener('beforeinput', onEdit);
        try {
          while (!stopped && Date.now() < deadline) {
            if (!input.isConnected || clean(input.textContent || '') !== clean(request.prompt)) {
              status(t('输入框发生变化，已停止自动发送。', 'The editor changed. Auto-send stopped.')); return;
            }
            const button = sendButton(document);
            if (!button || !document.hasFocus() || document.visibilityState !== 'visible') { await sleep(); continue; }
            clicked = true; button.click(); // Exactly once. Never retry an uncertain send.
            for (let i = 0; i < 25 && !stopped; i++) {
              await sleep();
              if (!input.isConnected || !clean(input.textContent || '')) {
                status(t('Leo · 已触发发送，请在 Gemini 查看回复。', 'Leo · Send triggered. View the reply in Gemini.'));
                setTimeout(() => panel?.remove(), 2500); return;
              }
            }
            status(t('已点击发送，但未确认页面状态；请查看 Gemini，未自动重试。', 'Send clicked, but page state is unconfirmed. Check Gemini; no automatic retry.')); return;
          }
        } finally { input.removeEventListener('beforeinput', onEdit); }
        break;
      }
      if (!stopped) status(t('等待超时。请确认登录与页面状态；内容未自动重试，可复制后手动发送。', 'Timed out. Check sign-in and page state. No retry; copy the prompt to send manually.'));
    } catch {
      if (!stopped) status(clicked ? t('发送状态未知，请查看 Gemini；不会重复发送。', 'Send state unknown. Check Gemini; no resend.') : t('自动填入未完成，可复制内容手动发送。', 'Auto-fill incomplete. Copy the prompt to send manually.'));
    }
  }
  window.addEventListener('hashchange', launch);
  window.addEventListener('pagehide', () => cancelCurrent());
  launch();
})();
