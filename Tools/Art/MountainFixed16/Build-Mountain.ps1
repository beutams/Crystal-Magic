$ErrorActionPreference='Stop'
$project=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$spritePath=Join-Path $project 'Assets/Res/Sprites/Dungeon/MountainFixed16.png'
$utf8=[Text.UTF8Encoding]::new($false)
function Stable-Guid([string]$key) {
    [Convert]::ToHexString([Security.Cryptography.MD5]::HashData([Text.Encoding]::UTF8.GetBytes('CrystalMagic/MountainFixed16/'+$key))).ToLowerInvariant()
}
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'BakeMountain.cs') -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.GdiPlus,System.Private.Windows.Core,System.Collections
$result=[BakeMountain]::Run((Join-Path $PSScriptRoot 'Masters'),$spritePath)
$sheetGuid=Stable-Guid 'sheet'
$spriteEntries=[Text.StringBuilder]::new()
$nameTable=[Text.StringBuilder]::new()
foreach($sprite in $result.Sprites) {
    $name=$sprite.Name;$id=21300000+$sprite.Slot;$guid=Stable-Guid $name
    $x=($sprite.Slot%16)*16;$y=$result.Height-([int][Math]::Floor($sprite.Slot/16)+1)*16
    [void]$spriteEntries.Append(@"
    - serializedVersion: 2
      name: $name
      rect:
        serializedVersion: 2
        x: $x
        y: $y
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
      spriteID: $guid
      internalID: $id
      vertices: []
      indices:
      edges: []
      weights: []

"@)
    [void]$nameTable.AppendLine("      ${name}: $id")
}
$importer=Get-Content -Raw (Join-Path $project 'Assets/Res/Sprites/Dungeon/Abyss/AbyssBlack_16.png.meta')
$importer=$importer -replace '3df3492ce975488693072ecb358b52bf',$sheetGuid
$importer=$importer -replace 'maxTextureSize: 32','maxTextureSize: 512' -replace 'spriteMode: 1','spriteMode: 2'
$importer=$importer.Replace('    sprites: []',"    sprites:`n"+$spriteEntries.ToString().TrimEnd())
$importer=$importer.Replace('    nameFileIdTable: {}',"    nameFileIdTable:`n"+$nameTable.ToString().TrimEnd())
[IO.File]::WriteAllText($spritePath+'.meta',$importer,$utf8)
$positions=@(@(0,1),@(1,1),@(1,0),@(1,-1),@(0,-1),@(-1,-1),@(-1,0),@(-1,1))
foreach($part in 0..2) {
    $name=@('MountainFoot_Fixed16','MountainWall_Fixed16','MountainTop_Fixed16')[$part]
    $defaultKey=if($part -eq 2){6560}else{3280}
    $defaultId=21300000+$result.Lookups[$part][$defaultKey]
    $asset=[Text.StringBuilder]::new()
    [void]$asset.Append(@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 9c618c3b62dd950c2ea4c272127a7ab2, type: 3}
  m_Name: $name
  m_EditorClassIdentifier:
  m_DefaultSprite: {fileID: $defaultId, guid: $sheetGuid, type: 3}
  m_DefaultGameObject: {fileID: 0}
  m_DefaultColliderType: 0
  m_TilingRules:
  - m_Id: 0
    m_Sprites:
    - {fileID: $defaultId, guid: $sheetGuid, type: 3}
    m_GameObject: {fileID: 0}
    m_MinAnimationSpeed: 1
    m_MaxAnimationSpeed: 1
    m_PerlinScale: 0.5
    m_Output: 0
    m_ColliderType: 0
    m_RandomTransform: 0
    m_Neighbors: 0000000000000000000000000000000000000000000000000000000000000000
    m_NeighborPositions:

"@)
    foreach($pos in $positions){[void]$asset.AppendLine("    - {x: $($pos[0]), y: $($pos[1]), z: 0}")}
    [void]$asset.AppendLine("    m_RuleTransform: 0`n  Family: PrairieMountainFixed16`n  Part: $part`n  VariantSprites:")
    foreach($sprite in $result.Sprites){[void]$asset.AppendLine("  - {fileID: $(21300000+$sprite.Slot), guid: $sheetGuid, type: 3}")}
    $hex=($result.Lookups[$part] | ForEach-Object {[Convert]::ToHexString([BitConverter]::GetBytes([int]$_)).ToLowerInvariant()}) -join ''
    [void]$asset.AppendLine("  VariantLookup: $hex")
    $path=Join-Path $project ('Assets/Res/Tile/Mountain/'+$name+'.asset')
    [IO.File]::WriteAllText($path,$asset.ToString(),$utf8)
    $guid=Stable-Guid $name
    [IO.File]::WriteAllText($path+'.meta',"fileFormatVersion: 2`nguid: $guid`nNativeFormatImporter:`n  externalObjects: {}`n  mainObjectFileID: 11400000`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n",$utf8)
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'atlas-manifest.json'),($result | ConvertTo-Json -Depth 8 -Compress),$utf8)
Write-Output "Baked 27 unchanged masters + deterministic joins: $($result.Sprites.Count) sprites, 3 RuleTiles, $($result.Width)x$($result.Height)."
