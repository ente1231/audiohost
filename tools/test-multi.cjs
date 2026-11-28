// Real HTTPS/WebSocket clients; the server must be in isolated dry-run mode.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require('playwright');const base='https://127.0.0.1:9443',headers={'X-Panic-Control':'1'};
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
  const owner=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:390,height:844}});
  const guestCtx=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:390,height:844}});
  const admin=await owner.newPage();const errors=[];admin.on('pageerror',e=>errors.push(e.message));admin.setDefaultTimeout(10000);
  async function login(page,user,password){await page.goto(base);await page.locator('#login-user').fill(user);await page.locator('#login-pass').fill(password);await page.locator('#login-btn').click();await page.locator('#app').waitFor({state:'visible'});}
  const snapshot=async()=>await (await owner.request.get(base+'/api/test/snapshot',{headers})).json();
  async function until(check){const end=Date.now()+10000;while(Date.now()<end){const value=await snapshot();if(check(value))return value;await new Promise(r=>setTimeout(r,100));}throw Error('Multi-client state timeout');}
  await login(admin,'testadmin','Local-Test-Only-2026');await admin.locator('[data-tab="admin"]').click();
  await admin.locator('#user-list .user-row').first().waitFor();
  const ids=await admin.evaluate(()=>permItems);const bits=Buffer.alloc(16);
  for(const id of ['capsToggle','jitter','livePreview']){const i=ids.indexOf(id);bits[i>>3]|=1<<(i&7);}
  if(await admin.evaluate(()=>document.getElementById('user-list').textContent.includes('multiguest'))){await admin.evaluate(()=>send({cmd:'admin',op:'del',user:'multiguest'}));await admin.waitForFunction(()=>!document.getElementById('user-list').textContent.includes('multiguest'));}
  await admin.evaluate(p=>send({cmd:'admin',op:'add',user:'multiguest',pw:'Multi-Test-Password',perms:p}),bits.toString('hex'));
  await admin.waitForFunction(()=>document.getElementById('user-list').textContent.includes('multiguest'));
  const tab2=await owner.newPage(),tab3=await owner.newPage(),guest=await guestCtx.newPage();
  for(const page of [tab2,tab3,guest]){page.setDefaultTimeout(10000);page.on('pageerror',e=>errors.push(e.message));}
  await Promise.all([tab2.goto(base),tab3.goto(base),login(guest,'multiguest','Multi-Test-Password')]);
  await Promise.all([tab2.locator('#app').waitFor({state:'visible'}),tab3.locator('#app').waitFor({state:'visible'})]);
  await until(s=>s.connections===4);console.log('PASS: four concurrent browser connections, two accounts');
  await admin.evaluate(()=>send({cmd:'admin',op:'setUserCooldown',user:'testadmin',enabled:true}));
  // Give this test long button cooldowns; restore them before leaving.
  await admin.evaluate(()=>send({cmd:'admin',op:'setCooldowns',seconds:[30,30,30]}));
  await admin.locator('[data-tab="tastatur"]').click();await admin.locator('[data-action="capsToggle"]').click();
  await Promise.all([tab2.waitForFunction(()=>document.querySelector('[data-action="capsToggle"]').disabled),tab3.waitForFunction(()=>document.querySelector('[data-action="capsToggle"]').disabled)]);
  assert(!await guest.locator('[data-action="capsToggle"]').isDisabled(),'Another user has an independent cooldown');
  await guest.locator('[data-tab="tastatur"]').click();await guest.locator('[data-action="capsToggle"]').click();await guest.waitForFunction(()=>document.querySelector('[data-action="capsToggle"]').disabled);
  console.log('PASS: same-account cooldown shared across tabs; different users independent');
  // Parallel starts by the same user correctly share the button cooldown.
  // Disable that user's cooldown explicitly to test two simultaneous leases.
  await admin.evaluate(()=>send({cmd:'admin',op:'setUserCooldown',user:'testadmin',enabled:false}));
  await admin.evaluate(()=>send({cmd:'toggle',name:'circle',value:true}));await guest.evaluate(()=>send({cmd:'toggle',name:'jitter',value:true}));
  await until(s=>s.effects.includes('circle')&&s.effects.includes('jitter'));
  for(const page of [admin,tab2])await page.locator('[data-tab="extras"]').click();
  const frames=[admin,tab2].map(page=>page.waitForResponse(r=>r.url().includes('/api/preview?')&&r.status()===200));
  await Promise.all([admin.locator('[data-action="livePreview"]').click(),tab2.locator('[data-action="livePreview"]').click()]);await Promise.all(frames);
  const lease1=await admin.evaluate(()=>previewLease),lease2=await tab2.evaluate(()=>previewLease);assert.notEqual(lease1,lease2,'Separate leases even with the same cookie');
  await until(s=>s.previews===2);
  await admin.locator('[data-action="livePreview"]').click();await until(s=>s.previews===1);
  await new Promise(r=>setTimeout(r,1000));
  assert.equal((await owner.request.get(base+'/api/preview?screen=0&lease='+lease1,{headers})).status(),403,'Stopped lease rejected');
  assert.equal((await owner.request.get(base+'/api/preview?screen=0&lease='+lease2,{headers})).status(),200,'Other live lease continues');
  console.log('PASS: two simultaneous live previews and independent stopping');
  await tab2.close();await until(s=>s.connections===3&&s.previews===0&&s.effects.includes('circle'));
  await guestCtx.close();await until(s=>s.connections===2&&!s.effects.includes('jitter')&&s.effects.includes('circle'));
  await admin.close();await until(s=>s.connections===1&&s.effects.includes('circle'));
  await tab3.evaluate(()=>send({cmd:'admin',op:'setCooldowns',seconds:[2,5,15]}));
  await tab3.evaluate(()=>send({cmd:'admin',op:'setUserCooldown',user:'testadmin',enabled:true}));
  await tab3.close();await until(s=>s.connections===0&&s.effects.length===0&&s.previews===0);
  assert.deepEqual(errors,[],'No browser errors');
  fs.writeFileSync(path.resolve(__dirname,'../test-results/multi-client.txt'),'PASS: 4 concurrent browser connections, 2 accounts, shared cooldown across same-account tabs, independent cooldowns across users, 2 simultaneous preview leases with same cookie, independent preview stop, disconnect cleanup limited to owner, last-tab and last-client cleanup. All Windows actions simulated.\n');
  console.log('PASS: disconnect only stops its owner; last client resets effects');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
