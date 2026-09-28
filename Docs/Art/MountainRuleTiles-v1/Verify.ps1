$ErrorActionPreference = 'Stop'
# Rebuild only the offline outputs, then compare the installed assets to them.
. (Join-Path $PSScriptRoot 'Build.ps1') -VerifySeams
$spriteTotal=0; $caseTotal=0; $pixelTotal=0
foreach($entry in $entries){
    $path=Join-Path $projectRoot $entry.texture
    $meta=Get-Content -Raw ($path+'.meta')
    $asset=Get-Content -Raw (Join-Path $projectRoot $entry.rule)
    foreach($setting in @('filterMode: 0','enableMipMap: 0','spriteMode: 2','spritePixelsToUnits: 16','textureCompression: 0','alphaIsTransparency: 1')){
        if(-not $meta.Contains($setting)){throw "Missing import setting: $setting"}
    }
    if($meta -notmatch "guid: $($entry.guid)" -or $asset -notmatch "m_Script:.*guid: $scriptGuid" -or $asset -notmatch "Part: $($entry.part)" -or $asset -notmatch 'Family: PrairieMountain'){throw 'Wrong script, family, part or texture GUID.'}
    $sprites=[regex]::Matches($meta,'(?ms)^    - serializedVersion: 2\r?\n      name: (?<name>[^\r\n]+).*?        x: (?<x>\d+)\r?\n        y: (?<y>\d+)\r?\n        width: 16\r?\n        height: 16.*?      internalID: (?<id>\d+)')
    if($sprites.Count -ne $entry.count){throw 'Sprite import count differs from the atlas.'}
    $references=[regex]::Match($asset,'(?s)  VariantSprites:\s*(.*?)  VariantLookup:').Groups[1].Value
    $refs=[regex]::Matches($references,'fileID: (\d+), guid: ([a-f0-9]+)')
    if($refs.Count -ne $entry.count){throw 'Missing variant sprite reference.'}
    for($i=0;$i -lt $entry.count;$i++){
        $expectedId=21300000+$i*2; $sprite=$sprites[$i]
        if([int]$refs[$i].Groups[1].Value -ne $expectedId -or $refs[$i].Groups[2].Value -ne $entry.guid -or [int]$sprite.Groups['id'].Value -ne $expectedId){throw 'Wrong sprite fileID/GUID mapping.'}
        if([int]$sprite.Groups['x'].Value -ne ($i%$entry.columns*16) -or [int]$sprite.Groups['y'].Value -ne ($entry.height-([int][Math]::Floor($i/$entry.columns)+1)*16)){throw 'Sprite coordinates do not address the baked tile.'}
    }
    $hex=[regex]::Match($asset,'VariantLookup: ([a-f0-9]+)').Groups[1].Value
    if($hex.Length -ne 6561*8){throw 'Lookup must contain all 6561 ternary cases.'}
    $bytes=[Convert]::FromHexString($hex); $lookup=[int[]]::new(6561); [Buffer]::BlockCopy($bytes,0,$lookup,0,$bytes.Length)
    for($key=0;$key -lt 6561;$key++){
        if($lookup[$key] -ne $entry.lookup[$key] -or $lookup[$key] -lt 0 -or $lookup[$key] -ge $entry.count){throw "Invalid lookup key: $key"}
    }
    $defaultId=21300000+$lookup[$entry.defaultKey]*2
    if($asset -notmatch "m_DefaultSprite: \{fileID: $defaultId, guid: $($entry.guid)"){throw 'Wrong default sprite.'}
    $dependency=[regex]::Match($asset,'m_Neighbors: ([a-f0-9]+)').Groups[1].Value
    if($dependency -ne ('0'*64)){throw 'Expected the eight-position RuleTile refresh dependency.'}
    $bitmap=[System.Drawing.Bitmap]::FromFile($path)
    try{
        if($bitmap.Width -ne $entry.width -or $bitmap.Height -ne $entry.height){throw 'Wrong atlas dimensions.'}
        $pixels=[int[]]::new($bitmap.Width*$bitmap.Height)
        $locked=$bitmap.LockBits([System.Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height),[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try{[System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0,$pixels,0,$pixels.Length)}finally{$bitmap.UnlockBits($locked)}
        $pixelTotal += $baker.VerifyAtlas($entry.part,$pixels,$entry.width,$entry.columns)
    }finally{$bitmap.Dispose()}
    $spriteTotal+=$entry.count; $caseTotal+=6561
}
$table=Get-Content -Raw (Join-Path $projectRoot 'Assets/Res/Data/DungeonThemeDataTable.json')|ConvertFrom-Json
$prairie=@($table.Rows|Where-Object ThemeKey -eq 'Prairie')
if($prairie.Count -ne 1){throw 'Expected one Prairie configuration.'}
$visual=$prairie[0].OpenField.Visual
$configured=@($visual.ObstacleVisual.TransitionRuleTile.AssetPath,$visual.ObstacleVisual.WallRuleTile.AssetPath,$visual.ObstacleVisual.TopRuleTile.AssetPath)
foreach($ground in $visual.GroundStyles){$configured+=@($ground.MountainTransitionRuleTile.AssetPath,$ground.MountainWallRuleTile.AssetPath,$ground.MountainTopRuleTile.AssetPath)}
for($i=0;$i -lt $configured.Count;$i++){
    if($configured[$i] -ne $entries[$i%3].rule){throw "Theme not configured: $($configured[$i])"}
}
$report=[ordered]@{passed=$true;source='MountainStyleStrip-v1';activeSprites=$spriteTotal;neighbourCases=$caseTotal;savedPixels=$pixelTotal;seamPixelPairs=7558272;themeReferences=$configured.Count;nativeUnityValidation='Run Tools/Crystal Magic/Validate Mountain RuleTiles after Unity imports.'}
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'verification.json'),($report|ConvertTo-Json),[System.Text.UTF8Encoding]::new($false))
Write-Output ("PASS: $spriteTotal sprites, $caseTotal neighbour cases, $pixelTotal saved pixels, $($configured.Count) active theme references.")
