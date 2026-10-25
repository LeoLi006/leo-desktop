/* Leo web AI bridge: no network requests, cookies, account access or persisted prompts. */
(() => {
  'use strict';
  const MAX = 2000, TTL = 120;
  const sites = {
    'https://gemini.google.com': { id:'gemini', name:'Gemini', path:/^\/(?:u\/\d+\/)?app\/?$/, editor:'rich-textarea .ql-editor[contenteditable="true"], [contenteditable="true"][role="textbox"]' },
    'https://chat.deepseek.com': { id:'deepseek', name:'DeepSeek', path:/^\/(?:a\/chat\/?)?$/, editor:'textarea#chat-input, textarea[placeholder], [contenteditable="true"][role="textbox"]' },
    'https://chatgpt.com': { id:'chatgpt', name:'ChatGPT', path:/^\/$/, editor:'#prompt-textarea[contenteditable]:not([contenteditable="false"]), textarea#prompt-textarea, textarea[name="prompt-textarea"], textarea#mobile-composer-prompt', fallback:'.ProseMirror[contenteditable]:not([contenteditable="false"]), [data-lexical-editor="true"][contenteditable]:not([contenteditable="false"]), [data-testid="prompt-textarea"][contenteditable]:not([contenteditable="false"])' }
  };
  const siteFor = href => { const url=new URL(href),site=sites[url.origin];return site&&site.path.test(url.pathname)?site:null; };
  const parse = (href, now = Date.now()) => {
    const url = new URL(href), p = new URLSearchParams(url.hash.slice(1));
    const site=siteFor(href);
    if (!site || (p.has('leo-site') && p.get('leo-site')!==site.id)) return null;
    const id = p.get('leo-id'), prompt = p.get('leo-prompt'), at = Number(p.get('leo-at'));
    if (!/^[a-f0-9]{32}$/.test(id || '') || !prompt?.trim() || prompt.length > MAX || !Number.isFinite(at) || Math.abs(now / 1000 - at) > TTL) return null;
    return { id, prompt, site };
  };
  const visible = el => !!el && el.isConnected && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
  const editor = (doc,site=sites['https://gemini.google.com']) => {
    let found = [...doc.querySelectorAll(site.editor)].filter(visible);
    if (!found.length && site.fallback) found=[...doc.querySelectorAll(site.fallback)].filter(visible);
    return found.length === 1 ? found[0] : null;
  };
  const sendButton = (doc,site=sites['https://gemini.google.com']) => {
    const found = [...doc.querySelectorAll(site.id==='deepseek'?'button, [role="button"]':'button')].filter(el => {
      if (!visible(el) || el.disabled || el.getAttribute('aria-disabled') === 'true') return false;
      const label = (el.getAttribute('aria-label') || '').trim();
      // Gemini can keep the same button class while changing its action to Stop/Voice.
      if (/stop|cancel|microphone|voice|停止|取消|麦克风|语音/i.test(label)) return false;
      const semantic = /^(send(?: message| prompt)?|发送(?:消息|提示|提示词)?)$/i.test(label);
      const marker = el.matches('button.send-button, [role="button"][data-testid="send-button"], button[data-test-id="send-button"], button[data-testid="send-button"]'+(site.id==='chatgpt'?', button#composer-submit-button':''));
      const icon = el.querySelector('mat-icon, .google-symbols, .material-symbols-outlined');
      const deepseekIcon=site.id==='deepseek'&&el.querySelector('.ds-icon svg[viewBox="0 0 14 16"], svg[data-icon="send"]');
      return semantic || marker || deepseekIcon || (icon && (icon.textContent || '').trim() === 'send');
    });
    // DeepSeek can nest a role-button around the same actual button.
    const deepest=site.id==='deepseek'?found.filter(el=>!found.some(other=>other!==el&&el.contains(other))):found;
    return deepest.length === 1 ? deepest[0] : null;
  };
  const clean = s => s.replace(/\r\n/g, '\n').replace(/\u00a0/g, ' ').trim();
  const textOf = input => input.tagName==='TEXTAREA' ? input.value : input.textContent || '';
  const fill = (input,prompt,doc,win) => {
    input.focus();
    if(input.tagName==='TEXTAREA') {
      const setter=Object.getOwnPropertyDescriptor(win.HTMLTextAreaElement.prototype,'value')?.set;
      if(!setter)throw new Error('Native textarea setter unavailable');
      setter.call(input,prompt);
    } else {
      const selection=win.getSelection(),range=doc.createRange();range.selectNodeContents(input);selection.removeAllRanges();selection.addRange(range);
      if(!doc.execCommand('insertText',false,prompt))input.textContent=prompt;
    }
    input.dispatchEvent(new InputEvent('input',{bubbles:true,composed:true,inputType:'insertText',data:prompt}));
  };
  // Exposed only in the repository's Node fixture, never to the page world.
  if (typeof module !== 'undefined' && module.exports) { module.exports = { parse, editor, sendButton, clean, siteFor, textOf, fill }; return; }
  if (window.top !== window) return;
  let cancelCurrent = () => {};
  async function launch() {
    cancelCurrent();
    const request = parse(location.href);
    if (!request) return;
    const site=request.site;
    // Consume the hand-off immediately: refresh/back must not send the prompt again.
    history.replaceState(history.state, '', location.pathname + location.search);
    const seenKey = 'leo-gemini-consumed';
    let seen;
    try { seen = JSON.parse(sessionStorage.getItem(seenKey) || '[]'); if (!Array.isArray(seen)) seen = []; }
    catch { return; } // Cannot guarantee deduplication: do not submit.
    if (seen.includes(request.id)) return;
    try { sessionStorage.setItem(seenKey, JSON.stringify([...seen, request.id].slice(-64))); }
    catch { return; }
    let stopped = false, clicked = false, filled = false, panel;
    cancelCurrent = () => { stopped = true; panel?.remove(); };
    const chinese = (document.documentElement?.lang || navigator.language || 'en').startsWith('zh');
    const t = (zh, en) => (chinese ? zh : en).replaceAll('Gemini',site.name);
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
        const input = editor(document,site);
        if (!input) { await sleep(); continue; }
        if (!document.hasFocus() || document.visibilityState !== 'visible') {
          status(t('已找到输入框；请点击 Gemini 网页空白处，Leo 将继续。', 'Composer found. Click an empty area of the Gemini page to continue.'));await sleep();continue;
        }
        if (clean(textOf(input))) { status(t('Gemini 已有草稿，未覆盖或发送。可复制 Leo 内容后手动处理。', 'Gemini contains a draft. Nothing overwritten or sent; copy the Leo prompt if needed.')); return; }
        fill(input,request.prompt,document,window);
        filled = true;
        status(t('Leo 1.1.1 · 已填入，正在等待发送按钮就绪。', 'Leo 1.1.1 · Prompt filled. Waiting for the Send button.'));
        const filledAt=Date.now();
        const onEdit = event => { if (event.isTrusted) { stopped = true; status(t('你正在编辑，已停止自动发送。', 'You are editing. Auto-send stopped.')); } };
        input.addEventListener('beforeinput', onEdit);
        try {
          while (!stopped && Date.now() < deadline) {
            if (!input.isConnected || clean(textOf(input)) !== clean(request.prompt)) {
              status(t('输入框发生变化，已停止自动发送。', 'The editor changed. Auto-send stopped.')); return;
            }
            const button = sendButton(document,site);
            if (!document.hasFocus() || document.visibilityState !== 'visible') { await sleep(); continue; }
            if (!button) {
              // Only DeepSeek: use its composer key handler once, never global keyboard input.
              // Wait for rendering first; never follow a possibly successful click with Enter.
              if(site.id!=='deepseek'||Date.now()-filledAt<1500){await sleep();continue;}
              const blocked=[...document.querySelectorAll('button, [role="button"]')].some(el=>visible(el)&&(
                /stop|停止|停止生成|^(send(?: message| prompt)?|发送(?:消息|提示|提示词)?)$/i.test(el.getAttribute('aria-label')||'') ||
                el.matches('button.send-button, [data-testid="send-button"], [data-test-id="send-button"]') ||
                el.querySelector('.ds-icon svg[viewBox="0 0 14 16"], svg[data-icon="send"]')));
              if(blocked){await sleep();continue;}
              clicked=true;input.focus();
              input.dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',code:'Enter',keyCode:13,which:13,bubbles:true,cancelable:true,composed:true}));
            } else { clicked = true; button.click(); } // Exactly once. Never retry an uncertain send.
            for (let i = 0; i < 25 && !stopped; i++) {
              await sleep();
              if (!input.isConnected || !clean(textOf(input))) {
                status(t('Leo · 已触发发送，请在 Gemini 查看回复。', 'Leo · Send triggered. View the reply in Gemini.'));
                setTimeout(() => panel?.remove(), 2500); return;
              }
            }
            status(t('已点击发送，但未确认页面状态；请查看 Gemini，未自动重试。', 'Send clicked, but page state is unconfirmed. Check Gemini; no automatic retry.')); return;
          }
        } finally { input.removeEventListener('beforeinput', onEdit); }
        break;
      }
      if (!stopped) status(filled ? t('已填入，但等待发送按钮超时；请手动点击发送，Leo 不会重复点击。', 'Prompt filled, but waiting for Send timed out. Click Send manually; Leo will not retry.') : t('等待超时。请确认登录与页面状态；内容未自动重试，可复制后手动发送。', 'Timed out. Check sign-in and page state. No retry; copy the prompt to send manually.'));
    } catch {
      if (!stopped) status(clicked ? t('发送状态未知，请查看 Gemini；不会重复发送。', 'Send state unknown. Check Gemini; no resend.') : t('自动填入未完成，可复制内容手动发送。', 'Auto-fill incomplete. Copy the prompt to send manually.'));
    }
  }
  window.addEventListener('hashchange', launch);
  window.addEventListener('pagehide', () => cancelCurrent());
  launch();
})();
