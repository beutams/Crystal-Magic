param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
throw 'Historical mountain pipeline is retired. Use Docs/Art/MountainRuleTiles-v1/Build.ps1 -Install to rebuild the approved v1 assets.'
Add-Type -AssemblyName System.Drawing
if (-not ('MountainFootPixels' -as [type])) {
    Add-Type -Path @((Join-Path $PSScriptRoot '..\MountainFoot16\MountainFootPixels.cs'), (Join-Path $PSScriptRoot 'MountainSeamChecks.cs'))
}
function Rgb([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex).ToArgb() }
$palette = [int[]]@((Rgb '#A08043'), (Rgb '#B3995C'), (Rgb '#805B32'), (Rgb '#514332'))
$grasses = @('#89A043')
$shadows = @('#647B36')
$masks = [MountainFootPixels]::Masks()
function Sample-Rock([string]$file, [int]$sx, [int]$sy, [int]$stepX, [int]$stepY) {
    $source = [System.Drawing.Bitmap]::FromFile($file)
    $result = [int[]]::new(256)
    try {
        for ($y = 0; $y -lt 16; $y++) { for ($x = 0; $x -lt 16; $x++) {
            $sample = $source.GetPixel(($sx + $x * $stepX), ($sy + $y * $stepY))
            $best = [double]::PositiveInfinity
            foreach ($argb in $palette) {
                $c = [System.Drawing.Color]::FromArgb($argb)
                $distance = [Math]::Pow(($sample.R - $c.R), 2) + [Math]::Pow(($sample.G - $c.G), 2) + [Math]::Pow(($sample.B - $c.B), 2)
                if ($distance -lt $best) { $best = $distance; $result[$y * 16 + $x] = $argb }
            }
        } }
    } finally { $source.Dispose() }
    return ,$result
}
$sourceRoot = Join-Path $ProjectRoot 'Docs\Art\MountainCliff-v2'
$footRock = Sample-Rock (Join-Path $sourceRoot '01-Front-And-Foot.png') 230 260 20 36
for ($y = 0; $y -lt 16; $y++) { $footRock[$y * 16 + 15] = $footRock[$y * 16] }
for ($x = 0; $x -lt 16; $x++) { $footRock[240 + $x] = $footRock[$x] }
$wallRock = Sample-Rock (Join-Path $sourceRoot '02-Sides-And-Corners.png') 410 230 26 36
# Reuse the foot's interface pixels so body/foot transitions do not change their colours.
for ($p = 0; $p -lt 16; $p++) {
    $wallRock[$p] = $footRock[$p]; $wallRock[240 + $p] = $footRock[240 + $p]
    $wallRock[$p * 16] = $footRock[$p * 16]; $wallRock[$p * 16 + 15] = $footRock[$p * 16 + 15]
}
$summitSample = Sample-Rock (Join-Path $sourceRoot '03-Summit-Transition.png') 440 340 14 14
$summitColor = $summitSample[8 * 16 + 8]
if ($summitColor -ne $palette[1]) { throw 'Summit centre colour unexpectedly changed.' }
function Save-Zoom([System.Drawing.Bitmap]$bitmap, [string]$path, [int]$factor) {
    $large = [System.Drawing.Bitmap]::new(($bitmap.Width * $factor), ($bitmap.Height * $factor))
    $g = [System.Drawing.Graphics]::FromImage($large)
    try {
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $g.DrawImage($bitmap, [System.Drawing.Rectangle]::new(0, 0, $large.Width, $large.Height))
        $large.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $g.Dispose(); $large.Dispose() }
}
function Save-Atlas([string]$name, [int[][]]$tiles, [int[]]$tileMasks, [string]$folder, [int]$grass) {
    $count = $tileMasks.Count
    $width = [Math]::Min(8, $count) * 16
    $height = [int][Math]::Ceiling($count / 8.0) * 16
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $atlas = [System.Drawing.Bitmap]::new($width, $height)
    try {
        for ($i = 0; $i -lt $height / 16 * ($width / 16); $i++) {
            $pixels = if ($i -lt $count) { $tiles[$tileMasks[$i]] } else { $null }
            for ($y = 0; $y -lt 16; $y++) { for ($x = 0; $x -lt 16; $x++) {
                $color = if ($null -ne $pixels) { $pixels[$y * 16 + $x] } else { $grass }
                $atlas.SetPixel(($i % 8 * 16 + $x), ([Math]::Floor($i / 8) * 16 + $y), [System.Drawing.Color]::FromArgb($color))
            } }
        }
        $atlas.Save((Join-Path $folder ($name + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
        Save-Zoom $atlas (Join-Path $PSScriptRoot ($name + '-atlas.png')) 4
    } finally { $atlas.Dispose() }
}
for ($style = 0; $style -lt $grasses.Count; $style++) {
    $grass = Rgb $grasses[$style]
    $foot = [MountainFootPixels]::Render($footRock, $grass, (Rgb $shadows[$style]), $palette)
    $wall = [MountainFootPixels]::Render($wallRock, $grass, (Rgb $shadows[$style]), $palette)
    $top = [MountainFootPixels]::RenderSummit($wallRock, $summitColor, $palette)
    Save-Atlas 'MountainFoot_Grass1' $foot $masks (Join-Path $ProjectRoot 'Assets\Res\Sprites\Dungeon\MountainFoot16') $grass
    Save-Atlas 'MountainWall_Grass1' $wall $masks (Join-Path $ProjectRoot 'Assets\Res\Sprites\Dungeon\MountainUpper16') $grass
    Save-Atlas 'MountainTop_Grass1' $top $masks (Join-Path $ProjectRoot 'Assets\Res\Sprites\Dungeon\MountainUpper16') $summitColor
    foreach ($tiles in @($foot, $wall)) { [MountainFootPixels]::VerifySeams($tiles, $grass) | Out-Null }

    # Illustrative heights 1/2/4; actual layout regression preview is exported separately.
    $map = [bool[,]]::new(30, 13)
    $parts = [int[,]]::new(30, 13) # 0=foot, 1=wall, 2=summit
    $samples = @(@{X=1;W=6;H=1}, @{X=9;W=7;H=2}, @{X=19;W=8;H=4})
    foreach ($s in $samples) {
        for ($y=2; $y -le 7+$s.H; $y++) { for ($x=$s.X; $x -lt $s.X+$s.W; $x++) {
            if ($s.H -eq 4 -and $y -le 3 -and $x -ge $s.X+5) { continue }
            $map[$x,$y]=$true
            $parts[$x,$y]=if ($y -le 6) {2} elseif ($y -eq 7+$s.H) {0} else {1}
        } }
    }
    $preview = [System.Drawing.Bitmap]::new(480,208)
    try {
        for ($cy=0; $cy -lt 13; $cy++) { for ($cx=0; $cx -lt 30; $cx++) {
            $pixels=$null
            if ($map[$cx,$cy]) {
                $mask=[MountainFootPixels]::MaskAt($map,$cx,$cy)
                $pixels=if ($parts[$cx,$cy] -eq 2) {$top[[MountainFootPixels]::SummitMaskAt($map,$parts,$cx,$cy)]} elseif ($parts[$cx,$cy] -eq 1) {$wall[$mask]} else {$foot[$mask]}
            }
            for ($y=0; $y -lt 16; $y++) { for ($x=0; $x -lt 16; $x++) {
                $color=if($null -eq $pixels){$grass}else{$pixels[$y*16+$x]}
                $preview.SetPixel(($cx*16+$x),($cy*16+$y),[System.Drawing.Color]::FromArgb($color))
            } }
        } }
        Save-Zoom $preview (Join-Path $PSScriptRoot "Mountain-Grass$($style+1)-assembled.png") 3
    } finally { $preview.Dispose() }
    Write-Output 'Shared grass #89A043: foot 47, wall 47, summit 47 (cliff-only rims, solid interior).'
}
