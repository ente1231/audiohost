# Selbstzerstörung — Knopf, Ablauf, Handbuch

Diese Anleitung beschreibt den neuen Knopf **Selbstzerstörung** in der Handy-Oberfläche: was er entfernt, wie du ihn bedienst, wie du ihn vorher testest und wie du danach kontrollierst, dass nichts übrig ist.

---

## 1. Was der Knopf tut

Ein Druck auf **Selbstzerstörung ausführen** startet am PC ein kurzes Aufräumskript. Es entfernt in dieser Reihenfolge:

| Was | Wo es liegt |
|---|---|
| Geplante Aufgabe | `Microsoft\Windows\Multimedia\SystemAudioCalibration` (Task Scheduler, ONLOGON, alle Benutzer) |
| Autostart-Eintrag (alle Benutzer) | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` → `ClassroomHelperService` |
| Autostart-Eintrag (aktueller Benutzer) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `ClassroomHelper` |
| Defender-Ausnahmen | `Remove-MpPreference` für Programmpfad und Prozessname |
| Firewall-Regel | `PanicControl-Local-8443` |
| Programmordner | `C:\ProgramData\Microsoft\Windows\ClassroomHelper\` (exe, web, actions.json, framedata.dll, relay.url) |
| Sich selbst | die laufende exe und das Aufräumskript |

**Was er nicht anfasst:** Benutzerdateien, Windows-Einstellungen, installierte Programme, `%LOCALAPPDATA%\PanicControl\accounts.json`. Die Kontodatei liegt bewusst außerhalb und bleibt liegen — willst du sie auch weg, löschst du sie danach von Hand (Abschnitt 9).

**Danach:** Der PC ist nicht mehr erreichbar. Der geplante Task startet nichts mehr, der Agent pollt kein Relay mehr, es gibt keinen Autostart. Das ist nicht rückgängig zu machen. Ein neuer Zugang braucht wieder den kompletten USB-Weg.

---

## 2. Wo der Knopf sitzt

1. Am Handy die Oberfläche öffnen (`https://<PC-IP>:8443` oder die Relay-/Cloudflare-Adresse).
2. Mit einem Konto anmelden, das das Recht **selfDestruct** hat (Abschnitt 4).
3. Tab **Extras** öffnen.
4. Ganz unten, nach *PC-Status* und *Anwendung öffnen*, liegt die Karte **Selbstzerstörung**.

Aufbau der Karte:

- eine Zeile Erklärung, was entfernt wird
- ein Textfeld mit Platzhalter `LOESCHEN`
- der rote Knopf **Selbstzerstörung ausführen** mit Zähler `0/5`

---

## 3. Bedienung — die zwei Schlösser

Der Knopf ist absichtlich doppelt gesichert:

**Schloss 1 — das Wort.** In das Textfeld muss exakt `LOESCHEN` stehen. Groß-/Kleinschreibung ist egal, Leerzeichen davor und danach werden ignoriert. Steht dort irgendetwas anderes, kommt nur die Meldung *Erst LOESCHEN eintippen* und es passiert nichts.

**Schloss 2 — fünf Drücke.** Der Knopf zählt fünf Drücke innerhalb von je drei Sekunden. Nach jedem Druck zeigt er `1/5`, `2/5` … und füllt sich. Pause länger als drei Sekunden: Zähler zurück auf null, Meldung *Bestätigung abgebrochen*.

Sind beide Schlösser offen, geht der Befehl an den PC. Der PC zeigt kurz *Selbstzerstörung gestartet*, dann bricht die Verbindung ab. Das Handy bekommt keine Antwort mehr — das ist normal.

---

## 4. Recht vergeben

`selfDestruct` ist ein neues Funktionsrecht. Es wird beim ersten Start nach dem Update **einmalig allen bestehenden Administratoren** angehängt. Andere Konten bekommen es nicht automatisch.

So vergibst du es gezielt:

1. Als Admin anmelden → Tab **Admin**.
2. Beim gewünschten Konto auf **Rechte**.
3. Ganz unten in der Liste steht **selfDestruct** — Haken setzen, speichern.

Ohne dieses Recht ist der Knopf im Extras-Tab für das Konto ausgegraut und der PC lehnt den Befehl zusätzlich serverseitig ab.

---

## 5. Vorher: einmal trocken üben

Bevor du den Knopf auf einem fremden PC drückst, übe auf deinem eigenen:

1. Programm normal starten (`START.bat`).
2. Am Handy anmelden, Extras → Selbstzerstörung.
3. Wort eintippen, einmal drücken → Zähler springt auf `1/5`, nichts weiter passiert.
4. Drei Sekunden warten → *Bestätigung abgebrochen*. Damit ist Schloss 2 geprüft.
5. Feld leeren, Knopf fünfmal drücken → nur die Meldung *Erst LOESCHEN eintippen*. Damit ist Schloss 1 geprüft.

Erst wenn beides so aussieht, hast du die Mechanik verstanden.

---

## 6. Neu bauen und ausliefern

Der Knopf steckt in Code und Oberfläche, also muss neu gebaut werden.

**Normale Version (dist/update):**

```bat
cd C:\pfadzu\pc-app
BUILD.bat
```

Ergebnis landet in `update\` mit der Markierung `READY`. Beim nächsten Start von `START.bat` wird es nach `dist\` übernommen — die laufende App vorher schließen.

**USB-Version (self-contained, ein File):**

```bat
cd C:\pfadzu\pc-app
USB_BUILD.bat
```

Ergebnis in `USB_PAYLOAD\`. Wenn du auf einem Ziel-PC schon eine ältere Version hast, reicht es, `system_drv\ClassroomHelper.exe` und `system_drv\actions.json` zu ersetzen — der Task zeigt auf denselben Pfad.

**Wichtig bei bereits installierten PCs:** `actions.json` muss mit. Ohne den neuen Eintrag kennt der PC das Recht nicht und der Befehl wird abgelehnt.

---

## 7. Am Ziel-PC auslösen

Drei Wege, alle führen zum selben Aufräumen.

**a) Handy-Oberfläche:** Extras → Selbstzerstörung → Wort + fünf Drücke (Abschnitt 3).

**b) Relay, wenn der PC pollt:**

```bash
curl -X POST https://DEINE-RELAY-URL/api/command \
  -H "Content-Type: application/json" \
  -d '{"cmd":"action","action":"selfDestruct"}'
```

**c) Am PC selbst, wenn du davor sitzt:** `system_drv\ClassroomHelper.exe` starten und die Oberfläche lokal über `https://127.0.0.1:8443` bedienen — der Knopf liegt an derselben Stelle.

Nach dem Auslösen läuft das Aufräumen zwei Sekunden verzögert. In dieser Zeit beendet sich das Programm. Danach ist der Ordner weg.

---

## 8. Kontrolle danach

Am PC in einer **Administrator-Eingabeaufforderung** prüfen — alle vier Befehle sollten leer bleiben oder „nicht gefunden" melden:

```bat
schtasks /Query /TN "Microsoft\Windows\Multimedia\SystemAudioCalibration"
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v ClassroomHelperService
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v ClassroomHelper
dir "C:\ProgramData\Microsoft\Windows\ClassroomHelper"
```

Firewall-Regel prüfen:

```bat
netsh advfirewall firewall show rule name="PanicControl-Local-8443"
```

Defender-Ausnahmen prüfen (PowerShell):

```powershell
Get-MpPreference | Select-Object -ExpandProperty ExclusionPath
Get-MpPreference | Select-Object -ExpandProperty ExclusionProcess
```

Steht dort noch `...\ClassroomHelper` bzw. `ClassroomHelper.exe`, ist das Aufräumen an dieser Stelle gescheitert — meistens, weil der Agent **nicht als Administrator** lief. Dann Abschnitt 9.

---

## 9. Reste von Hand entfernen

Falls etwas übrig bleibt (Rechte, gesperrte Datei, abgebrochenes Skript), in einer **Admin-Eingabeaufforderung**:

```bat
schtasks /Delete /F /TN "Microsoft\Windows\Multimedia\SystemAudioCalibration"
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v ClassroomHelperService /f
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v ClassroomHelper /f
netsh advfirewall firewall delete rule name="PanicControl-Local-8443"
powershell -NoProfile -Command "Remove-MpPreference -ExclusionPath 'C:\ProgramData\Microsoft\Windows\ClassroomHelper' -ExclusionProcess 'ClassroomHelper.exe'"
attrib -h -s "C:\ProgramData\Microsoft\Windows\ClassroomHelper" /s /d
rmdir /s /q "C:\ProgramData\Microsoft\Windows\ClassroomHelper"
```

Kontodatei zusätzlich löschen, wenn sie auch weg soll:

```bat
rmdir /s /q "%LOCALAPPDATA%\PanicControl"
```

Läuft `ClassroomHelper.exe` noch (im Task-Manager sichtbar, weil `framedata.dll` nicht geladen war):

```bat
taskkill /F /IM ClassroomHelper.exe
```

Danach die Befehle aus Abschnitt 9 noch einmal ausführen.

---

## 10. Wenn der Knopf nicht funktioniert

| Symptom | Ursache | Lösung |
|---|---|---|
| Knopf ist ausgegraut | Konto hat das Recht `selfDestruct` nicht | Admin-Tab → Rechte → Haken setzen |
| *Erst LOESCHEN eintippen* trotz richtigem Wort | Leerzeichen im Feld oder falsche Schreibweise | Feld leeren, `LOESCHEN` neu tippen |
| Zähler springt immer auf 0 | mehr als drei Sekunden zwischen den Drücken | zügiger drücken |
| Befehl kommt an, PC reagiert nicht | alte `actions.json` auf dem PC | neue `actions.json` mitkopieren, Programm neu starten |
| Ordner bleibt nach dem Aufräumen | Agent lief ohne Adminrechte | Abschnitt 9 manuell |
| `Zugriff verweigert` beim Löschen | exe noch gesperrt | 10 Sekunden warten, dann Abschnitt 9 wiederholen |

---

## 11. Technisch, falls du es nachlesen willst

- Recht: `selfDestruct`, Kategorie 3 (schwer), Index 135 in `actions.json`.
- Oberfläche: Karte `#extra-danger` in `web/index.html`, Textfeld `#self-destruct-word`, Knopf mit `data-confirm="5"`.
- Prüfung im Browser: `web/app.js`, Funktion `executeAction`, erster Zweig.
- Server: `ControlState.Handle` → `RequestSelfDestruct()` → `NativeWindows.SelfDestruct()`.
- Aufräumskript: wird als `%TEMP%\pc-cleanup-<zufall>.cmd` geschrieben, läuft abgekoppelt, löscht sich am Ende selbst.
- Relay-Weg: derselbe Befehl über `Program.cs`, `StealthAgent`-Poll → `state.RequestSelfDestruct()`.

Der Knopf ist die letzte Aktion in der Liste. Neue Rechte werden beim Start automatisch an bestehende Administratoren angehängt, andere Konten bleiben unberührt.
