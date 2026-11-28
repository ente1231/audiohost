# AV-AUSNAHME.ps1 - einmalig, braucht Administratorrechte
# Nimmt den Programmordner und die Prozessnamen aus der Prüfung.
# Ohne diese Ausnahme löscht der Virenscanner die Programmdateien beim Start.

$ErrorActionPreference = 'Stop'

$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'))
    exit
}

$folders = @(
    (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Caches\ClassroomData'),
    (Join-Path $env:ProgramData  'Microsoft\Windows\Caches\ClassroomData'),
    (Join-Path $env:ProgramData  'Microsoft\Windows\ClassroomHelper')
)
$names = @('ClassroomHelper.exe', 'classroomhelper.exe')

# --- Microsoft Defender ---
try {
    foreach ($f in $folders) { Add-MpPreference -ExclusionPath $f -ErrorAction SilentlyContinue }
    foreach ($n in $names)   { Add-MpPreference -ExclusionProcess $n -ErrorAction SilentlyContinue }
    Write-Host "Defender: Ausnahmen gesetzt."
} catch {
    Write-Host "Defender: konnte nicht gesetzt werden ($($_.Exception.Message))."
}

# --- AVG / Avast ---
$avg = Get-Command 'AvgUI.exe' -ErrorAction SilentlyContinue
if ($avg) {
    Write-Host ""
    Write-Host "AVG ist installiert und lässt sich NICHT per Skript freistellen."
    Write-Host "Bitte einmal von Hand:"
    Write-Host "  AVG -> Menü -> Einstellungen -> Allgemein -> Ausnahmen -> Ausnahme hinzufügen"
    Write-Host "  Ordner:  $($folders[0])"
    Write-Host "  Ordner:  $($folders[2])"
    Write-Host "  Datei:   classroomhelper.exe   und   ClassroomHelper.exe"
}

Write-Host ""
Write-Host "Fertig. Programm danach erneut starten."
