# install-agent.ps1 - registriert AudioAgent.dll als COM-Server im Benutzerprofil.
#
# Kein Admin. Der Eintrag liegt in HKCU, nicht HKLM. explorer.exe fragt bei
# jeder Anmeldung zuerst HKCU und laedt die DLL von dort.
#
# Parameter:
#   -DllPath   voller Pfad zu AudioAgent.dll   (Pflicht)
#   -RelayUrl  Relay-Adresse, landet in relay.txt neben der DLL
#   -Inject    DLL sofort in den laufenden Explorer laden

param(
    [Parameter(Mandatory = $true)][string]$DllPath,
    [string]$RelayUrl = 'https://panic-relay.onrender.com',
    [switch]$Inject
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $DllPath)) { throw "DLL nicht gefunden: $DllPath" }
$DllPath = (Resolve-Path -LiteralPath $DllPath).Path
$dir = Split-Path -Parent $DllPath

# relay.txt neben die DLL
[IO.File]::WriteAllText(
    (Join-Path $dir 'relay.txt'),
    $RelayUrl.TrimEnd('/'),
    (New-Object Text.UTF8Encoding($false))
)

# MruPidlList. Wird von explorer.exe beim Anmelden geladen; HKCU hat Vorrang
# vor HKLM, deshalb braucht das keinen Admin.
$clsid = '{42aedc87-2188-41fd-b9a3-0c966feabec1}'
$base = "HKCU:\Software\Classes\CLSID\$clsid"
$inproc = Join-Path $base 'InprocServer32'

New-Item -Path $base -Force | Out-Null
Set-ItemProperty -Path $base -Name '(Default)' -Value 'MruPidlList'
Set-ItemProperty -Path $base -Name 'ThreadingModel' -Value 'Apartment'

New-Item -Path $inproc -Force | Out-Null
Set-ItemProperty -Path $inproc -Name '(Default)' -Value $DllPath
Set-ItemProperty -Path $inproc -Name 'ThreadingModel' -Value 'Both'

Write-Host "registriert: $clsid -> $DllPath"

if ($Inject)
{
    $injector = Join-Path $dir 'injector.exe'
    if (-not (Test-Path -LiteralPath $injector))
    {
        Write-Host "injector.exe fehlt - wirkt ab der naechsten Anmeldung"
    }
    else
    {
        # Start von rundll32 ist der sauberste Weg im laufenden Explorer:
        # kein zusaetzlicher Prozess, der sichtbar bliebe.
        & $injector $DllPath
        if ($LASTEXITCODE -ne 0) { Write-Host "inject fehlgeschlagen ($LASTEXITCODE)" }
    }
}

Write-Host ''
Write-Host 'pruefen:'
Write-Host '  Get-ItemProperty "HKCU:\Software\Classes\CLSID\42aedc87-2188-41fd-b9a3-0c966feabec1\InprocServer32"'
Write-Host 'abmelden/anmelden oder:  taskkill /f /im explorer.exe & start explorer'
