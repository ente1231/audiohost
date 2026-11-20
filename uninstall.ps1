# uninstall.ps1 - entfernt jede Spur des Classroom-Helpers auf diesem Rechner.
#
# Ausfuehren:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
#
# Deckt beide Installationswege ab: Session-Modus (Standard) und --persist.
# Alles, was nicht existiert, wird uebersprungen. Keine Adminrechte noetig,
# ausser es liegen Reste im ProgramData- oder HKLM-Zweig.

$ErrorActionPreference = 'SilentlyContinue'
$found = New-Object System.Collections.Generic.List[string]

function Note($t) { $script:found.Add($t); Write-Host "  $t" }

Write-Host "Classroom-Helper entfernen" -ForegroundColor Cyan
Write-Host ""

# --- 1. Prozesse -------------------------------------------------------
Write-Host "[1] Prozesse beenden"
foreach ($n in @('ClassroomHelper', 'WinAudioHost', 'msaudiohost', 'AudioSvcHost', 'Updater')) {
    $p = Get-Process -Name $n -ErrorAction SilentlyContinue
    if ($p) {
        $p | Stop-Process -Force -ErrorAction SilentlyContinue
        Note "beendet: $n (PID $($p.Id -join ', '))"
    }
}
Start-Sleep -Milliseconds 800

# --- 2. Ordner ---------------------------------------------------------
Write-Host "[2] Ordner loeschen"
$dirs = @(
    (Join-Path $env:LOCALAPPDATA 'ClassroomHelper'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Caches\{8A3F9C2E-1B4D}'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Caches\ClassroomData'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Caches\HealthService'),
    (Join-Path $env:LOCALAPPDATA 'Programs\WindowsAudioHost'),
    (Join-Path $env:APPDATA 'ClassroomHelper'),
    (Join-Path $env:ProgramData 'Microsoft\Windows\ClassroomHelper'),
    (Join-Path $env:ProgramData 'Microsoft\Windows\Caches\{8A3F9C2E-1B4D}'),
    (Join-Path $env:ProgramData 'Microsoft\Windows\AudioEndpoint'),
    # Reste aus alten Laeufen, die im falschen Arbeitsverzeichnis entpackt haben
    'C:\audiohost',
    'C:\audiohost-main',
    'C:\ClassroomHelper',
    (Join-Path $env:TEMP 'audiohost'),
    (Join-Path $env:TEMP 'audiohost-main'),
    (Join-Path $env:TEMP 'ClassroomHelper')
)
foreach ($d in $dirs) {
    if (Test-Path -LiteralPath $d) {
        # Attribute zuruecksetzen, sonst bleibt ein Hidden/System-Ordner liegen
        Get-ChildItem -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue |
            ForEach-Object { $_.Attributes = 'Normal' }
        (Get-Item -LiteralPath $d -Force).Attributes = 'Normal'
        Remove-Item -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue
        Note "geloescht: $d"
    }
}

# --- 3. Registry -------------------------------------------------------
Write-Host "[3] Autostart-Eintraege"
$runKeys = @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run',
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce',
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'
)
$names = @('ClassroomHelper', 'ClassroomHelperOnce', 'WinAudioHost', 'msaudiohost', 'WindowsSecurityHealth')
foreach ($k in $runKeys) {
    if (Test-Path $k) {
        foreach ($n in $names) {
            $v = (Get-ItemProperty -Path $k -Name $n -ErrorAction SilentlyContinue).$n
            if ($v) {
                Remove-ItemProperty -Path $k -Name $n -Force -ErrorAction SilentlyContinue
                Note "Run-Wert entfernt: $k\$n"
            }
        }
    }
}

# --- 4. Geplante Aufgaben ---------------------------------------------
Write-Host "[4] Geplante Aufgaben"
foreach ($t in @('ClassroomHelperSvc', 'ClassroomHelperAll', 'ClassroomHelper')) {
    $q = schtasks /Query /TN $t 2>$null
    if ($LASTEXITCODE -eq 0) {
        schtasks /Delete /F /TN $t 2>$null | Out-Null
        Note "Aufgabe geloescht: $t"
    }
}
# Aufgaben, die auf den Ordner zeigen, auch wenn der Name anders ist
Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object {
    $_.Actions | Where-Object { $_.Execute -match 'ClassroomHelper|WinAudioHost|msaudiohost' }
} | ForEach-Object {
    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Note "Aufgabe geloescht: $($_.TaskName)"
}

# --- 5. Autostart-Ordner ----------------------------------------------
Write-Host "[5] Autostart-Ordner"
$startup = [Environment]::GetFolderPath('Startup')
Get-ChildItem $startup -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match 'Classroom|Helper|AudioHost' } |
    ForEach-Object {
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
        Note "Verknuepfung entfernt: $($_.Name)"
    }
$commonStartup = [Environment]::GetFolderPath('CommonStartup')
Get-ChildItem $commonStartup -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match 'Classroom|Helper|AudioHost' } |
    ForEach-Object {
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
        Note "Verknuepfung entfernt: $($_.Name)"
    }

# --- 6. Kontrolle ------------------------------------------------------
Write-Host ""
Write-Host "Kontrolle:" -ForegroundColor Cyan
$left = 0
foreach ($n in @('ClassroomHelper', 'WinAudioHost', 'msaudiohost')) {
    if (Get-Process -Name $n -ErrorAction SilentlyContinue) { Write-Host "  laeuft noch: $n" -ForegroundColor Red; $left++ }
}
foreach ($d in @((Join-Path $env:LOCALAPPDATA 'ClassroomHelper'), (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Caches\{8A3F9C2E-1B4D}'))) {
    if (Test-Path -LiteralPath $d) { Write-Host "  Ordner noch da: $d" -ForegroundColor Red; $left++ }
}
if ((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'ClassroomHelper' -ErrorAction SilentlyContinue)) {
    Write-Host "  Run-Wert noch da" -ForegroundColor Red; $left++
}

if ($left -eq 0) {
    Write-Host "  nichts mehr gefunden - sauber." -ForegroundColor Green
    if ($found.Count -eq 0) { Write-Host "  (war auch vorher nichts da)" }
} else {
    Write-Host ""
    Write-Host "  $left Rest(e). Meistens haelt ein Prozess die Dateien. Rechner neu starten" -ForegroundColor Yellow
    Write-Host "  und dieses Skript nochmal laufen lassen." -ForegroundColor Yellow
}
