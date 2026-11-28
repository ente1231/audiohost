# Die Adresse aufs Handy bekommen — ohne Terminal

Kurz: **`curl` brauchst du nicht.** Das war meine Formulierung für die Kommandozeile. Auf dem Handy machst du das im Browser.

Und ja: **die Adresse funktioniert überall**, auch ohne gleiches WLAN. Genau dafür ist der Tunnel da. Der PC baut eine ausgehende Verbindung zu Cloudflare auf, dein Handy geht über das normale Internet.

---

## Variante A — Relay-Seite (einfachster Weg)

Wenn dein Relay auf Render läuft, hast du jetzt eine echte Webseite statt rohem JSON:

```
https://panic-relay.onrender.com
```

Einfach im Handy-Browser öffnen. Da steht:

- **grüner Punkt + „PC ist online"** — der Rechner hängt am Relay
- **großer grüner Knopf** mit der aktuellen Tunnel-Adresse — antippen, fertig
- darunter Rechnername und Tunnel-Adresse als Text

Die Seite aktualisiert sich alle 4 Sekunden selbst. Wenn der PC offline ist, wird der Knopf grau. Kein curl, kein JSON, kein Suchen.

---

## Variante B — feste Adresse, gar nichts nachschauen

Wenn du einmal Domain + Token einrichtest (siehe `CLOUDFLARE.md`, Abschnitt 3), ist die Adresse für immer dieselbe:

```
https://pc.deinedomain.de
```

Die merkst du dir einmal. Kein Relay, kein Nachschauen, kein Quick-Tunnel-Zufall.

Das ist der Weg, wenn du „URL gespeichert, Handy auf, fertig" willst.

---

## Variante C — am PC nachsehen

Wenn du gerade am Rechner sitzt:

```bat
type "%LOCALAPPDATA%\Microsoft\Windows\Caches\ClassroomData\tunnel.url"
```

Oder in einer Eingabeaufforderung:

```bat
curl https://panic-relay.onrender.com/api/status
```

Das ist die Kommandozeilen-Variante. Am PC okay, am Handy unpraktisch — dafür gibt es Variante A.

---

## Warum die Adresse sich ändert

Der Quick-Tunnel bekommt bei **jedem Start** einen neuen Zufallsnamen, z. B. `https://seeing-invitation-decor-belief.trycloudflare.com`. Deshalb:

- Relay-Seite: du siehst immer die aktuelle
- benannter Tunnel: immer dieselbe

Wenn dich das Nachschauen nervt, ist der benannte Tunnel die Antwort. Eine `.xyz`-Domain kostet ein bis zwei Euro im Jahr.

---

## Was auf dem Handy passiert

1. Adresse öffnen (Relay-Knopf oder gemerkte Domain)
2. Anmeldeseite erscheint — das ist die echte PC-Oberfläche
3. `admin` / `PanicControl-2026` (oder was du in `admin.user` / `admin.pass` gesetzt hast)
4. Alle Tabs da: Quick, Maus, Tastatur, System, Windows, Browser, Extras, Admin

Der Tunnel ist eine normale HTTPS-Adresse mit gültigem Cloudflare-Zertifikat — **keine Zertifikatswarnung** wie im WLAN-Modus. Das Handy muss nur Internet haben, sonst nichts.
