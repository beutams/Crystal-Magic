$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$palette = @('#A08043', '#B3995C', '#805B32', '#514332', '#89A043', '#647B36') |
    ForEach-Object { [System.Drawing.ColorTranslator]::FromHtml($_) }
$names = @('01-Foot', '02-Wall', '03-Summit-Front', '04-Summit', '05-Summit-Back')
function Snap-Color([System.Drawing.Color]$sample) {
    $bestDistance = [double]::PositiveInfinity
    $bestColor = $palette[0]
    foreach ($color in $palette) {
        $distance = [Math]::Pow($sample.R - $color.R, 2) + [Math]::Pow($sample.G - $color.G, 2) + [Math]::Pow($sample.B - $color.B, 2)
        if ($distance -lt $bestDistance) { $bestDistance = $distance; $bestColor = $color }
    }
    return $bestColor
}
function Save-Zoom([System.Drawing.Bitmap]$source, [string]$path, [int]$scale) {
    $zoom = [System.Drawing.Bitmap]::new([int]($source.Width * $scale), [int]($source.Height * $scale))
    try {
        for ($y = 0; $y -lt $zoom.Height; $y++) {
            for ($x = 0; $x -lt $zoom.Width; $x++) {
                $zoom.SetPixel($x, $y, $source.GetPixel([int][Math]::Floor($x / $scale), [int][Math]::Floor($y / $scale)))
            }
        }
        $zoom.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $zoom.Dispose() }
}
# Mechanical conversion only: sample the generated tile, snap to the common
# palette, and copy pixels to the requested exact native dimensions.
# No hand-drawn replacement textures or added edge geometry.
foreach ($name in $names) {
    $sourceName = if ($name -eq '02-Wall') { '02-Wall-SeamMatched' } else { $name }
    $source = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot ('Sources/' + $sourceName + '.png')))
    $alphaProposal = if ($name -eq '05-Summit-Back') {
        [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'Sources/05-Summit-Back-AlphaProposal.png'))
    } else { $null }
    $tile = [System.Drawing.Bitmap]::new(16, 16)
    try {
        for ($y = 0; $y -lt 16; $y++) {
            for ($x = 0; $x -lt 16; $x++) {
                $sx = [int][Math]::Floor(($x + 0.5) * $source.Width / 16)
                $sy = [int][Math]::Floor(($y + 0.5) * $source.Height / 16)
                $color = Snap-Color ($source.GetPixel($sx, $sy))
                # Accept the generated cutout only inside the requested green
                # outside-region mask. Retain all original opaque rock pixels;
                # the cutout proposal incorrectly removed some summit interior.
                if ($null -ne $alphaProposal -and $color.ToArgb() -eq $palette[4].ToArgb()) {
                    $ax = [int][Math]::Floor(($x + 0.5) * $alphaProposal.Width / 16)
                    $ay = [int][Math]::Floor(($y + 0.5) * $alphaProposal.Height / 16)
                    if ($alphaProposal.GetPixel($ax, $ay).A -ge 128) { throw 'Generated cutout did not remove an outside pixel.' }
                    $color = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
                }
                $tile.SetPixel($x, $y, $color)
            }
        }
        $tile.Save((Join-Path $PSScriptRoot ($name + '-16x16.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $tile.Dispose(); $source.Dispose()
        if ($null -ne $alphaProposal) { $alphaProposal.Dispose() }
    }
}
$order = @('05-Summit-Back', '04-Summit', '03-Summit-Front', '02-Wall', '01-Foot', '06-Grass')
$strip = [System.Drawing.Bitmap]::new(16, 96)
try {
    for ($i = 0; $i -lt $order.Count; $i++) {
        $tile = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot ($order[$i] + '-16x16.png')))
        try {
            if ($tile.Width -ne 16 -or $tile.Height -ne 16) { throw 'Incorrect native tile size.' }
            for ($y = 0; $y -lt 16; $y++) {
                for ($x = 0; $x -lt 16; $x++) { $strip.SetPixel($x, $i * 16 + $y, $tile.GetPixel($x, $y)) }
            }
        } finally { $tile.Dispose() }
    }
    $strip.Save((Join-Path $PSScriptRoot 'Mountain-Front-16x96.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    Save-Zoom $strip (Join-Path $PSScriptRoot 'Mountain-Front-preview-8x.png') 8
    $seams = @()
    for ($i = 1; $i -lt 6; $i++) {
        $different = @()
        for ($x = 0; $x -lt 16; $x++) {
            if ($strip.GetPixel($x, $i * 16 - 1).ToArgb() -ne $strip.GetPixel($x, $i * 16).ToArgb()) { $different += $x }
        }
        $seams += [pscustomobject]@{Above=$order[$i-1]; Below=$order[$i]; DifferentColumns=($different -join ',')}
    }
    $seams | Format-Table -AutoSize
    if (@($seams | Where-Object { $_.DifferentColumns -ne '' }).Count -gt 0) { throw 'An adjacent tile edge does not match.' }
    $summit = [System.Drawing.ColorTranslator]::FromHtml('#B3995C').ToArgb()
    $grass = [System.Drawing.ColorTranslator]::FromHtml('#89A043').ToArgb()
    $transparentPixels = 0
    for ($y = 0; $y -lt 96; $y++) {
        for ($x = 0; $x -lt 16; $x++) {
            $color = $strip.GetPixel($x, $y)
            if ($color.A -ne 255) {
                if ($color.A -ne 0 -or $y -ge 16) { throw 'Unexpected transparency outside the back-edge tile.' }
                $transparentPixels++
            }
            if ($y -lt 16 -and $color.ToArgb() -eq $grass) { throw 'Back edge still contains grass.' }
            if ($y -eq 0 -and $color.A -ne 0) { throw 'Back outside must reveal the scene behind.' }
            if ($y -ge 16 -and $y -lt 32 -and $color.ToArgb() -ne $summit) { throw 'Summit is not a solid fill.' }
            if ($y -ge 77 -and $color.ToArgb() -ne $grass) { throw 'Foot does not meet existing grass.' }
        }
    }
    if ($transparentPixels -eq 0) { throw 'Back edge has no transparent outside area.' }
    Write-Output ('Verified transparent outside pixels: ' + $transparentPixels + '; all opaque mountain pixels retained.')
} finally { $strip.Dispose() }
Write-Output 'Created five generated 16x16 mountain tiles, existing 16x16 grass, and 16x96 strip.'
