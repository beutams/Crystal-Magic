$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
# Existing single-grass mountain atlas: the unused final cell stores #89A043.
# Copy its pixels unchanged; do not use the different Village g_base green.
$sourcePath = Join-Path $projectRoot 'Assets/Res/Sprites/Dungeon/MountainFoot16/MountainFoot_Grass1.png'
$source = [System.Drawing.Bitmap]::FromFile($sourcePath)
$tile = [System.Drawing.Bitmap]::new(16, 16)
$zoom = [System.Drawing.Bitmap]::new(256, 256)
try {
    for ($y = 0; $y -lt 16; $y++) {
        for ($x = 0; $x -lt 16; $x++) {
            $color = $source.GetPixel(112 + $x, 80 + $y)
            if ($color.ToArgb() -ne [System.Drawing.ColorTranslator]::FromHtml('#89A043').ToArgb()) {
                throw 'The existing grass sample no longer matches #89A043.'
            }
            $tile.SetPixel($x, $y, $color)
        }
    }
    for ($y = 0; $y -lt 256; $y++) {
        for ($x = 0; $x -lt 256; $x++) {
            $zoom.SetPixel($x, $y, $tile.GetPixel([int][Math]::Floor($x / 16), [int][Math]::Floor($y / 16)))
        }
    }
    $tile.Save((Join-Path $PSScriptRoot '06-Grass-16x16.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $zoom.Save((Join-Path $PSScriptRoot 'Grass-reference-256.png'), [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $source.Dispose()
    $tile.Dispose()
    $zoom.Dispose()
}
Write-Output 'Copied existing #89A043 grass: 16x16, opaque, unchanged.'
