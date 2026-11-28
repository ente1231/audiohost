# uninstall-agent.ps1 - entfernt den COM-Eintrag wieder.
#
# Die DLL im Speicher laeuft bis zum naechsten Explorer-Neustart weiter;
# danach wird sie nicht mehr geladen.

$clsid = '{42aedc87-2188-41fd-b9a3-0c966feabec1}'
$base = "HKCU:\Software\Classes\CLSID\$clsid"

if (Test-Path $base)
{
    Remove-Item -Path $base -Recurse -Force
    Write-Host "entfernt: $base"
}
else
{
    Write-Host "nichts zu entfernen"
}

Write-Host 'Explorer neu starten, damit die DLL aus dem Speicher faellt:'
Write-Host '  taskkill /f /im explorer.exe ; start explorer'
