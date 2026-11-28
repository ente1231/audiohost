param([switch]$UpdateOnly)
$ErrorActionPreference = 'Stop'
$panicDist = Join-Path $PSScriptRoot 'dist'
$panicUpdate = Join-Path $PSScriptRoot 'update'
$panicReady = Join-Path $panicUpdate 'READY'
try {
    if (Test-Path -LiteralPath $panicReady) {
        # The loaded executable/DLL cannot be replaced. Do not stop a user's app.
        foreach ($panicFile in @('ClassroomHelper.exe', 'ClassroomHelper.dll', 'PanicControl.exe', 'PanicControl.dll')) {
            $panicTarget = Join-Path $panicDist $panicFile
            if (Test-Path -LiteralPath $panicTarget) {
                try { $panicLock = [IO.File]::Open($panicTarget, 'Open', 'ReadWrite', 'None'); $panicLock.Dispose() }
                catch { throw 'Bitte das laufende Panic-Control-Fenster zuerst vollständig schließen, dann START.bat erneut öffnen.' }
            }
        }
        if (-not (Test-Path -LiteralPath $panicDist)) { [void](New-Item -ItemType Directory -Path $panicDist) }
        foreach ($panicEntry in Get-ChildItem -LiteralPath $panicUpdate) {
            if ($panicEntry.Name -ne 'READY') { Copy-Item -LiteralPath $panicEntry.FullName -Destination $panicDist -Recurse -Force }
        }
        Remove-Item -LiteralPath $panicReady
    }
    if ($UpdateOnly) { return }
    # assembly name is ClassroomHelper; keep PanicControl.exe working for old layouts
    $panicExe = Join-Path $panicDist 'ClassroomHelper.exe'
    if (-not (Test-Path -LiteralPath $panicExe)) { $panicExe = Join-Path $panicDist 'PanicControl.exe' }
    if (-not (Test-Path -LiteralPath $panicExe)) { throw 'Bitte zuerst BUILD.bat ausführen.' }
    Start-Process -FilePath $panicExe -WorkingDirectory $panicDist -WindowStyle Normal
} catch {
    if ($UpdateOnly) { throw }
    Add-Type -AssemblyName System.Windows.Forms
    [void][System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Panic Control · Start')
    exit 1
}
