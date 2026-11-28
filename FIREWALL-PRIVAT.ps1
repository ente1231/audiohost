# Run only if Windows blocks connections from your phone. Requests normal UAC.
# Restricts access to this program, TCP 8443, private networks and the local subnet.
$panicName = 'ClassroomHelper.exe'
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot ('dist\' + $panicName)) -PathType Leaf)) { $panicName = 'PanicControl.exe' }
$panicProgram = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('dist\' + $panicName)))
if (-not (Test-Path -LiteralPath $panicProgram -PathType Leaf)) { throw 'Erst BUILD.bat ausführen.' }
$panicIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$panicPrincipal = [Security.Principal.WindowsPrincipal]::new($panicIdentity)
if (-not $panicPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList @('-NoProfile', '-File', ('"' + $PSCommandPath + '"'))
  exit
}
$panicRuleName = 'PanicControl-Local-8443'
$panicExisting = Get-NetFirewallRule -Name $panicRuleName -ErrorAction SilentlyContinue
if ($panicExisting) { $panicExisting | Remove-NetFirewallRule }
New-NetFirewallRule -Name $panicRuleName -DisplayName 'Panic Control - privates WLAN' -Direction Inbound -Action Allow -Program $panicProgram -Protocol TCP -LocalPort 8443 -Profile Private -RemoteAddress LocalSubnet | Out-Null
Write-Host "Panic Control ($panicName) darf jetzt im privaten WLAN erreicht werden."
