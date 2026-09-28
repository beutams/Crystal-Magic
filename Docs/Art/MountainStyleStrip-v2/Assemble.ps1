$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$oldRoot = Join-Path $PSScriptRoot '../MountainStyleStrip-v1'
$rockPalette = @('#A08043', '#B3995C', '#805B32', '#514332') |
    ForEach-Object { [System.Drawing.ColorTranslator]::FromHtml($_) }
$names = @('01-Foot', '02-Wall', '03-Summit-Front', '04-Summit', '05-Summit-Back', '06-Grass')
function Snap-Color([System.Drawing.Color]$sample) {
    $bestDistance = [double]::PositiveInfinity
    $bestColor = $rockPalette[0]
    foreach ($color in $rockPalette) {
        $distance = [Math]::Pow($sample.R - $color.R, 2) + [Math]::Pow($sample.G - $color.G, 2) + [Math]::Pow($sample.B - $color.B, 2)
        if ($distance -lt $bestDistance) { $bestDistance = $distance; $bestColor = $color }
    }
    return $bestColor
}
function Sample-Detail([System.Drawing.Bitmap]$source, [int]$x, [int]$y, [System.Drawing.Color]$original) {
    # Palette-aware reduction: retain generated narrow fractures occupying at
    # least an eighth of a native cell. Plain nearest sampling can miss them.
    # Every chosen color comes from the generated image, not procedural art.
    $counts = @{}
    for ($dy = 0; $dy -lt 4; $dy++) {
        for ($dx = 0; $dx -lt 4; $dx++) {
            $sx = [int][Math]::Floor(($x + ($dx + 0.5) / 4) * $source.Width / 16)
            $sy = [int][Math]::Floor(($y + ($dy + 0.5) / 4) * $source.Height / 16)
            $color = Snap-Color ($source.GetPixel($sx, $sy))
            $key = $color.ToArgb()
            $counts[$key]++
        }
    }
    $detail = $counts.GetEnumerator() |
        Where-Object { $_.Key -ne $original.ToArgb() } |
        Sort-Object @{Expression='Value';Descending=$true}, @{Expression='Key';Descending=$false} |
        Select-Object -First 1
    if ($null -ne $detail -and $detail.Value -ge 2) {
        return [System.Drawing.Color]::FromArgb([int]$detail.Key)
    }
    return $original
}
$totalChanged = 0
foreach ($name in $names) {
    $oldPath = Join-Path $oldRoot ($name + '-16x16.png')
    $outputPath = Join-Path $PSScriptRoot ($name + '-16x16.png')
    if ($name -in @('04-Summit', '05-Summit-Back', '06-Grass')) {
        Copy-Item -LiteralPath $oldPath -Destination $outputPath -Force
        Write-Output ($name + ': unchanged original tile.')
        continue
    }
    $original = [System.Drawing.Bitmap]::FromFile($oldPath)
    $generated = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot ('Sources/' + $name + '.png')))
    $tile = [System.Drawing.Bitmap]::new(16, 16)
    $changed = 0
    try {
        for ($y = 0; $y -lt 16; $y++) {
            for ($x = 0; $x -lt 16; $x++) {
                $before = $original.GetPixel($x, $y)
                # Composite the generated detail pass only into the editable
                # rock region. Borders, grass and summit interior remain exact.
                $preserve = $x -eq 0 -or $x -eq 15 -or $y -eq 0 -or $y -eq 15 -or
                    ($name -eq '01-Foot' -and $y -ge 12) -or
                    ($name -eq '03-Summit-Front' -and $y -lt 6)
                $after = $before
                if (-not $preserve) {
                    $after = Sample-Detail $generated $x $y $before
                }
                $tile.SetPixel($x, $y, $after)
                if ($before.ToArgb() -ne $after.ToArgb()) { $changed++ }
            }
        }
        if ($changed -eq 0) { throw ($name + ' has no visible detail change at native resolution.') }
        $tile.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output ($name + ': ' + $changed + ' of 256 pixels changed.')
        $totalChanged += $changed
    } finally { $original.Dispose(); $generated.Dispose(); $tile.Dispose() }
}
$order = @('05-Summit-Back', '04-Summit', '03-Summit-Front', '02-Wall', '01-Foot', '06-Grass')
$strip = [System.Drawing.Bitmap]::new(16, 96)
try {
    for ($i = 0; $i -lt $order.Count; $i++) {
        $tile = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot ($order[$i] + '-16x16.png')))
        try {
            if ($tile.Width -ne 16 -or $tile.Height -ne 16) { throw 'Incorrect native tile dimensions.' }
            for ($y = 0; $y -lt 16; $y++) {
                for ($x = 0; $x -lt 16; $x++) { $strip.SetPixel($x, $i * 16 + $y, $tile.GetPixel($x, $y)) }
            }
        } finally { $tile.Dispose() }
    }
    for ($i = 1; $i -lt 6; $i++) {
        for ($x = 0; $x -lt 16; $x++) {
            if ($strip.GetPixel($x, $i * 16 - 1).ToArgb() -ne $strip.GetPixel($x, $i * 16).ToArgb()) { throw 'An adjacent tile edge does not match.' }
        }
    }
    $oldStrip = [System.Drawing.Bitmap]::FromFile((Join-Path $oldRoot 'Mountain-Front-16x96.png'))
    $transparent = 0
    try {
        for ($y = 0; $y -lt 96; $y++) {
            for ($x = 0; $x -lt 16; $x++) {
                $old = $oldStrip.GetPixel($x, $y)
                $new = $strip.GetPixel($x, $y)
                if ($old.A -ne $new.A) { throw 'The approved transparency shape changed.' }
                if ($new.A -eq 0) { $transparent++ }
                $preserve = $y -lt 32 -or $y -ge 76 -or $x -eq 0 -or $x -eq 15 -or $y % 16 -eq 0 -or $y % 16 -eq 15
                if ($preserve -and $old.ToArgb() -ne $new.ToArgb()) { throw 'A protected pixel changed.' }
            }
        }
    } finally { $oldStrip.Dispose() }
    if ($transparent -ne 53) { throw 'Expected exactly the 53 approved transparent pixels.' }
    $strip.Save((Join-Path $PSScriptRoot 'Mountain-Front-16x96.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output ('Verified 80 matching seam pairs, 53 unchanged transparent pixels; total edited pixels: ' + $totalChanged)
} finally { $strip.Dispose() }
