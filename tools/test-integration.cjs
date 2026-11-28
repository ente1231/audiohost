const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const https=require('node:https');
const {chromium}=require('playwright');
const base='https://127.0.0.1:9443';
const results=path.resolve(__dirname,'../test-results');fs.mkdirSync(results,{recursive:true});
function request(route,{method='GET',headers={},body}={}){return new Promise((resolve,reject)=>{const r=https.request(base+route,{method,rejectUnauthorized:false,headers},res=>{let data='';res.on('data',d=>data+=d);res.on('end',()=>resolve({status:res.statusCode,headers:res.headers,body:data}));});r.on('error',reject);r.end(body);});}
(async()=>{
 assert.equal((await request('/api/stats',{headers:{'X-Panic-Control':'1'}})).status,401);
 assert.equal((await request('/api/login',{method:'POST',headers:{'Content-Type':'application/json','X-Panic-Control':'1',Origin:'https://evil.example'},body:'{}'})).status,403,'Foreign origin rejected');
 assert.equal((await request('/api/login',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'})).status,403,'CSRF header required');
 assert.equal((await request('/',{headers:{Host:'evil.example:9443'}})).status,403,'DNS rebinding host rejected');
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
  const ctx=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:390,height:844},isMobile:true,hasTouch:true});
  const page=await ctx.newPage();page.setDefaultTimeout(10000);const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(base);await page.locator('#login-user').fill('testadmin');await page.locator('#login-pass').fill('Local-Test-Only-2026');await page.locator('#login-btn').click();
  await page.locator('#app').waitFor({state:'visible'});
  console.log('PASS: HTTPS login and session');
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'No phone overflow');
  const cookie=(await ctx.cookies()).find(c=>c.name==='panic_session');assert(cookie.httpOnly&&cookie.secure&&cookie.sameSite==='Strict','Protected cookie');
  assert(!await page.evaluate(()=>document.cookie.includes('panic_session')),'Session not readable by JS');
  await page.screenshot({path:path.join(results,'phone-quick.png'),fullPage:true});
  await page.locator('.quick-strip [data-action="virtDesktop"]').click();
  await page.waitForFunction(()=>document.querySelector('.quick-strip [data-action="virtDesktop"]').disabled);
  assert(await page.locator('[data-action="virtDesktop"]').nth(1).isDisabled(),'Duplicate quick button shares cooldown');
  await page.locator('[data-tab="maus"]').click();
  const rect=await page.locator('#mouse-pad').boundingBox();await page.mouse.move(rect.x+50,rect.y+80);await page.mouse.down();
  await page.waitForFunction(()=>padReady);await page.mouse.move(rect.x+100,rect.y+110,{steps:6});await page.mouse.up();
  assert(!await page.evaluate(()=>padReady),'Pad ends');
  await page.screenshot({path:path.join(results,'phone-mouse.png'),fullPage:true});
  await page.locator('[data-tab="extras"]').click();await page.locator('[data-action="pcStats"]').click();await page.waitForFunction(()=>document.getElementById('pc-stats').textContent.includes('CPU'));
  await page.waitForFunction(()=>document.getElementById('screen-select').options.length===2);await page.locator('#screen-select').selectOption('1');
  await page.locator('[data-action="screenshotView"]').click();await page.locator('#screen-preview').waitFor({state:'visible'});assert(await page.locator('#screen-preview').evaluate(i=>i.naturalWidth>0),'Secondary screenshot preview decodes');
  await page.locator('[data-action="windowList"]').click();await page.waitForFunction(()=>document.getElementById('window-select').value==='test-window');
  assert.equal(await page.locator('#window-select option').textContent(),'Testfenster <script>','Window titles stay plain text');
  await page.locator('[data-action="windowFocus"]').click();await page.locator('[data-action="windowMinimize"]').click();
  await page.locator('[data-action="clipboardRead"]').click();await page.waitForFunction(()=>document.getElementById('clipboard-text').value==='Test-Zwischenablage');
  await page.locator('#clipboard-text').fill('Text vom Handy <script>');await page.locator('[data-action="clipboardWrite"]').click();await page.locator('[data-action="clipboardWrite"]').click();
  await page.locator('#pc-message').fill('Bin gleich wieder da!');await page.locator('[data-action="showMessage"]').click();
  await page.waitForFunction(()=>document.querySelector('[data-action="showMessage"]').disabled);
  // Live preview has its own lease and start cooldown. Stop remains available.
  assert.equal((await ctx.request.get(base+'/api/preview?screen=1',{headers:{'X-Panic-Control':'1'}})).status(),403,'Cannot fetch a live frame before starting');
  const liveFrame=page.waitForResponse(r=>r.url().includes('/api/preview?')&&r.status()===200);await page.locator('[data-action="livePreview"]').click();await liveFrame;await page.waitForFunction(()=>previewActive&&document.getElementById('screen-preview').naturalWidth>0);
  assert(!await page.locator('[data-action="livePreview"]').isDisabled(),'Preview stop stays usable during its start cooldown');
  await page.waitForFunction(()=>document.getElementById('preview-status').textContent.includes('Live'));
  await page.locator('[data-action="livePreview"]').click();await page.waitForFunction(()=>!previewActive);
  assert.equal((await ctx.request.get(base+'/api/preview?screen=1',{headers:{'X-Panic-Control':'1'}})).status(),403,'Stopping removes frame access');
  await page.locator('#countdown-seconds').fill('45');await page.locator('[data-action="countdown"]').click();
  await page.locator('#screen-color').selectOption('white');await page.locator('[data-action="screenColor"]').click();await page.locator('[data-action="desktopClock"]').click();
  let displaySnapshot=await (await ctx.request.get(base+'/api/test/snapshot',{headers:{'X-Panic-Control':'1'}})).json();
  for(const id of ['showMessage','countdown','screenColor','desktopClock'])assert(displaySnapshot.overlays.includes(id),'Display routed: '+id);
  await page.locator('[data-action="closeOverlays"]').click();await page.waitForFunction(()=>document.getElementById('toast').textContent.includes('Aktion'));
  displaySnapshot=await (await ctx.request.get(base+'/api/test/snapshot',{headers:{'X-Panic-Control':'1'}})).json();assert.equal(displaySnapshot.overlays.length,0,'Display recovery closes everything');
  for(const id of ['browserNewTab','tabNext','browserZoomReset','openCalculator','openNotepad','mediaStop']){await page.locator('[data-action="'+id+'"]').click();await page.waitForTimeout(100);}
  const extraSnapshot=await (await ctx.request.get(base+'/api/test/snapshot',{headers:{'X-Panic-Control':'1'}})).json();
  for(const id of ['windowFocus','windowMinimize','clipboardWrite','showMessage'])assert(extraSnapshot.simulated.includes(id),'Extra routed: '+id);
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Extras fit the phone');
  await page.screenshot({path:path.join(results,'phone-extras.png'),fullPage:true});
  await page.locator('#extra-overlays').screenshot({path:path.join(results,'phone-overlays.png')});
  await page.locator('#extra-screen').screenshot({path:path.join(results,'phone-live-preview.png')});
  console.log('PASS: touchpad, screens, window controls, clipboard, live preview, fullscreen display routing, recovery, tools and browser controls');
  await page.locator('[data-tab="admin"]').click();await page.locator('#user-list .user-row').first().waitFor();
  const ids=await page.evaluate(()=>permItems);const perm=(wanted)=>{const b=Buffer.alloc(16);for(const id of wanted){const i=ids.indexOf(id);assert(i>=0);b[i>>3]|=1<<(i&7);}return b.toString('hex');};
  if(await page.evaluate(()=>document.getElementById('user-list').textContent.includes('phoneguest'))){await page.evaluate(()=>send({cmd:'admin',op:'del',user:'phoneguest'}));await page.waitForFunction(()=>!document.getElementById('user-list').textContent.includes('phoneguest'));}
  await page.evaluate(p=>send({cmd:'admin',op:'add',user:'phoneguest',pw:'Guest-Test-Password',perms:p}),perm(['capsToggle','jitter','mouseMove']));
  await page.waitForFunction(()=>document.getElementById('user-list').textContent.includes('phoneguest'));
  console.log('PASS: guest created');
  const guestCtx=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:390,height:844}});const guest=await guestCtx.newPage();guest.setDefaultTimeout(10000);guest.on('pageerror',e=>errors.push(e.message));
  await guest.goto(base);await guest.locator('#login-user').fill('phoneguest');await guest.locator('#login-pass').fill('Guest-Test-Password');await guest.locator('#login-btn').click();await guest.locator('#app').waitFor({state:'visible'});
  console.log('PASS: guest login');
  for(const route of ['/api/screens','/api/screenshot?screen=1','/api/windows','/api/clipboard','/api/preview/screens','/api/preview?screen=1'])assert.equal((await guestCtx.request.get(base+route,{headers:{'X-Panic-Control':'1'}})).status(),403,'Guest cannot access '+route);
  await guest.evaluate(()=>send({cmd:'action',action:'clipboardWrite',text:'unauthorized'}));await guest.waitForFunction(()=>document.getElementById('toast').textContent.includes('Berechtigung'));
  // A grant starts working after reload; revocation stops the live lease at once.
  await page.evaluate(p=>send({cmd:'admin',op:'setPerms',user:'phoneguest',perms:p}),perm(['capsToggle','jitter','mouseMove','livePreview']));
  await guest.locator('#permission-notice').waitFor({state:'visible'});await guest.locator('[data-tab="extras"]').waitFor({state:'visible'});await guest.locator('[data-tab="extras"]').click();
  const guestFrame=guest.waitForResponse(r=>r.url().includes('/api/preview?')&&r.status()===200);await guest.locator('[data-action="livePreview"]').click();await guestFrame;assert(await guest.evaluate(()=>previewActive));
  const guestLease=await guest.evaluate(()=>previewLease);
  await page.evaluate(p=>send({cmd:'admin',op:'setPerms',user:'phoneguest',perms:p}),perm(['capsToggle','jitter','mouseMove']));
  await guest.locator('#permission-notice').waitFor({state:'visible'});assert.equal((await guestCtx.request.get(base+'/api/preview?screen=0&lease='+guestLease,{headers:{'X-Panic-Control':'1'}})).status(),403,'Revocation removes an active live lease immediately');
  await guest.locator('#app').waitFor({state:'visible'});
  assert(await guest.locator('[data-action="shutdown"]').evaluate(e=>e.classList.contains('hidden')));
  await guest.evaluate(()=>send({cmd:'action',action:'shutdown'}));await guest.waitForFunction(()=>document.getElementById('toast').textContent.includes('Berechtigung'));
  assert(!(await (await ctx.request.get(base+'/api/test/snapshot',{headers:{'X-Panic-Control':'1'}})).json()).simulated.includes('shutdown'),'Forged command denied');
  await guest.locator('[data-tab="tastatur"]').click();await guest.locator('[data-action="capsToggle"]').click();await guest.waitForFunction(()=>document.querySelector('[data-action="capsToggle"]').disabled);
  await page.evaluate(p=>send({cmd:'admin',op:'setPerms',user:'phoneguest',perms:p}),perm(['mouseMove']));
  await guest.locator('#permission-notice').waitFor({state:'visible'});await guest.waitForFunction(()=>!document.getElementById('app').classList.contains('hidden'),{},{timeout:10000});
  assert(await guest.locator('[data-action="capsToggle"]').evaluate(e=>e.classList.contains('hidden')),'Permissions automatically refreshed');
  await page.locator('#cooldown-users input[data-user="phoneguest"]').uncheck();await page.waitForFunction(()=>!document.querySelector('#cooldown-users input[data-user="phoneguest"]').checked);
  await guest.reload();await guest.locator('#app').waitFor({state:'visible'});assert(await guest.evaluate(()=>myUser==='phoneguest'),'Cookie resumes on reload');
  await page.evaluate(()=>send({cmd:'admin',op:'setPass',user:'phoneguest',pw:'New-Guest-Password'}));await guest.locator('#login-overlay').waitFor({state:'visible'});
  const response=await ctx.request.get(base+'/api/test/snapshot',{headers:{'X-Panic-Control':'1'}});const snapshot=await response.json();assert(snapshot.simulated.includes('virtDesktop'),'Real server routed desktop action into dry-run adapter');
  await page.screenshot({path:path.join(results,'phone-admin.png'),fullPage:true});
  await page.emulateMedia({reducedMotion:'reduce'});
  assert.equal(await page.locator('.header').evaluate(e=>getComputedStyle(e).animationName),'none','Reduced motion disables the new animation');
  assert.equal(await page.locator('.tab-content.active').evaluate(e=>getComputedStyle(e).animationName),'none','Reduced motion disables tab animation');
  assert.deepEqual(errors,[],'No browser runtime errors');
  await page.locator('#logout-btn').click();await page.locator('#login-overlay').waitFor({state:'visible'});assert.equal((await ctx.request.get(base+'/api/stats',{headers:{'X-Panic-Control':'1'}})).status(),401,'Logout invalidates session');
  // Test the per-address login limiter without changing Windows.
  let rateLimited=false;for(let i=0;i<6;i++){const r=await request('/api/login',{method:'POST',headers:{'Content-Type':'application/json','X-Panic-Control':'1',Origin:base},body:JSON.stringify({user:'nobody',password:'not-the-password'})});if(r.status===429)rateLimited=true;}
  assert(rateLimited,'Login throttling');
  fs.writeFileSync(path.join(results,'integration.txt'),'PASS: HTTPS, origin/CSRF/host checks, HttpOnly cookie, login/logout, authorization, shared cooldowns, touchpad, secondary screenshots, window controls, clipboard, explicit live-preview start/stop, overlay routing/recovery, browser/tools, extras denied to guests, accounts, permission refresh, password invalidation, throttling, mobile layout. No real PC actions executed.\n');
  console.log('PASS: local server and mobile UI integration; no real PC actions.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
