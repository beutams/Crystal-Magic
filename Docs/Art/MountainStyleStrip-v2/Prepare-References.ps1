$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$oldRoot = Join-Path $PSScriptRoot '../MountainStyleStrip-v1'
$referenceRoot = Join-Path $PSScriptRoot 'References'
New-Item -ItemType Directory -Path $referenceRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'Sources') -Force | Out-Null
foreach ($name in @('01-Foot', '02-Wall', '03-Summit-Front')) {
    $tile = [System.Drawing.Bitmap]::FromFile((Join-Path $oldRoot ($name + '-16x16.png')))
    $zoom = [System.Drawing.Bitmap]::new(512, 512)
    try {
        for ($y = 0; $y -lt 512; $y++) {
            for ($x = 0; $x -lt 512; $x++) {
                $zoom.SetPixel($x, $y, $tile.GetPixel([int][Math]::Floor($x / 32), [int][Math]::Floor($y / 32)))
            }
        }
        $zoom.Save((Join-Path $referenceRoot ($name + '-v1-512.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $zoom.Dispose(); $tile.Dispose() }
}
