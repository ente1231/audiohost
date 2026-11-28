$panicPidFile = Join-Path $PSScriptRoot '..\test-data\test-process-id.txt'
if (-not (Test-Path -LiteralPath $panicPidFile)) { exit }
$panicTestId = [int](Get-Content -LiteralPath $panicPidFile)
$panicTestInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $panicTestId"
$panicExpectedDll = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Release\net8.0-windows\PanicControl.dll'))
if (-not $panicTestInfo) { exit }
if (-not $panicTestInfo.CommandLine.Contains($panicExpectedDll) -or -not $panicTestInfo.CommandLine.Contains('--test-server')) { throw 'PID gehört nicht zum isolierten Testserver.' }
$panicProcess = Get-Process -Id $panicTestId
[void]$panicProcess.CloseMainWindow()
if (-not $panicProcess.WaitForExit(3000)) { Stop-Process -Id $panicTestId }
Write-Host 'Isolierter Testserver beendet.'
