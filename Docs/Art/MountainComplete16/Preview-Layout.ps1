param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('MountainFootPixels' -as [type])) {
    Add-Type -Path @((Join-Path $PSScriptRoot '..\MountainFoot16\MountainFootPixels.cs'), (Join-Path $PSScriptRoot 'MountainSeamChecks.cs'))
}
$fixture = Get-Content -Raw (Join-Path $ProjectRoot 'Temp\MountainFootValidation\layout-preview.json') | ConvertFrom-Json
$manifest = Get-Content -Raw (Join-Path $PSScriptRoot 'manifest.json') | ConvertFrom-Json
$map = [bool[,]]::new($fixture.width, $fixture.height)
$parts = [int[,]]::new($fixture.width, $fixture.height)
foreach ($cell in $fixture.cells) {
    $bitmapY = $fixture.height - 1 - $cell.y
    $map[$cell.x, $bitmapY] = $true
    $parts[$cell.x, $bitmapY] = $cell.part
}
$atlases = @{}
$indices = @{}
$preview = [System.Drawing.Bitmap]::new([int]($fixture.width * 16), [int]($fixture.height * 16))
try {
    foreach ($entry in $manifest) {
        $atlases[[int]$entry.role] = [System.Drawing.Bitmap]::FromFile((Join-Path $ProjectRoot $entry.texture))
        $lookup = @{}
        for ($i = 0; $i -lt $entry.slices.Count; $i++) { $lookup[[int]$entry.slices[$i].mask] = $i }
        $indices[[int]$entry.role] = $lookup
    }
    $grass = [System.Drawing.ColorTranslator]::FromHtml('#89A043')
    for ($cy = 0; $cy -lt $fixture.height; $cy++) { for ($cx = 0; $cx -lt $fixture.width; $cx++) {
        $atlas = $null
        if ($map[$cx, $cy]) {
            $part = $parts[$cx, $cy]
            $mask = if ($part -eq 2) { [MountainFootPixels]::SummitMaskAt($map, $parts, $cx, $cy) } else { [MountainFootPixels]::MaskAt($map, $cx, $cy) }
            $atlas = $atlases[$part]
            $index = $indices[$part][$mask]
            if ($null -eq $index) { throw "Unmatched preview rule: role=$part mask=$mask" }
            $sx = $index % 8 * 16
            $sy = [int][Math]::Floor($index / 8) * 16
        }
        for ($y = 0; $y -lt 16; $y++) { for ($x = 0; $x -lt 16; $x++) {
            $color = if ($null -eq $atlas) { $grass } else { $atlas.GetPixel(($sx + $x), ($sy + $y)) }
            $preview.SetPixel(($cx * 16 + $x), ($cy * 16 + $y), $color)
        } }
    } }
    $zoom = [System.Drawing.Bitmap]::new(($preview.Width * 2), ($preview.Height * 2))
    $graphics = [System.Drawing.Graphics]::FromImage($zoom)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.DrawImage($preview, [System.Drawing.Rectangle]::new(0, 0, $zoom.Width, $zoom.Height))
        $zoom.Save((Join-Path $PSScriptRoot 'Runtime-Layout-Regression.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $zoom.Dispose() }
} finally {
    $preview.Dispose()
    foreach ($atlas in $atlases.Values) { $atlas.Dispose() }
}
Write-Output 'Rendered actual final placements: low plateau, tall plateau, stepped/concave front.'
