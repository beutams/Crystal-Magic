param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
throw 'Historical 47-mask checks no longer describe the installed assets. Use Docs/Art/MountainRuleTiles-v1/Verify.ps1.'
Add-Type -AssemblyName System.Drawing
if (-not ('MountainFootPixels' -as [type])) {
    Add-Type -Path @((Join-Path $PSScriptRoot '..\MountainFoot16\MountainFootPixels.cs'), (Join-Path $PSScriptRoot 'MountainSeamChecks.cs'))
}
$manifest = Get-Content -Raw (Join-Path $PSScriptRoot 'manifest.json') | ConvertFrom-Json
$directions = @('0,1', '1,1', '1,0', '1,-1', '0,-1', '-1,-1', '-1,0', '-1,1')
$familyMeta = Get-Content -Raw (Join-Path $ProjectRoot 'Assets\Scripts\Core\Scene\Dungeon\MountainRuleTile.cs.meta')
$familyGuid = [regex]::Match($familyMeta, 'guid: ([a-f0-9]+)').Groups[1].Value
$grass = [System.Drawing.ColorTranslator]::FromHtml('#89A043').ToArgb()
$summit = [System.Drawing.ColorTranslator]::FromHtml('#B3995C').ToArgb()
$checks = 0; $edgeChecks = 0; $spriteCount = 0; $allPixels = @{}
if ($manifest.Count -ne 3) { throw 'Exactly one active set of three mountain parts is required.' }
foreach ($entry in $manifest) {
    $name = $entry.name
    $texturePath = Join-Path $ProjectRoot $entry.texture
    $meta = Get-Content -Raw ($texturePath + '.meta')
    $guid = [regex]::Match($meta, 'guid: ([a-f0-9]+)').Groups[1].Value
    $asset = Get-Content -Raw (Join-Path $ProjectRoot $entry.rule)
    if ($guid -ne $entry.guid -or $asset -notmatch "m_Script:.*guid: $familyGuid" -or $asset -notmatch 'Family: PrairieMountain') { throw "Invalid family/GUID: $name" }
    if ($asset -notmatch "Part: $($entry.role)") { throw "Invalid mountain part: $name" }
    foreach ($setting in @('filterMode: 0', 'enableMipMap: 0', 'spritePixelsToUnits: 16', 'spriteMode: 2', 'textureCompression: 0')) {
        if (-not $meta.Contains($setting)) { throw "Missing import setting $setting" }
    }
    $sprites = [regex]::Matches($meta, '(?ms)^    - serializedVersion: 2\r?\n      name: (?<name>[^\r\n]+).*?        x: (?<x>\d+)\r?\n        y: (?<y>\d+)\r?\n        width: 16\r?\n        height: 16.*?      internalID: (?<id>\d+)')
    $rules = [regex]::Matches($asset, '(?ms)^  - m_Id: (?<id>\d+)\r?\n(?<body>.*?)(?=^  - m_Id:|^  Family:|\z)')
    $expectedCount = 47
    $expectedRules = 47
    if ($sprites.Count -ne $expectedCount -or $rules.Count -ne $expectedRules -or $entry.slices.Count -ne $expectedCount) { throw "Wrong count: $name" }
    $spriteById = @{}; $sliceById = @{}
    foreach ($sprite in $sprites) { $spriteById[[int]$sprite.Groups['id'].Value] = $sprite }
    foreach ($slice in $entry.slices) { $sliceById[[int]$slice.id] = $slice }
    $pixels = [int[][]]::new(256)
    $bitmap = [System.Drawing.Bitmap]::FromFile($texturePath)
    try {
        if ($bitmap.Width -ne $entry.width -or $bitmap.Height -ne $entry.height) { throw 'Wrong atlas dimensions.' }
        foreach ($slice in $entry.slices) {
            $sprite = $spriteById[[int]$slice.id]
            if (-not $sprite -or $slice.cap -or $sprite.Groups['name'].Value -ne $slice.name) { throw 'Wrong sprite reference or residual cap.' }
            $tile = [int[]]::new(256)
            $sx = [int]$sprite.Groups['x'].Value
            $sy = $bitmap.Height - [int]$sprite.Groups['y'].Value - 16
            for ($y = 0; $y -lt 16; $y++) { for ($x = 0; $x -lt 16; $x++) {
                $pixel = $bitmap.GetPixel(($sx + $x), ($sy + $y))
                if ($pixel.A -ne 255) { throw 'Unexpected transparent pixel.' }
                $tile[$y * 16 + $x] = $pixel.ToArgb()
            } }
            $pixels[$slice.mask] = $tile
            $spriteCount++
        }
        $defaultRef = [regex]::Match($asset, 'm_DefaultSprite: \{fileID: (\d+), guid: ([a-f0-9]+)')
        if (-not $spriteById.ContainsKey([int]$defaultRef.Groups[1].Value) -or $defaultRef.Groups[2].Value -ne $guid) { throw 'Invalid default sprite.' }
        if ($entry.role -eq 2) {
            if (@($pixels[255] | Where-Object { $_ -ne $summit }).Count -ne 0) { throw 'Unconnected summit interior must be solid.' }
            foreach ($slice in $entry.slices) {
                if (@($pixels[$slice.mask] | Where-Object { $_ -eq $grass }).Count -gt 0) { throw 'Summit must not transition into grass.' }
                if ($slice.mask -ne 255 -and @($pixels[$slice.mask] | Where-Object { $_ -ne $summit }).Count -eq 0) { throw 'Missing cliff-contact rim.' }
            }
        }
        & {
            $parsedRules = @()
            foreach ($rule in $rules) {
                $body = $rule.Groups['body'].Value
                $ruleId = [int]$rule.Groups['id'].Value
                $reference = [regex]::Match($body, 'fileID: (?<id>\d+), guid: (?<guid>[a-f0-9]+)')
                $slice = $sliceById[[int]$reference.Groups['id'].Value]
                if (-not $slice -or $slice.mask -ne $ruleId -or $reference.Groups['guid'].Value -ne $guid) { throw 'Dangling/mismatched rule sprite reference.' }
                $hex = [regex]::Match($body, 'm_Neighbors: ([a-f0-9]+)').Groups[1].Value
                $positions = [regex]::Matches($body, '\{x: (-?\d+), y: (-?\d+), z: 0\}')
                if ($hex.Length -ne $positions.Count * 8 -or $body -notmatch 'm_RuleTransform: 0') { throw 'Invalid rule geometry.' }
                $tests = @()
                for ($p = 0; $p -lt $positions.Count; $p++) {
                    $direction = [Array]::IndexOf($directions, ($positions[$p].Groups[1].Value + ',' + $positions[$p].Groups[2].Value))
                    $kind = [Convert]::ToInt32($hex.Substring($p * 8, 2), 16)
                    $allowed = if ($entry.role -eq 2) { @(3,4) } else { @(1,2) }
                    if ($direction -lt 0 -or $kind -notin $allowed) { throw 'Invalid neighbour role predicate.' }
                    $tests += @{ Bit = $direction; Present = $kind -eq $(if ($entry.role -eq 2) {4} else {1}) }
                }
                $parsedRules += @{ Id = $ruleId; Tests = $tests }
            }
            for ($raw = 0; $raw -lt 256; $raw++) {
                $matched = @($parsedRules | Where-Object {
                    foreach ($test in $_.Tests) {
                        if ((($raw -band (1 -shl $test.Bit)) -ne 0) -ne $test.Present) { return $false }
                    }
                    return $true
                })
                if ($matched.Count -ne 1 -or $matched[0].Id -ne [MountainFootPixels]::Normalise($raw)) { throw "Wrong match: $name mask=$raw" }
                $script:checks++
            }
        }
        if ($entry.role -ne 2) { $edgeChecks += [MountainFootPixels]::VerifySeams($pixels, $grass) }
        $allPixels[$name] = $pixels
        Write-Output "$name : $expectedCount sprites; $expectedRules transition rules; palette/reference checks passed."
    } finally { $bitmap.Dispose() }
}
$edgeChecks += [MountainSeamChecks]::Vertical($allPixels['MountainWall_Grass1'], $allPixels['MountainFoot_Grass1'], $false)
$edgeChecks += [MountainSeamChecks]::Summit($allPixels['MountainTop_Grass1'], $allPixels['MountainWall_Grass1'])
$table = Get-Content -Raw (Join-Path $ProjectRoot 'Assets\Res\Data\DungeonThemeDataTable.json') | ConvertFrom-Json
$prairie = @($table.Rows | Where-Object ThemeKey -eq 'Prairie')
if ($prairie.Count -ne 1) { throw 'Expected one Prairie theme.' }
$visual = $prairie[0].OpenField.Visual
$configured = @($visual.ObstacleVisual.TransitionRuleTile.AssetPath, $visual.ObstacleVisual.WallRuleTile.AssetPath, $visual.ObstacleVisual.TopRuleTile.AssetPath)
foreach ($ground in $visual.GroundStyles) {
    $configured += @($ground.MountainTransitionRuleTile.AssetPath, $ground.MountainWallRuleTile.AssetPath, $ground.MountainTopRuleTile.AssetPath)
}
$expected = @('MountainFoot_Grass1.asset', 'MountainWall_Grass1.asset', 'MountainTop_Grass1.asset')
for ($i = 0; $i -lt $configured.Count; $i++) {
    if ($configured[$i] -ne ('Assets/Res/Tile/Mountain/' + $expected[$i % 3]) -or -not (Test-Path -LiteralPath (Join-Path $ProjectRoot $configured[$i]))) { throw "Non-shared or invalid mountain path: $($configured[$i])" }
}
Write-Output "PASS: $checks neighbour cases, $edgeChecks edge-pixel checks, $spriteCount active sprites, $($configured.Count) shared configuration paths; summit rims only against cliff faces."
