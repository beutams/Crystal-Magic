$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$project = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$spritePath = Join-Path $project 'Assets/Res/Sprites/Dungeon/Abyss/AbyssSingleCell.png'
$tilePath = Join-Path $project 'Assets/Res/Tile/Abyss/AbyssSingleCell.asset'
$utf8 = [Text.UTF8Encoding]::new($false)
function Stable-Guid([string]$key) {
    return [Convert]::ToHexString([Security.Cryptography.MD5]::HashData([Text.Encoding]::UTF8.GetBytes('CrystalMagic/AbyssSingleCell/'+$key))).ToLowerInvariant()
}
function Normalize-Mask([int]$mask) {
    foreach($corner in @(@(2,1,4),@(8,4,16),@(32,16,64),@(128,64,1))) {
        if(($mask -band $corner[1]) -eq 0 -or ($mask -band $corner[2]) -eq 0) {$mask=$mask -band (-bnot $corner[0])}
    }
    return $mask
}

# These are the APPROVED masters, not freshly generated/recoloured artwork.
# Bits clockwise from north: N NE E SE S SW W NW. One means another abyss cell.
$masterNames = @{
    124='straight-N';241='straight-E';199='straight-S';31='straight-W';
    28='outer-NW';112='outer-NE';193='outer-SE';7='outer-SW';
    127='inner-NW';253='inner-NE';247='inner-SE';223='inner-SW';255='black'
}
$masters=@{}
foreach($key in $masterNames.Keys) {$masters[[string]$masterNames[$key]]=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot ('Masters/'+$masterNames[$key]+'.png')))}
$masks=@(124,241,199,31,28,112,193,7,127,253,247,223,255)+@(0..255 | ForEach-Object {Normalize-Mask $_} | Sort-Object -Unique | Where-Object {!$masterNames.Contains($_)})
if($masks.Count -ne 47){throw 'Expected 47 normalized neighbour shapes'}
$atlas=[Drawing.Bitmap]::new(128,96)
$spriteEntries=[Text.StringBuilder]::new()
$nameTable=[Text.StringBuilder]::new()
$ruleEntries=[Text.StringBuilder]::new()
$manifest=@()
$sheetGuid=Stable-Guid 'sprite-sheet'
$assetGuid=Stable-Guid 'rule-tile'
$positions=@(@(0,1),@(1,1),@(1,0),@(1,-1),@(0,-1),@(-1,-1),@(-1,0),@(-1,1))

for($slot=0;$slot -lt $masks.Count;$slot++) {
    $mask=[int]$masks[$slot]
    $sources=@()
    if($masterNames.Contains($mask)) {
        $name='Abyss_'+$masterNames[$mask]
        $sources=@($masterNames[$mask])
    } else {
        $name='Abyss_join_'+$mask.ToString('D3')
        # Rare narrow cells / multiple missing corners only reuse pixels from
        # the same masters. No random variants and no new hand-painted faces.
        foreach($side in @(@(1,'straight-N'),@(4,'straight-E'),@(16,'straight-S'),@(64,'straight-W'))) {
            if(($mask -band [int]$side[0]) -eq 0){$sources+= $side[1]}
        }
        foreach($corner in @(@(128,1,64,'outer-NW','inner-NW'),@(2,1,4,'outer-NE','inner-NE'),@(8,4,16,'outer-SE','inner-SE'),@(32,16,64,'outer-SW','inner-SW'))) {
            $a=($mask -band [int]$corner[1]) -ne 0;$b=($mask -band [int]$corner[2]) -ne 0
            if(!$a -and !$b){$sources+=$corner[3]}
            elseif($a -and $b -and ($mask -band [int]$corner[0]) -eq 0){$sources+=$corner[4]}
        }
    }
    $left=($slot%8)*16;$top=[int][Math]::Floor($slot/8)*16
    for($y=0;$y -lt 16;$y++){for($x=0;$x -lt 16;$x++) {
        # Start from the first actual source, never clamp against black: the
        # approved fade can contain equal-red pixels with a different blue.
        # A single-source master must be copied byte-for-byte.
        $color=$masters[$sources[0]].GetPixel($x,$y)
        foreach($sourceName in $sources) {
            $candidate=$masters[$sourceName].GetPixel($x,$y)
            $candidateGrass=$candidate.G -gt $candidate.R;$currentGrass=$color.G -gt $color.R
            if(($candidateGrass -and !$currentGrass) -or ($candidateGrass -eq $currentGrass -and $candidate.R -gt $color.R)) {$color=$candidate}
        }
        $atlas.SetPixel($left+$x,$top+$y,$color)
    }}
    $fileId=21300000+$slot
    $spriteGuid=Stable-Guid ('mask/'+$mask)
    [void]$spriteEntries.Append(@"
    - serializedVersion: 2
      name: $name
      rect:
        serializedVersion: 2
        x: $left
        y: $(96-$top-16)
        width: 16
        height: 16
      alignment: 0
      pivot: {x: 0.5, y: 0.5}
      border: {x: 0, y: 0, z: 0, w: 0}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: $spriteGuid
      internalID: $fileId
      vertices: []
      indices:
      edges: []
      weights: []

"@)
    [void]$nameTable.AppendLine("      ${name}: $fileId")
    $conditions=@(for($bit=0;$bit -lt 8;$bit++) {
        $value=if(($mask -band (1 -shl $bit)) -ne 0){1}else{2}
        if(($bit%2) -eq 1) {
            $previous=1 -shl ($bit-1);$next=1 -shl (($bit+1)%8)
            if(($mask -band $previous) -eq 0 -or ($mask -band $next) -eq 0){$value=0}
        }
        $value
    })
    $hex=($conditions | ForEach-Object {[Convert]::ToHexString([BitConverter]::GetBytes([int]$_)).ToLowerInvariant()}) -join ''
    [void]$ruleEntries.Append(@"
  - m_Id: $mask
    m_Sprites:
    - {fileID: $fileId, guid: $sheetGuid, type: 3}
    m_GameObject: {fileID: 0}
    m_MinAnimationSpeed: 1
    m_MaxAnimationSpeed: 1
    m_PerlinScale: 0.5
    m_Output: 0
    m_ColliderType: 0
    m_RandomTransform: 0
    m_Neighbors: $hex
    m_NeighborPositions:

"@)
    foreach($position in $positions) {[void]$ruleEntries.AppendLine("    - {x: $($position[0]), y: $($position[1]), z: 0}")}
    [void]$ruleEntries.AppendLine('    m_RuleTransform: 0')
    $manifest+=[ordered]@{mask=$mask;name=$name;fileId=$fileId;pngRect=@($left,$top,16,16);sources=$sources;conditions=$conditions}
}
$atlas.Save($spritePath)
$atlas.Dispose()
foreach($master in $masters.Values){$master.Dispose()}

# Use the project's known importer schema; sprites are point-filtered,
# uncompressed, 16 pixels/unit, no mipmaps and no automatic collision shapes.
$importer=Get-Content -Raw (Join-Path $project 'Assets/Res/Sprites/Dungeon/Abyss/AbyssBlack_16.png.meta')
$importer=$importer -replace '3df3492ce975488693072ecb358b52bf',$sheetGuid
$importer=$importer -replace 'maxTextureSize: 32','maxTextureSize: 128'
$importer=$importer -replace 'spriteMode: 1','spriteMode: 2'
$importer=$importer.Replace('    sprites: []',"    sprites:`n"+$spriteEntries.ToString().TrimEnd())
$importer=$importer.Replace('    nameFileIdTable: {}',"    nameFileIdTable:`n"+$nameTable.ToString().TrimEnd())
[IO.File]::WriteAllText($spritePath+'.meta',$importer,$utf8)
$template=Get-Content -Raw (Join-Path $project 'Assets/Res/Tile/Abyss/AbyssBlack.asset')
$template=$template.Replace('m_Name: AbyssBlack','m_Name: AbyssSingleCell')
$template=$template.Replace('{fileID: 21300000, guid: 3df3492ce975488693072ecb358b52bf, type: 3}',"{fileID: 21300012, guid: $sheetGuid, type: 3}")
$template=$template.Replace('  m_TilingRules: []',"  m_TilingRules:`n"+$ruleEntries.ToString().TrimEnd())
[IO.File]::WriteAllText($tilePath,$template,$utf8)
$assetMeta=(Get-Content -Raw (Join-Path $project 'Assets/Res/Tile/Abyss/AbyssBlack.asset.meta')).Replace('dded5d9e22cb4a4890ed226e69623dda',$assetGuid)
[IO.File]::WriteAllText($tilePath+'.meta',$assetMeta,$utf8)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'atlas-manifest.json'),($manifest | ConvertTo-Json -Depth 8),$utf8)
Write-Output 'Installed 12 unchanged edge masters + black, with deterministic joins for all 256 neighbour masks. One transition cell; one RuleTile asset.'
