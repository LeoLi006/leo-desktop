// Isolated DOM fixtures: no browser/account/network is contacted.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const code = fs.readFileSync(require('node:path').join(__dirname, '../Assets/gemini-bridge/bridge.js'), 'utf8');
const {parse, clean, siteFor} = require('../Assets/gemini-bridge/bridge.js');
const at = 1720000000;
const link = (prompt='中文 & # ?\nsecond line', id='a'.repeat(32)) => `https://gemini.google.com/app#leo-id=${id}&leo-at=${at}&leo-prompt=${encodeURIComponent(prompt)}`;
assert.equal(parse(link(), at*1000).prompt, '中文 & # ?\nsecond line');
for (const href of [link().replace('gemini.google.com','evil.test'),link().replace('/app#','/other#'),link(''),link('a'.repeat(2001)),link('ok','bad')]) assert.equal(parse(href,at*1000),null);
assert.equal(parse(link(),(at+121)*1000),null);
assert.equal(clean(' x\r\ny\u00a0 '),'x\ny');

async function scenario(options={}) {
  let now=at*1000, clicks=0, inserts=0, panelText='', removed=0, checked=0, notified=false, enters=0;
  const events={}, memory=new Map(), location=new URL(link());
  if(options.provider==='deepseek')location.href=link().replace('https://gemini.google.com/app','https://chat.deepseek.com/');
  if(options.provider==='chatgpt')location.href=link().replace('https://gemini.google.com/app','https://chatgpt.com/');
  function element(){return {style:{},children:[],append(...nodes){this.children.push(...nodes);},attachShadow(){return element();},remove(){removed++;},set textContent(value){panelText=String(value);},get textContent(){return panelText;}};}
  class TextArea {get value(){return this._value||'';}set value(v){this._value=v;}}
  const input={isConnected:true,textContent:options.draft||'',getClientRects:()=>[{}],focus(){},addEventListener(){},removeEventListener(){},dispatchEvent(event){if(event.type==='keydown'){enters++;if(!options.unconfirmed){this.textContent='';if(options.textarea)this.value='';}}else notified=true;}};
  if(options.textarea){Object.setPrototypeOf(input,TextArea.prototype);input.tagName='TEXTAREA';input.value=options.draft||'';}
  const button={isConnected:true,get disabled(){return !!options.disabled || (options.eventRequired&&!notified) || (options.delayed&&now<at*1000+1000);},getClientRects:()=>[{}],
    getAttribute:key=>key==='aria-label'?(options.label||null):null,matches:selector=>!options.semanticOnly&&!options.iconOnly&&!options.unknown,
    contains:other=>!!options.nested&&other!==button,querySelector:selector=>options.deepseekIcon&&selector.includes('viewBox')?{}:options.iconOnly&&!selector.includes('viewBox')?{textContent:'send'}:null,click(){clicks++;if(!options.unconfirmed){input.textContent='';if(options.textarea)input.value='';}}};
  const nested={...button,contains:()=>false};
  const nonSend={...button,getAttribute:key=>key==='aria-label'?'Stop response':null,matches:()=>true};
  const document={documentElement:options.earlyDocument?null:{lang:'zh-CN'},body:{append(){}},visibilityState:options.background?'hidden':'visible',hasFocus:()=>true,
    querySelectorAll(selector){checked++;return options.login?[]:!selector.startsWith('button')?(options.fallback&&selector.startsWith('#prompt-textarea')?[]:options.ambiguous?[input,input]:[input]):options.nested?[button,nested]:options.duplicate?[button,button]:options.withStop?[button,nonSend]:[button];},
    createElement:element,createRange:()=>({selectNodeContents(){}}),execCommand(cmd,unused,prompt){inserts++;input.textContent=prompt;return true;}};
  const window={HTMLTextAreaElement:TextArea,addEventListener:(key,fn)=>events[key]=fn,getSelection:()=>({removeAllRanges(){},addRange(){}})};window.top=window;
  const sessionStorage={getItem:key=>memory.get(key),setItem:(key,value)=>{if(options.storageFailure)throw Error();memory.set(key,value);}};
  const context={document,window,location,navigator:{language:'en',clipboard:{writeText:()=>Promise.resolve()}},history:{state:null,replaceState(a,b,path){location.href=location.origin+path;}},sessionStorage,
    URL,URLSearchParams,Date:{now:()=>now},getComputedStyle:()=>({visibility:'visible'}),InputEvent:function(type){this.type=type;},KeyboardEvent:function(type){this.type=type;},
    setTimeout(fn,ms){now+=ms;queueMicrotask(fn);}};
  vm.runInNewContext(code,context);
  for(let i=0;i<8;i++)await new Promise(resolve=>setImmediate(resolve));
  if(options.replay){location.href=link().replace('https://gemini.google.com/app',options.provider==='deepseek'?'https://chat.deepseek.com/':options.provider==='chatgpt'?'https://chatgpt.com/':'https://gemini.google.com/app');events.hashchange();for(let i=0;i<3;i++)await new Promise(resolve=>setImmediate(resolve));}
  return {clicks,enters,inserts,panelText,removed,checked,hash:location.hash,input:options.textarea?input.value:input.textContent,notified};
}
(async()=>{
  const early=await scenario({earlyDocument:true});assert.equal(early.clicks,1);assert.equal(early.inserts,1);
  const normal=await scenario({replay:true});assert.equal(normal.clicks,1);assert.equal(normal.inserts,1);assert.equal(normal.hash,'');
  const draft=await scenario({draft:'my draft'});assert.equal(draft.clicks,0);assert.equal(draft.input,'my draft');assert.equal(draft.inserts,0);
  const login=await scenario({login:true});assert.equal(login.clicks,0);assert.match(login.panelText,/超时/);assert.ok(login.checked<310);
  const ambiguous=await scenario({ambiguous:true});assert.equal(ambiguous.clicks,0);assert.equal(ambiguous.inserts,0);
  const background=await scenario({background:true});assert.equal(background.inserts,0);assert.equal(background.clicks,0);
  const uncertain=await scenario({unconfirmed:true});assert.equal(uncertain.clicks,1);assert.match(uncertain.panelText,/未确认/);
  for(const label of ['Send message','Send prompt','发送消息','发送']) {
    const semantic=await scenario({semanticOnly:true,label});assert.equal(semantic.clicks,1,`semantic ${label}`);
  }
  const icon=await scenario({iconOnly:true});assert.equal(icon.clicks,1);
  const notification=await scenario({eventRequired:true,semanticOnly:true,label:'Send message'});assert.equal(notification.clicks,1);assert.equal(notification.notified,true);
  const delayed=await scenario({delayed:true,semanticOnly:true,label:'发送消息'});assert.equal(delayed.clicks,1);
  const stop=await scenario({label:'Stop response'});assert.equal(stop.clicks,0);
  const combined=await scenario({withStop:true});assert.equal(combined.clicks,1);
  for(const options of [{disabled:true},{duplicate:true},{unknown:true}]) {
    const blocked=await scenario(options);assert.equal(blocked.clicks,0);assert.match(blocked.panelText,/等待发送按钮超时/);
  }
  const storage=await scenario({storageFailure:true});assert.equal(storage.clicks,0);assert.equal(storage.inserts,0);
  for(const provider of ['deepseek','chatgpt']) {
    for(const textarea of [false,true]) {
      const result=await scenario({provider,textarea,replay:true,eventRequired:true,semanticOnly:true,label:'Send message'});
      assert.equal(result.clicks,1,`${provider} ${textarea} one-shot`);assert.equal(result.hash,'');assert.equal(result.notified,true);
      const existing=await scenario({provider,textarea,draft:'preserve me'});assert.equal(existing.clicks,0);assert.equal(existing.input,'preserve me');
      const timeout=await scenario({provider,textarea,login:true});assert.equal(timeout.clicks,0);assert.match(timeout.panelText,/超时/);
      const uncertain=await scenario({provider,textarea,unconfirmed:true});assert.equal(uncertain.clicks,1);assert.match(uncertain.panelText,/未确认/);
    }
  }
  const key=await scenario({provider:'deepseek',textarea:true,unknown:true});assert.equal(key.clicks,0);assert.equal(key.enters,1);assert.equal(key.input,'');
  const uncertainKey=await scenario({provider:'deepseek',textarea:true,unknown:true,unconfirmed:true});assert.equal(uncertainKey.enters,1);assert.match(uncertainKey.panelText,/未确认/);
  const disabledDs=await scenario({provider:'deepseek',textarea:true,disabled:true});assert.equal(disabledDs.enters,0);assert.equal(disabledDs.clicks,0);
  const nestedDs=await scenario({provider:'deepseek',textarea:true,nested:true});assert.equal(nestedDs.clicks,1);assert.equal(nestedDs.enters,0);
  const fallbackGpt=await scenario({provider:'chatgpt',fallback:true});assert.equal(fallbackGpt.clicks,1);assert.equal(fallbackGpt.inserts,1);
  const dsIcon=await scenario({provider:'deepseek',textarea:true,deepseekIcon:true,unknown:true});assert.equal(dsIcon.clicks,1);
  assert.equal(siteFor('https://chatgpt.com/').id,'chatgpt');assert.equal(siteFor('https://chat.deepseek.com/').id,'deepseek');
  for(const href of ['https://chatgpt.com/c/old','https://chat.deepseek.com/a/chat/s/old','https://chatgpt.com.evil.test/','http://chatgpt.com/'])assert.equal(siteFor(href),null);
  assert.equal(parse(link()+'&leo-site=chatgpt',at*1000),null);
  console.log('PASS: URL validation, Unicode, TTL, one-shot replay, draft protection, login timeout, ambiguous editor, background, uncertain send, storage failure, semantic/icon send controls, input notification, delayed readiness, disabled/ambiguous/stop controls. No live Gemini requests.');
})().catch(error=>{console.error(error);process.exitCode=1;});
