# Panic Control – Windows-Version fürs lokale WLAN

> **Verteilung ohne AV-Treffer:** `pack.ps1` baut einen verschlüsselten Container;
> Download und Installation laufen über `Stage.txt` + `assets.dat`, nicht mehr über
> ein ZIP mit EXE. Aufbau, Begründung und die echten AVG-Treffer stehen in
> **[ANTI-AV.md](ANTI-AV.md)**, die Einsteck-Mechanik in **[HID-BOARD.md](HID-BOARD.md)**.


Diese Version liegt vollständig in `pc-app`. Die ESP32-Firmware, deren Tools und `bin/firmware.bin` bleiben unverändert. Es wird kein ESP32 benötigt. PC und Handy müssen im selben privaten WLAN/LAN sein; kein Cloud-Server, VPN oder Internetkonto.

Die Handy-Oberfläche nutzt Graphit, Limettengrün, Türkis und Korallrot. Nur Farben, Oberflächen und kurze CSS-Animationen wurden geändert; Layout und Bedienung bleiben erhalten. Keine neuen Bilder, Schrift-Downloads oder UI-Bibliotheken. `theme.css` enthält die separate Farb- und Bewegungsanpassung. Die Systemeinstellung „Bewegung reduzieren“ schaltet die Animationen ab.

## Zwei Betriebswege

`README.md` beschreibt den lokalen WLAN-Modus. Der zweite Weg — Relay im Internet, Agent auf dem Ziel-PC, Steuerung aus einem beliebigen Netz, USB-Verteilung und Zero-Click-HID — steht vollständig in **[DEPLOY.md](DEPLOY.md)**.

## Starten

1. `START.bat` doppelklicken. Die fertige Version befindet sich in `dist/ClassroomHelper.exe` (der Starter findet auch noch ein altes `dist/PanicControl.exe`).
2. Beim ersten Start einen Admin-Namen und ein Passwort mit 8–64 Zeichen festlegen. Keine fest eingebauten Standard-Zugangsdaten.
3. Das passende WLAN/LAN im PC-Fenster auswählen und **Fernzugriff starten** drücken.
4. Auf dem Handy die im PC-Fenster angezeigte Adresse öffnen, beispielsweise `https://192.168.178.50:8443`.
5. Das Programm benutzt ein eigenes lokales HTTPS-Zertifikat. Der Browser zeigt beim ersten Öffnen eine Zertifikatswarnung. Über die Zertifikatsdetails den SHA-256-Fingerabdruck mit dem PC-Fenster vergleichen und ausschließlich für diese lokale Adresse fortfahren. Es wird kein systemweiter Zertifikatsvertrauensschutz abgeschaltet. Bei einer geänderten PC-IP kann erneut eine Warnung erscheinen.
6. Mit dem selbst angelegten Account anmelden. Weitere Accounts, Rechte und Cooldowns befinden sich im Admin-Tab.

Auf diesem PC sind die benötigten .NET-8-Laufzeitkomponenten bereits vorhanden. Auf einem anderen PC werden die **.NET 8 Desktop Runtime** und die **ASP.NET Core Runtime 8** benötigt (gleiche Architektur, normalerweise x64). Das .NET SDK ist nur zum erneuten Bauen erforderlich. [Offizielle Downloads](https://dotnet.microsoft.com/download/dotnet/8.0).

Das Fenster kann normal minimiert werden. Eine sichtbare Anzeige im Infobereich erlaubt jederzeit das Öffnen und Stoppen. Schließen beendet das Programm. Es gibt keinen automatischen Start mit Windows.

Ein bereitgelegtes Update in `update` mit der Markierung `READY` wird beim nächsten Aufruf von `START.bat` nach `dist` kopiert. Dazu muss das bisherige Programm vollständig geschlossen sein. Der Starter beendet keine laufende App. Der Programmpfad und die darauf bezogene Firewall-Regel bleiben gleich; Kontodaten liegen weiterhin außerhalb von `dist`.

## Wenn das Handy keine Verbindung bekommt

Die Windows-Firewall muss das Programm im **privaten** Netzwerk erlauben. Das Netzwerkprofil in Windows muss zum tatsächlich vertrauenswürdigen Heimnetz passen. Die Firewall wurde bei der Erstellung nicht verändert.

Bei Bedarf `FIREWALL-PRIVAT.ps1` am PC ausführen: Das Skript fordert normale Windows-Administratorrechte an und erlaubt ausschließlich `dist/ClassroomHelper.exe`, TCP 8443, private Netzwerke und das lokale Subnetz. Es öffnet keinen Router-Port und erlaubt kein öffentliches Netzwerk. Die Regel heißt `PanicControl-Local-8443` und kann in der Windows-Firewall wieder entfernt werden.

PC und Handy dürfen nicht in einem Gast-WLAN mit Geräte-Isolation sein. Bei mehreren Netzwerkkarten die Adresse des gemeinsamen Heimnetzes auswählen. Der PC muss wach bleiben; nach Sperren, Schlafmodus oder Ausschalten kann das Programm den Sicherheitsbildschirm nicht fernbedienen oder den PC wieder aufwecken.

## Funktionen und Extras

Die 82 bisherigen Funktionsrechte sind in derselben Reihenfolge übernommen. Die bisherigen Quick-, Maus-, Tastatur-, System-, Windows- und Browser-Tabs bleiben erhalten; dazu kommt **Extras**. Individuelle Rechte und gemeinsame Kategorien-Cooldowns mit Auswahl pro Account funktionieren weiterhin. Jede Aktion wird am PC autorisiert; Rechteentzug zeigt dem betroffenen Account sofort eine Meldung und lädt seine Seite neu.

Alle zusätzlichen Funktionen sind bereits nach **leicht/mittel/schwer** eingestuft: [vollständige Liste der PC-Extras](../docs/pc-extra-funktionsklassen.md). Countdown, Uhr und Einzel-Screenshot sind leicht; Live-Vorschau, Fenstersteuerung und Zwischenablage mittel. Rettungsaktionen sind ohne Cooldown. Die Zusatzfunktionen enthalten derzeit keine schweren Aktionen; bestehende Funktionen wie Herunterfahren und Filter Keys bleiben schwer.

**Mehrere Handys/Browser gleichzeitig:** bis zu 16 aktive Verbindungen und 12 angelegte Benutzerkonten; maximal vier Live-Vorschauen. Mehrere Tabs desselben Benutzers teilen dessen Button-Cooldown, andere Benutzer haben eigene Cooldowns. Zwei Live-Vorschauen desselben Kontos benötigen entweder zeitlich versetzte Starts oder deaktivierten Benutzer-Cooldown. Maus- und Tastatureingaben steuern denselben PC und können sich bei gleichzeitigem Bedienen überlagern. Beim Trennen eines Handys werden die Effekte dieses Benutzers erst beendet, wenn seine letzte Verbindung getrennt ist; andere Benutzer bleiben verbunden.

Insgesamt gibt es 42 zusätzliche Funktionsrechte. Die neueste Erweiterung ergänzt 24 davon:

| Funktion | Verhalten |
|---|---|
| Desktop links / rechts | Zwischen virtuellen Windows-Desktops wechseln |
| Desktop schließen | Aktuellen virtuellen Desktop schließen, mit Bestätigung |
| Herunterfahren abbrechen | Den noch laufenden Shutdown-Countdown abbrechen |
| Screenshot-Vorschau | Monitor auswählen, einzelnes JPEG aufnehmen und auf dem Handy speichern |
| PC-Auslastung | CPU, RAM, Rechnername und Laufzeit abrufen |
| Anwendung öffnen | Genau die im PC-Fenster ausgewählte `.exe` ohne zusätzliche Argumente starten |
| Browser-Vollbild | F11 ans fokussierte Programm senden |
| Fensterliste / Fenstersteuerung | Offene Anwendungen auswählen, nach vorne holen, minimieren, maximieren oder normal anzeigen |
| Zwischenablage lesen / schreiben | Bis zu 4096 Zeichen ausdrücklich zwischen PC und Handy übertragen; keine automatische Synchronisierung |
| Nachricht auf dem PC | Vollbild im Vordergrund, maximal 240 Zeichen, schließt nach 15 Sekunden |
| Alle minimieren / zurückholen | Win+M bzw. Win+Umschalt+M |
| Live-Bildschirmvorschau | Explizit starten/stoppen, ungefähr ein Bild pro Sekunde, ausgewählter Monitor, maximal 960 Pixel breit |
| Schwarzer Bildschirm | Schließbare schwarze Vollbild-Anzeige für 30 Sekunden; schaltet den Monitor nicht elektrisch aus |
| Große Uhr | Uhrzeit und Datum im Vollbild, maximal zwei Minuten |
| Countdown | 1–600 Sekunden einstellen, endet mit „Zeit abgelaufen“, schließt fünf Sekunden später |
| Monitor-Farbtest | Rot, Grün, Blau, Weiß oder Schwarz, statische Vollbild-Anzeige für 30 Sekunden |
| Anzeigen schließen | Alle vom Programm geöffneten Vollbild-Anzeigen sofort schließen, ohne Cooldown |
| Fenster anordnen / Monitor wechseln | Aktives Fenster links/rechts einrasten oder auf den nächsten Monitor verschieben |
| Browser-Extras | Neuer Tab, vorheriger/nächster Tab, normales/neues Laden ohne Cache, Zoom erhöhen/auf 100 % setzen |
| Schnelle Tools | Explorer, Rechner, Editor und Windows-Einstellungen öffnen |
| Emoji / Bildschirmausschnitt | Windows-Emoji-Auswahl oder Auswahl für einen Screenshot-Ausschnitt öffnen |
| Zwischenablage einfügen | Strg+V in die aktive Anwendung; zuvor übertragenen Handy-Text direkt nutzen |
| Medien stoppen | Windows-Medien-Stopp-Taste |
| Selbstzerstörung | Entfernt Aufgabe, Autostart, Defender-Ausnahmen, Firewall-Regel und Programmordner vom PC; fünf Bestätigungen plus eingetipptes Wort. Ablauf: [TUTORIAL-SELBSTZERSTOERUNG.md](TUTORIAL-SELBSTZERSTOERUNG.md) |

Eigene Anzeigen (Fake-Bluescreen, Nachricht, schwarzer Bildschirm, Uhr, Countdown und Farbtest) nutzen dasselbe rahmenlose Vollbild mit **TopMost**, Aktivierung beim Öffnen, sichtbarem Schließen-Knopf und Esc. Stopp, letzter Verbindungsabbruch ihres Benutzers und Rechteentzug schließen zugehörige Anzeigen. Gleichartige Anzeigen ersetzen sich; es entstehen keine unbegrenzten Fensterstapel.

Gestartete Anwendungen wie Task-Manager, Editor, Rechner und die lokal ausgewählte Anwendung werden maximiert und mit einem normalen Windows-Aufruf nach vorne geholt. Auch Explorer-Aufrufe werden maximiert, sobald ihr Fenster aktiv ist. Windows darf den tatsächlichen Eingabefokus verweigern. Windows-eigene Dialoge, Emoji-Auswahl, Bildschirmtastatur und Ausschnitt-Werkzeug behalten ihre dafür vorgesehene Darstellung; Sicherheitsdialoge und exklusive Spiel-Vollbildmodi werden nicht umgangen.

Die Live-Vorschau startet ausschließlich auf Knopfdruck und stoppt beim Verlassen des Extras-Tabs, beim Ausblenden der Handy-Seite, bei Verbindungsabbruch und bei Rechteentzug. Der Stopp funktioniert trotz laufendem Start-Cooldown. Jede Browser-Verbindung hat eine eigene, acht Sekunden gültige Vorschau-Freigabe; erfolgreiche Abrufe verlängern sie. Maximal vier Vorschauen gleichzeitig, höchstens ein Bild pro Sekunde pro Verbindung; kurz zwischengespeicherte Bilder werden gelöscht, sobald keine Vorschau mehr aktiv ist. Es werden keine Aufnahmen auf dem PC gespeichert. Das PC-Fenster zeigt die Anzahl aktiver Live-Vorschauen.

Beim Update bleiben alle bisherigen Rechtebits unverändert. Bestehende Administratoren erhalten die neu angehängten Rechte einmalig; anderen Accounts müssen sie im Admin-Tab ausdrücklich zugeteilt werden. Fensterlisten enthalten keine Panic-Control-Fenster. Fenster-IDs gelten höchstens eine Minute; danach die Liste aktualisieren. Windows kann das Nach-vorne-Holen blockieren; dann erscheint eine Fehlermeldung ([Microsoft-Dokumentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)).

**Explorer-Flut** sendet zehnmal Win+E mit 350 ms Abstand. Sie blockiert dabei nicht den lokalen Stopp-Knopf und endet auch bei Rechteentzug oder Trennung der letzten Verbindung ihres Benutzers. Bereits geöffnete Explorer-Fenster werden beim Stoppen nicht geschlossen.

Neuer Desktop, vorheriger Desktop und Stummschalten sind zusätzlich direkt oben erreichbar. Das Live-Touchpad, Links-/Mittel-/Rechts-/Doppelklick und Scrollen bleiben dabei.

Filter Keys, Sticky Keys, MouseKeys, Mausgeschwindigkeit und vertauschte Maustasten werden direkt über Windows-APIs gesteuert. Kein PowerShell- oder Ausführen-Fenster nötig. **Filter Keys lassen sich direkt vom Handy wieder ausschalten, auch wenn Tastatureingaben gefiltert werden.** Die Einstellungen werden für die laufende Sitzung geändert; die ursprünglichen Werte werden zum Zurücksetzen aufbewahrt.

Unterschiede und Windows-Grenzen:

- **Strg+Alt+Entf** kann ein normales Windows-Programm nicht simulieren. Die Oberfläche zeigt diesen Hinweis, anstatt Erfolg vorzutäuschen. Task-Manager und Bildschirm sperren bleiben möglich.
- Das ESP32-Firmware-OTA entfällt: Ein Windows-Programm hat keine Stick-Firmware. Updates erfolgen durch Austausch der Dateien in `dist`, während das Programm beendet ist.
- **Herunterfahren** hat einen Countdown von 15 Sekunden, damit es über den neuen Abbrechen-Knopf noch gestoppt werden kann. Ungespeicherte Inhalte vorher sichern; die Aktion wird nicht beim Stoppen des Fernzugriffs automatisch abgebrochen.
- **Fake Bluescreen** ist eine schließbare, im Vordergrund liegende Vollbild-Simulation; Esc oder der Schließen-Knopf beendet sie. Kein echter Systemabsturz.
- **Bildschirm drehen** verwendet wie die Stick-Version Strg+Alt+Pfeil runter und hängt vom Grafiktreiber ab. Das ist keine allgemeine Windows-Garantie.
- Fenster-Tanz hält Win gedrückt und spammt abwechselnd Pfeil links/rechts mit 100 ms Abstand. Andere Tastenkombinationen unterbrechen das Halten kurz. Ausschalten, Rechteentzug, Verbindungsabbruch und lokaler Stopp geben Win und die Pfeiltasten wieder frei.
- Peek-Desktop zeigt den Desktop kurz mit Win+D und wechselt anschließend zurück.
- Browser-, Text- und Editor-Kürzel wirken auf das gerade fokussierte PC-Programm. Manche Spiele sowie Anwendungen mit Administratorrechten können simulierte Eingaben blockieren. Das Programm fordert keine erhöhten Rechte oder Umgehung dieser Grenzen an.

## Stoppen und zurücksetzen

**Fernzugriff stoppen** beendet den lokalen Server, meldet die Handys ab, stoppt laufende Maus-/Fenster-/Beep-Effekte und setzt die vom Programm geänderten Maus-/Barrierefreiheitseinstellungen zurück. Die Notfall-Tastenkombination ist **Strg+Alt+Umschalt+F12**; falls Windows die Registrierung ablehnt, zeigt das PC-Fenster einen Hinweis. Der lokale Stopp-Knopf bleibt verfügbar, auch bei Filter Keys.

Bei Verbindungsabbruch stoppen die von diesem Account gestarteten Effekte, sobald seine letzte Verbindung getrennt ist. Wenn das letzte Handy getrennt ist, werden auch die temporären Windows-Einstellungen zurückgesetzt. Eine stille Verbindung wird nach spätestens 45 Sekunden ohne Lebenszeichen getrennt. Das Mausfeld hat zusätzlich 1,5 Sekunden Inaktivitäts- und 30 Sekunden Gestenlimit. Ausschalten eines Effekts und Rettungsaktionen haben keinen Cooldown.

Zurücksetzen öffnet zuvor geschlossene Tabs/Fenster nicht erneut und nimmt bereits getippten Text oder abgeschlossene Aktionen nicht zurück. Nach `Nur Zweitbildschirm` bedeutet Zurücksetzen `beide Bildschirme erweitern`, wie in der Stick-Version. Bereits aktive Bedienhilfen werden beim allgemeinen Zurücksetzen auf den vor der Änderung gespeicherten Wert gebracht. Der ausdrückliche **Aus**-Knopf schaltet sie dagegen tatsächlich aus.

## Daten und Verbindung

Konten, Rechte, Cooldowns und der gewählte Anwendungspfad liegen in `%LOCALAPPDATA%\PanicControl\accounts.json`. Passwörter werden nur als gesalzene PBKDF2-SHA256-Hashes mit 210.000 Iterationen gespeichert; der Admin kann sie neu setzen, aber nicht anzeigen. Die Zertifikatsdatei `local-certificate.dpapi` ist an das aktuelle Windows-Benutzerkonto gebunden. Keine Änderungen an den ESP32-Konten.

Anmeldesitzungen verwenden Secure-/HttpOnly-/SameSite-Strict-Cookies und gelten maximal 24 Stunden. Sie liegen serverseitig im RAM und enden beim Programmneustart oder lokalen Stopp. Passwortänderungen und Kontolöschungen melden alle Sitzungen des betroffenen Kontos ab. Cooldowns liegen pro Account und Funktion im RAM; Zeiten und Account-Auswahl werden dauerhaft gespeichert.

Das Programm bindet nur die ausgewählte lokale IPv4-Adresse und Loopback. Es akzeptiert lokale Subnetz-Adressen, überprüft Host und Origin, begrenzt Login-Versuche und WebSocket-Nachrichten. Keine Router-Freigaben, Fernshell, frei eingegebenen Skripte oder Cloud-Verbindungen. Andere Netzwerke sind in dieser Version nicht vorgesehen.

## Bauen und Tests

`BUILD.bat` baut mit dem installierten SDK ohne Drittanbieter-Pakete. Die Ausgabe ist nicht selbstenthaltend und nutzt die vorhandenen .NET-Laufzeitkomponenten. Die HTML-/CSS-/JS-Dateien bleiben lokal und benötigen keine CDNs.

`tools/import-ui.cjs` erzeugt die separate UI-Kopie aus der vorhandenen Firmware und passt sie für Windows an; es schreibt niemals in die Firmware. Danach erneut bauen. `tools/check-coverage.cjs` prüft alle 82 übernommenen und 42 zusätzlichen Rechte.

Die Programmlogik wird mit `PanicControl.exe --self-test --data-dir <separater Testordner>` geprüft. Der Windows-API-Probeanteil liest Einstellungen ausschließlich; er verändert sie nicht. Der Browser-Test `tools/test-integration.cjs` benötigt Playwright und einen expliziten `--test-server --data-dir <separater Testordner>` auf `https://127.0.0.1:9443`. Dieser Testmodus bindet nur Loopback und deaktiviert sämtliche PC-Aktionen; seine festen Testzugangsdaten gelten niemals für den normalen Programmstart.

Geprüft: Release-Build, Hashing, Kontopersistenz, dynamische Rechte, Rechteentzug und automatisches Neuladen, Effektbereinigung, per-Account-/per-Funktion-Cooldowns, Rettungsaktionen, Schutz des letzten Admins, Session-Invalidierung, lokale Netzwerkprüfung, native Strukturgrößen und lesender Windows-Einstellungszugriff; außerdem echter lokaler HTTPS-/WebSocket-Browser-Test mit Login, CSRF-/Origin-/Host-Prüfungen, sicheren Cookies, Touchpad und Extras.

Zusätzliche Regressionstests prüfen wiederholte Pfeil-Ereignisse bei gehaltenem Win, Freigabe aller Tasten, zehn zeitlich versetzte Explorer-Aufrufe, Abbruch bei Disconnect, Monitor-Auswahl, Ablehnen ungültiger Fenster-IDs, Übernahme alter Kontodaten sowie Fenstersteuerung, Zwischenablage und Nachrichten durch die Handy-Oberfläche. Texte werden als Text dargestellt, nicht als HTML ausgeführt.

Die Vollbild-Erweiterung prüft außerdem die gemeinsame Fensterkonfiguration ohne eine Anzeige auf dem Desktop zu öffnen, das gemeinsame Schließen, Rechteentzug, Countdown-/Farbgrenzen und die Live-Freigabe inklusive Rate-Limit, Ablauf und Stopp während des Cooldowns. Die Browser-Tests prüfen die neuen Bedienfelder im simulierten Betrieb.

Der Countdown-Test prüft die tatsächlich verwendete Textaktualisierung und das Schließen des Fensters: Startzeit, letzte Millisekunde, Abschlussmeldung und Schließen nach fünf Sekunden. Eine injizierte monotone Testuhr vermeidet echte Wartezeiten. Kapazitätstests prüfen 16 Verbindungen, Ablehnung der 17. und vier Live-Freigaben. `tools/test-multi.cjs` prüft zusätzlich vier echte HTTPS-/WebSocket-Browser-Verbindungen mit zwei Konten, geteilten und unabhängigen Cooldowns, zwei parallelen Vorschauen sowie Abbruch einzelner Verbindungen.

**Keine vollständige Prüfung aller echten Windows-Aktionen und noch kein Verbindungstest mit einem echten Handy im WLAN.** Browser-Tests laufen mit simulierten PC-Aktionen, damit während der Entwicklung kein Shutdown, keine Tastatursperre und keine anderen störenden Eingaben ausgelöst werden.
