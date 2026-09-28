param([switch]$Install, [switch]$VerifySeams)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'ApprovedMountainPixels.cs')
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$sourceRoot = Join-Path $PSScriptRoot '../MountainStyleStrip-v1'
$generatedRoot = Join-Path $PSScriptRoot 'Generated'
New-Item -ItemType Directory -Path $generatedRoot -Force | Out-Null
function Read-Tile([string]$name) {
    $image = [System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot ($name + '-16x16.png')))
    try {
        if ($image.Width -ne 16 -or $image.Height -ne 16) { throw "Source is not 16x16: $name" }
        $pixels = [int[]]::new(256)
        for ($y=0; $y -lt 16; $y++) { for ($x=0; $x -lt 16; $x++) { $pixels[$y*16+$x] = $image.GetPixel($x,$y).ToArgb() } }
        return ,$pixels
    } finally { $image.Dispose() }
}
function Save-Pixels([int[]]$pixels, [int]$width, [int]$height, [string]$path, [int]$scale=1) {
    $bitmap = [System.Drawing.Bitmap]::new($width,$height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0,0,$width,$height), [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try { [System.Runtime.InteropServices.Marshal]::Copy($pixels,0,$data.Scan0,$pixels.Length) } finally { $bitmap.UnlockBits($data) }
        if ($scale -eq 1) { $bitmap.Save($path,[System.Drawing.Imaging.ImageFormat]::Png); return }
        $zoom=[System.Drawing.Bitmap]::new([int]($width*$scale),[int]($height*$scale))
        $g=[System.Drawing.Graphics]::FromImage($zoom)
        try {
            $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $g.DrawImage($bitmap,[System.Drawing.Rectangle]::new(0,0,$zoom.Width,$zoom.Height))
            $zoom.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $g.Dispose(); $zoom.Dispose() }
    } finally { $bitmap.Dispose() }
}
function Stable-Id([string]$name) {
    $hash = [System.Security.Cryptography.MD5]::HashData([System.Text.Encoding]::UTF8.GetBytes($name))
    return [Convert]::ToHexString($hash).ToLowerInvariant()
}
$baker = [ApprovedMountainPixels]::new((Read-Tile '01-Foot'),(Read-Tile '02-Wall'),(Read-Tile '03-Summit-Front'),(Read-Tile '04-Summit'),(Read-Tile '05-Summit-Back'),(Read-Tile '06-Grass'))
if($VerifySeams){Write-Output ($baker.VerifySeams())}
$scriptMeta = Get-Content -Raw (Join-Path $projectRoot 'Assets/Scripts/Core/Scene/Dungeon/MountainRuleTile.cs.meta')
$scriptGuid = [regex]::Match($scriptMeta,'guid: ([a-f0-9]+)').Groups[1].Value
$entries = @()
for ($part=0; $part -lt 3; $part++) {
    $name = @('MountainFoot_Grass1','MountainWall_Grass1','MountainTop_Grass1')[$part]
    $folder = if($part -eq 0){'MountainFoot16'}else{'MountainUpper16'}
    $texture = "Assets/Res/Sprites/Dungeon/$folder/$name.png"
    $rule = "Assets/Res/Tile/Mountain/$name.asset"
    $oldMeta = Get-Content -Raw (Join-Path $projectRoot ($texture+'.meta'))
    $guid = [regex]::Match($oldMeta,'guid: ([a-f0-9]+)').Groups[1].Value
    $count=$baker.Tiles[$part].Count; $columns=32; $width=$columns*16; $height=[int][Math]::Ceiling($count/[double]$columns)*16
    $textureOut=Join-Path $generatedRoot $texture
    $ruleOut=Join-Path $generatedRoot $rule
    New-Item -ItemType Directory -Path (Split-Path $textureOut),(Split-Path $ruleOut) -Force | Out-Null
    Save-Pixels ($baker.Atlas($part,$columns)) $width $height $textureOut
    $meta=[System.Text.StringBuilder]::new()
    $prefix=$oldMeta.Substring(0,$oldMeta.IndexOf('    sprites:'))
    [void]$meta.Append($prefix)
    [void]$meta.AppendLine('    sprites:')
    $fileIds=[System.Text.StringBuilder]::new()
    $refs=[System.Text.StringBuilder]::new()
    for($index=0;$index -lt $count;$index++) {
        $sliceName=$name+'_'+$index.ToString('D4'); $id=21300000+$index*2
        $sx=($index%$columns)*16; $sy=$height-([int][Math]::Floor($index/$columns)+1)*16
        $spriteId=Stable-Id ($name+'/'+$index)
        [void]$meta.AppendLine(@"
    - serializedVersion: 2
      name: $sliceName
      rect:
        serializedVersion: 2
        x: $sx
        y: $sy
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
      spriteID: $spriteId
      internalID: $id
      vertices: []
      indices:
      edges: []
      weights: []
"@)
        [void]$fileIds.AppendLine("      ${sliceName}: $id")
        [void]$refs.AppendLine("  - {fileID: $id, guid: $guid, type: 3}")
    }
    $atlasId=Stable-Id $name
    [void]$meta.AppendLine(@"
    outline: []
    physicsShape: []
    bones: []
    spriteID: $atlasId
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable:
"@)
    [void]$meta.Append($fileIds)
    [void]$meta.AppendLine("  mipmapLimitGroupName: `n  pSDRemoveMatte: 0`n  userData: `n  assetBundleName: `n  assetBundleVariant:")
    $metaText=$meta.ToString().Replace('maxTextureSize: 2048','maxTextureSize: 4096')
    [System.IO.File]::WriteAllText($textureOut+'.meta',$metaText,[System.Text.UTF8Encoding]::new($false))
    $lookup=$baker.Lookup[$part]
    $bytes=[byte[]]::new($lookup.Length*4); [Buffer]::BlockCopy($lookup,0,$bytes,0,$bytes.Length)
    $lookupHex=[Convert]::ToHexString($bytes).ToLowerInvariant()
    $defaultKey=if($part -eq 0){[ApprovedMountainPixels]::Encode([int[]]@(1,1,1,0,0,0,1,1))}elseif($part -eq 1){3280}else{6560}
    $defaultId=21300000+$lookup[$defaultKey]*2
    $asset=@"
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
  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}
  m_Name: $name
  m_EditorClassIdentifier:
  m_DefaultSprite: {fileID: $defaultId, guid: $guid, type: 3}
  m_DefaultGameObject: {fileID: 0}
  m_DefaultColliderType: 0
  m_TilingRules:
  - m_Id: 0
    m_Sprites:
    - {fileID: $defaultId, guid: $guid, type: 3}
    m_GameObject: {fileID: 0}
    m_MinAnimationSpeed: 1
    m_MaxAnimationSpeed: 1
    m_PerlinScale: 0.5
    m_Output: 0
    m_ColliderType: 0
    m_RandomTransform: 0
    m_Neighbors: 0000000000000000000000000000000000000000000000000000000000000000
    m_NeighborPositions:
    - {x: 0, y: 1, z: 0}
    - {x: 1, y: 1, z: 0}
    - {x: 1, y: 0, z: 0}
    - {x: 1, y: -1, z: 0}
    - {x: 0, y: -1, z: 0}
    - {x: -1, y: -1, z: 0}
    - {x: -1, y: 0, z: 0}
    - {x: -1, y: 1, z: 0}
    m_RuleTransform: 0
  Family: PrairieMountain
  Part: $part
  VariantSprites:
$($refs.ToString())  VariantLookup: $lookupHex
"@
    [System.IO.File]::WriteAllText($ruleOut,$asset,[System.Text.UTF8Encoding]::new($false))
    $entries += [pscustomobject]@{name=$name;part=$part;texture=$texture;rule=$rule;guid=$guid;count=$count;width=$width;height=$height;columns=$columns;defaultKey=$defaultKey;lookup=$lookup}
    Write-Output "$name : $count unique 16x16 sprites for 6561 ternary neighbourhoods; atlas ${width}x${height}."
}
[System.IO.File]::WriteAllText((Join-Path $generatedRoot 'manifest.json'),($entries | ConvertTo-Json -Depth 8),[System.Text.UTF8Encoding]::new($false))

# Render the real layout export (including concave fronts and mixed heights).
$layoutPath=Join-Path $projectRoot 'Temp/MountainFootValidation/layout-preview.json'
if(Test-Path -LiteralPath $layoutPath){
$layout=Get-Content -Raw $layoutPath | ConvertFrom-Json
$map=[int[,]]::new([int]$layout.width,[int]$layout.height)
for($y=0;$y -lt $layout.height;$y++){for($x=0;$x -lt $layout.width;$x++){$map[$x,$y]=-1}}
foreach($cell in $layout.cells){$map[[int]$cell.x,([int]$layout.height-1-[int]$cell.y)]=[int]$cell.part}
Save-Pixels ($baker.Preview($map,$baker.Grass)) ([int]$layout.width*16) ([int]$layout.height*16) (Join-Path $PSScriptRoot 'Runtime-layout-preview.png') 2
}else{Write-Warning 'Runtime layout export is unavailable; atlas build still succeeds. The saved runtime preview is not refreshed.'}

# The five approved roles in a broad plateau: foot, wall, front fold, fill, back rim.
$sample=[int[,]]::new(12,9)
for($y=0;$y -lt 9;$y++){for($x=0;$x -lt 12;$x++){$sample[$x,$y]=-1}}
for($x=1;$x -lt 11;$x++){$sample[$x,2]=2;$sample[$x,3]=2;$sample[$x,4]=1;$sample[$x,5]=1;$sample[$x,6]=0}
Save-Pixels ($baker.Preview($sample,$baker.Grass)) 192 144 (Join-Path $PSScriptRoot 'Approved-style-plateau-preview.png') 4

if($Install){
    $backupRoot=Join-Path $PSScriptRoot 'Before-install'
    foreach($entry in $entries){
        foreach($relative in @($entry.texture,($entry.texture+'.meta'),$entry.rule)){
            $target=Join-Path $projectRoot $relative; $backup=Join-Path $backupRoot $relative
            if(-not(Test-Path -LiteralPath $backup)){New-Item -ItemType Directory -Path (Split-Path $backup) -Force|Out-Null;Copy-Item -LiteralPath $target -Destination $backup}
            Copy-Item -LiteralPath (Join-Path $generatedRoot $relative) -Destination $target -Force
        }
    }
    Write-Output 'Installed in existing asset paths; prior textures/metadata/RuleTiles preserved in Before-install. Asset GUIDs and all theme references remain stable.'
}
