$ErrorActionPreference = 'Stop'
$panicCase = Join-Path $PSScriptRoot ('..\test-data\starter-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path (Join-Path $panicCase 'dist'))
[void](New-Item -ItemType Directory -Path (Join-Path $panicCase 'update\web'))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\Start.ps1') -Destination $panicCase
$panicDll = Join-Path $panicCase 'dist\PanicControl.dll'
Set-Content -LiteralPath $panicDll -Value 'previous'
Set-Content -LiteralPath (Join-Path $panicCase 'update\PanicControl.dll') -Value 'updated'
Set-Content -LiteralPath (Join-Path $panicCase 'update\web\app.js') -Value 'new-ui'
$panicReady = Join-Path $panicCase 'update\READY'
Set-Content -LiteralPath $panicReady -Value 'ready'
$panicLock = [IO.File]::Open($panicDll, 'Open', 'Read', 'Read')
try {
    $panicRejected = $false
    try { & (Join-Path $panicCase 'Start.ps1') -UpdateOnly }
    catch { $panicRejected = $_.Exception.Message.Contains('vollständig schließen') }
    if (-not $panicRejected -or -not (Test-Path -LiteralPath $panicReady)) { throw 'Locked app was not preserved.' }
} finally { $panicLock.Dispose() }
& (Join-Path $panicCase 'Start.ps1') -UpdateOnly
if ((Get-Content -LiteralPath $panicDll) -ne 'updated' -or (Test-Path -LiteralPath $panicReady)) { throw 'Update was not installed.' }
if ((Get-Content -LiteralPath (Join-Path $panicCase 'dist\web\app.js')) -ne 'new-ui') { throw 'Nested UI was not installed.' }
Write-Output 'PASS: starter preserves a running app, installs a ready update after closing, copies UI, launches no PC app during test.'
