ESP32-S2 Mini – HID one-shot launch (sketch notes)
==================================================

Goal: plug board → type Win+R → path to PanicControl.exe --silent → Enter
Once per user the exe installs HKCU Run + marker; later starts are silent via autostart.
Second HID launch while already running: mutex → instant exit (no second Win+R needed if you only plug when not running).

Hardware: ESP32-S2 (native USB HID). Arduino core or ESP-IDF USB HID keyboard example.

Important:
- Target PC keyboard layout must match what you type (DE vs US: Y/Z, \, /, etc.).
- Session must be unlocked.
- Delay 1.5–3s after USB enumerate before first key.
- Prefer path on a second USB stick letter, e.g. E:\PanicControl\PanicControl.exe
  or the shadow copy path after first install.

Example payload idea (US layout):
  Wait 2000 ms
  GUI r
  Wait 400 ms
  String E:\PanicControl\PanicControl.exe --silent
  Enter

German layout: adjust characters for \ and path.

After first successful start on a user:
- HKCU Run starts the exe at next logon of THAT user (if no Deep Freeze)
- Mutex prevents double instance
- .user-ready marker in data dir

Do NOT re-plug and type on every login if autostart already works – teacher sees Win+R.

Portable folder on stick:
  PanicControl.exe
  actions.json
  web\...
  (optional) this README

Default silent admin (first run without UI):
  user = admin (or field default)
  pass = Panic-<MachineName>-<UserName>  (see .first-cred.txt in data dir, hidden)

Connect from phone on same LAN:
  https://<pc-lan-ip>:8443
