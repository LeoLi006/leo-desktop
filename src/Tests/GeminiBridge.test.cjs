// Isolated DOM fixtures: no browser/account/network is contacted.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const code = fs.readFileSync(require('node:path').join(__dirname, '../Assets/gemini-bridge/bridge.js'), 'utf8');
const {parse, clean} = require('../Assets/gemini-bridge/bridge.js');
const at = 1720000000;
const link = (prompt='中文 & # ?\nsecond line', id='a'.repeat(32)) => `https://gemini.google.com/app#leo-id=${id}&leo-at=${at}&leo-prompt=${encodeURIComponent(prompt)}`;
assert.equal(parse(link(), at*1000).prompt, '中文 & # ?\nsecond line');
for (const href of [link().replace('gemini.google.com','evil.test'),link().replace('/app#','/other#'),link(''),link('a'.repeat(2001)),link('ok','bad')]) assert.equal(parse(href,at*1000),null);
assert.equal(parse(link(),(at+121)*1000),null);
assert.equal(clean(' x\r\ny\u00a0 '),'x\ny');

async function scenario(options={}) {
  let now=at*1000, clicks=0, inserts=0, panelText='', removed=0, checked=0;
  const events={}, memory=new Map(), location=new URL(link());
  function element(){return {style:{},children:[],append(...nodes){this.children.push(...nodes);},attachShadow(){return element();},remove(){removed++;},set textContent(value){panelText=String(value);},get textContent(){return panelText;}};}
  const input={isConnected:true,textContent:options.draft||'',getClientRects:()=>[{}],focus(){},addEventListener(){},removeEventListener(){},dispatchEvent(){}};
  const button={isConnected:true,disabled:false,getClientRects:()=>[{}],getAttribute:()=>null,click(){clicks++;if(!options.unconfirmed)input.textContent='';}};
  const document={documentElement:{lang:'zh-CN'},body:{append(){}},visibilityState:options.background?'hidden':'visible',hasFocus:()=>true,
    querySelectorAll(selector){checked++;return options.login?[]:selector.startsWith('rich')?(options.ambiguous?[input,input]:[input]):[button];},
    createElement:element,createRange:()=>({selectNodeContents(){}}),execCommand(cmd,unused,prompt){inserts++;input.textContent=prompt;return true;}};
  const window={addEventListener:(key,fn)=>events[key]=fn,getSelection:()=>({removeAllRanges(){},addRange(){}})};window.top=window;
  const sessionStorage={getItem:key=>memory.get(key),setItem:(key,value)=>{if(options.storageFailure)throw Error();memory.set(key,value);}};
  const context={document,window,location,navigator:{language:'en',clipboard:{writeText:()=>Promise.resolve()}},history:{state:null,replaceState(a,b,path){location.href=location.origin+path;}},sessionStorage,
    URL,URLSearchParams,Date:{now:()=>now},getComputedStyle:()=>({visibility:'visible'}),InputEvent:function(){},
    setTimeout(fn,ms){now+=ms;queueMicrotask(fn);}};
  vm.runInNewContext(code,context);
  for(let i=0;i<8;i++)await new Promise(resolve=>setImmediate(resolve));
  if(options.replay){location.href=link();events.hashchange();for(let i=0;i<3;i++)await new Promise(resolve=>setImmediate(resolve));}
  return {clicks,inserts,panelText,removed,checked,hash:location.hash,input:input.textContent};
}
(async()=>{
  const normal=await scenario({replay:true});assert.equal(normal.clicks,1);assert.equal(normal.inserts,1);assert.equal(normal.hash,'');
  const draft=await scenario({draft:'my draft'});assert.equal(draft.clicks,0);assert.equal(draft.input,'my draft');assert.equal(draft.inserts,0);
  const login=await scenario({login:true});assert.equal(login.clicks,0);assert.match(login.panelText,/超时/);assert.ok(login.checked<310);
  const ambiguous=await scenario({ambiguous:true});assert.equal(ambiguous.clicks,0);assert.equal(ambiguous.inserts,0);
  const background=await scenario({background:true});assert.equal(background.inserts,0);assert.equal(background.clicks,0);
  const uncertain=await scenario({unconfirmed:true});assert.equal(uncertain.clicks,1);assert.match(uncertain.panelText,/未确认/);
  const storage=await scenario({storageFailure:true});assert.equal(storage.clicks,0);assert.equal(storage.inserts,0);
  console.log('PASS: URL validation, Unicode, TTL, one-shot replay, draft protection, login timeout, ambiguous editor, background, uncertain send, storage failure. No live Gemini requests.');
})().catch(error=>{console.error(error);process.exitCode=1;});
