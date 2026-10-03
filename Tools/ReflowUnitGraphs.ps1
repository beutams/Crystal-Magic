param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
Add-Type -Path (Join-Path $projectRoot 'Assets/Scripts/Game/Editor/OrderedGraphLayout.cs')
$culture = [Globalization.CultureInfo]::InvariantCulture
$originals = @{}
$plans = [Collections.Generic.List[object]]::new()
$stats = [ordered]@{ BehaviorTrees = 0; BehaviorNodes = 0; StateGraphs = 0; StateNodes = 0; BackEdges = 0; EffectGraphs = 0; EffectContainers = 0 }

function Read-Document([string]$path) {
    $text = [IO.File]::ReadAllText((Join-Path $projectRoot $path))
    $originals[$path] = $text
    return ConvertFrom-Json -InputObject $text -Depth 100
}
function Read-Constant([string]$path, [string]$name) {
    $match = [regex]::Match([IO.File]::ReadAllText((Join-Path $projectRoot $path)), "$name\s*=\s*([\d.]+)f")
    if (!$match.Success) { throw "Missing layout constant: $name" }
    return [float]::Parse($match.Groups[1].Value, $culture)
}
$unitLayout = 'Assets/Scripts/Game/Editor/UnitGraphAutoLayout.cs'
$effectLayout = 'Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphAutoLayout.cs'
$btWidth = Read-Constant $unitLayout 'BehaviorWidth'
$btHeight = Read-Constant $unitLayout 'BehaviorHeight'
$stateWidth = Read-Constant $unitLayout 'StateWidth'
$stateHeight = Read-Constant $unitLayout 'StateHeight'
$effectWidth = Read-Constant $effectLayout 'ContainerWidth'
$effectHeader = Read-Constant $effectLayout 'HeaderHeight'
$effectRow = Read-Constant $effectLayout 'EffectRowHeight'
$effectNodeWidth = Read-Constant $effectLayout 'EffectWidth'
$effectGap = Read-Constant $effectLayout 'EffectGap'
$effectPadding = Read-Constant $effectLayout 'ContainerPadding'

function Assert-Layout($nodes, $edges, $result, [bool]$vertical, [string]$label) {
    if ($result.Positions.Count -ne $nodes.Count) { throw "Duplicate/missing node IDs: $label" }
    $byId = @{}; $back = @{}
    foreach ($node in $nodes) { $byId[$node.Id] = $node }
    foreach ($edge in $result.BackEdges) { $back[$edge.From + '|' + $edge.To] = $true }
    foreach ($edge in $edges) {
        if (!$byId.ContainsKey($edge.From) -or !$byId.ContainsKey($edge.To)) { throw "Dangling edge: $label" }
        $from = $result.Positions[$edge.From]; $to = $result.Positions[$edge.To]
        if ($back.ContainsKey($edge.From + '|' + $edge.To)) { continue }
        $forward = if ($vertical) { $to.Y -ge $from.Y + $byId[$edge.From].Height + 139 } else { $to.X -ge $from.X + $byId[$edge.From].Width + 139 }
        if (!$forward) { throw "Non-forward execution edge: $label / $($edge.From) -> $($edge.To)" }
    }
    for ($i = 0; $i -lt $nodes.Count; $i++) {
        $a = $nodes[$i]; $pa = $result.Positions[$a.Id]
        for ($j = $i + 1; $j -lt $nodes.Count; $j++) {
            $b = $nodes[$j]; $pb = $result.Positions[$b.Id]
            if ($pa.X -lt $pb.X + $b.Width -and $pb.X -lt $pa.X + $a.Width -and $pa.Y -lt $pb.Y + $b.Height -and $pb.Y -lt $pa.Y + $a.Height) {
                throw "Overlapping nodes: $label / $($a.Id), $($b.Id)"
            }
        }
    }
}

# Replace only coordinate values, preserving every other byte of the authored JSON text.
function Replace-Positions([string]$text, [string]$property, $positions) {
    $number = '-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?'
    $pattern = '("' + $property + '"\s*:\s*\{\s*"x"\s*:\s*)' + $number + '(\s*,\s*"y"\s*:\s*)' + $number + '(\s*\})'
    $matches = [regex]::Matches($text, $pattern)
    if ($matches.Count -ne $positions.Count) { throw "$property count mismatch: $($matches.Count) vs $($positions.Count)" }
    $cursor = @{ Index = 0 }
    return [regex]::Replace($text, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m)
        $p = $positions[$cursor.Index]; $cursor.Index++
        return $m.Groups[1].Value + $p.X.ToString('0.###', $culture) + $m.Groups[2].Value + $p.Y.ToString('0.###', $culture) + $m.Groups[3].Value
    })
}
function Assert-GameplayUnchanged([string]$before, [string]$after) {
    $a = [Newtonsoft.Json.Linq.JObject]::Parse($before)
    $b = [Newtonsoft.Json.Linq.JObject]::Parse($after)
    foreach ($doc in @($a, $b)) {
        foreach ($property in @('EditorPosition', 'ViewPosition', 'ViewScale')) {
            foreach ($token in @($doc.SelectTokens('$..' + $property))) { $token.get_Parent().Remove() }
        }
    }
    if (![Newtonsoft.Json.Linq.JToken]::DeepEquals($a, $b)) { throw 'A non-layout field was changed.' }
}

$treePath = 'Assets/Res/Data/BehaviorTreeDataTable.json'
$trees = Read-Document $treePath
$positions = [Collections.Generic.List[object]]::new()
foreach ($tree in $trees.Rows) {
    $nodes = [Collections.Generic.List[CrystalMagic.Editor.OrderedGraphLayout+Node]]::new()
    $edges = [Collections.Generic.List[CrystalMagic.Editor.OrderedGraphLayout+Edge]]::new()
    foreach ($node in $tree.Nodes) {
        $nodes.Add([CrystalMagic.Editor.OrderedGraphLayout+Node]::new($node.Guid, $btWidth, $btHeight))
        foreach ($child in $node.ChildGuids) { $edges.Add([CrystalMagic.Editor.OrderedGraphLayout+Edge]::new($node.Guid, $child)) }
    }
    $result = [CrystalMagic.Editor.OrderedGraphLayout]::Arrange($nodes, $edges, $tree.RootNodeGuid, $true, 140, 90, 80, 80)
    Assert-Layout $nodes $edges $result $true "Behavior/$($tree.Id)"
    foreach ($node in $tree.Nodes) {
        $positions.Add($result.Positions[$node.Guid])
        $lastAtDepth = @{}
        foreach ($child in $node.ChildGuids) {
            $x = $result.Positions[$child].X
            $depth = $result.Depths[$child]
            if ($lastAtDepth.ContainsKey($depth) -and $x -le $lastAtDepth[$depth]) { throw "Behavior sibling order changed: $($tree.Id) / $($node.Guid)" }
            $lastAtDepth[$depth] = $x
        }
    }
    $stats.BehaviorTrees++; $stats.BehaviorNodes += $nodes.Count
}
$treeText = Replace-Positions $originals[$treePath] 'EditorPosition' $positions
Assert-GameplayUnchanged $originals[$treePath] $treeText
$plans.Add(@{ Path = $treePath; Text = $treeText })

$statePath = 'Assets/Res/Data/StateScriptDataTable.json'
$states = Read-Document $statePath
$positions = [Collections.Generic.List[object]]::new()
$views = [Collections.Generic.List[object]]::new()
foreach ($row in $states.Rows) {
    foreach ($graph in $row.Graphs) {
        $nodes = [Collections.Generic.List[CrystalMagic.Editor.OrderedGraphLayout+Node]]::new()
        $edges = [Collections.Generic.List[CrystalMagic.Editor.OrderedGraphLayout+Edge]]::new()
        foreach ($node in $graph.Nodes) { $nodes.Add([CrystalMagic.Editor.OrderedGraphLayout+Node]::new($node.Guid, $stateWidth, $stateHeight)) }
        foreach ($edge in $graph.Edges) { $edges.Add([CrystalMagic.Editor.OrderedGraphLayout+Edge]::new($edge.OutputNodeGuid, $edge.InputNodeGuid)) }
        $result = [CrystalMagic.Editor.OrderedGraphLayout]::Arrange($nodes, $edges, $graph.EntryNodeGuid, $false, 140, 90, 80, 80)
        Assert-Layout $nodes $edges $result $false "State/$($row.Id)/$($graph.Name)"
        foreach ($node in $graph.Nodes) { $positions.Add($result.Positions[$node.Guid]) }
        $entry = $result.Positions[$graph.EntryNodeGuid]
        if ($null -ne $graph.ViewPosition) { $views.Add([CrystalMagic.Editor.OrderedGraphLayout+Position]::new(80 - $entry.X, 80 - $entry.Y)) }
        $stats.StateGraphs++; $stats.StateNodes += $nodes.Count; $stats.BackEdges += $result.BackEdges.Count
    }
}
$stateText = Replace-Positions $originals[$statePath] 'EditorPosition' $positions
$stateText = Replace-Positions $stateText 'ViewPosition' $views
$stateText = [regex]::Replace($stateText, '("ViewScale"\s*:\s*)-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?', '${1}1.0')
Assert-GameplayUnchanged $originals[$statePath] $stateText
$plans.Add(@{ Path = $statePath; Text = $stateText })

# Match EffectGraphModel's declared EffectData[] field order, not arbitrary JSON property order.
$effectFields = @{}
$effectBases = @{}
Get-ChildItem -LiteralPath 'Assets/Scripts/Game/Data/Effects' -Filter '*.cs' | ForEach-Object {
    $source = [IO.File]::ReadAllText($_.FullName)
    $classes = [regex]::Matches($source, 'class\s+(\w+)(?:\s*:\s*(\w+))?')
    for ($i = 0; $i -lt $classes.Count; $i++) {
        $class = $classes[$i]
        $end = if ($i + 1 -lt $classes.Count) { $classes[$i + 1].Index } else { $source.Length }
        $body = $source.Substring($class.Index, $end - $class.Index)
        $effectFields[$class.Groups[1].Value] = @([regex]::Matches($body, 'public\s+EffectData\[\]\s+(\w+)') | ForEach-Object { $_.Groups[1].Value })
        $effectBases[$class.Groups[1].Value] = $class.Groups[2].Value
    }
}
function Get-EffectFields([string]$type) {
    if (!$effectFields.ContainsKey($type)) { throw "Unknown effect type: $type" }
    foreach ($field in $effectFields[$type]) { $field }
    $baseType = $effectBases[$type]
    if ($baseType -and $effectFields.ContainsKey($baseType)) { Get-EffectFields $baseType }
}
$layoutPath = 'Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphLayouts.json'
$layouts = Read-Document $layoutPath
$entries = [Collections.Generic.List[object]]::new()
$entryByKey = @{}
foreach ($entry in $layouts.Entries) { $entries.Add($entry); $entryByKey[$entry.OwnerKey] = $entry }
$seenOwners = @{}
function Arrange-Effects([string]$owner, $effects) {
    $entry = $entryByKey[$owner]
    $saved = @{}
    if ($null -ne $entry) { foreach ($container in $entry.Layout.Containers) { $saved[$container.Path] = $true } }
    $nodes = [Collections.Generic.List[CrystalMagic.Editor.OrderedGraphLayout+Node]]::new()
    $edges = [Collections.Generic.List[CrystalMagic.Editor.OrderedGraphLayout+Edge]]::new()
    function Visit-Effects([string]$path, $items, [string]$parent) {
        if ($null -eq $items) { $items = @() }
        if ($path -ne 'root' -and $items.Count -eq 0 -and !$saved.ContainsKey($path)) { return }
        $width = [Math]::Max($effectWidth, $effectPadding + $items.Count * $effectNodeWidth + [Math]::Max(0, $items.Count - 1) * $effectGap)
        $height = $effectHeader + $(if ($items.Count -gt 0) { $effectRow } else { 0 })
        $nodes.Add([CrystalMagic.Editor.OrderedGraphLayout+Node]::new($path, $width, $height))
        if ($parent) { $edges.Add([CrystalMagic.Editor.OrderedGraphLayout+Edge]::new($parent, $path)) }
        for ($i = 0; $i -lt $items.Count; $i++) {
            $effect = $items[$i]
            if ($null -eq $effect) { continue }
            $type = (($effect.'$type' -split ',')[0] -split '\.')[-1]
            if (!$effectFields.ContainsKey($type)) { throw "Unknown effect type: $owner / $type" }
            foreach ($field in @(Get-EffectFields $type)) { Visit-Effects "$path/$i/$field" $effect.$field $path }
        }
    }
    Visit-Effects 'root' $effects ''
    $result = [CrystalMagic.Editor.OrderedGraphLayout]::Arrange($nodes, $edges, 'root', $true, 140, 90, 80, 220)
    Assert-Layout $nodes $edges $result $true $owner
    $containers = @($nodes | ForEach-Object {
        $p = $result.Positions[$_.Id]
        [ordered]@{ Path = $_.Id; Position = [ordered]@{ x = $p.X; y = $p.Y }; Expanded = $true }
    })
    $newLayout = [ordered]@{ ViewPosition = [ordered]@{ x = 240 - $result.Positions['root'].X - $nodes[0].Width * 0.5; y = 0 }; ViewScale = 1.0; Containers = $containers }
    if ($seenOwners.ContainsKey($owner)) {
        if (($newLayout | ConvertTo-Json -Depth 20 -Compress) -ne $seenOwners[$owner]) { throw "Conflicting effect owner key: $owner" }
        return
    }
    $seenOwners[$owner] = $newLayout | ConvertTo-Json -Depth 20 -Compress
    if ($null -eq $entry) {
        $entry = [pscustomobject]@{ OwnerKey = $owner; Layout = $newLayout }
        $entries.Add($entry); $entryByKey[$owner] = $entry
    } else { $entry.Layout = $newLayout }
    $stats.EffectGraphs++; $stats.EffectContainers += $nodes.Count
}
$skills = Read-Document 'Assets/Res/Data/SkillDataTable.json'
foreach ($row in $skills.Rows) { Arrange-Effects "Skill:$($row.Id):EffectChain" $row.EffectChain }
foreach ($row in $states.Rows) {
    foreach ($graph in $row.Graphs) {
        foreach ($node in $graph.Nodes) {
            if ($null -ne $node.Effects) { Arrange-Effects "StateScript:Node:$($node.Guid):Effects" $node.Effects }
        }
    }
}
$buffs = Read-Document 'Assets/Res/Data/BuffDataTable.json'
foreach ($row in $buffs.Rows) {
    for ($i = 0; $i -lt $row.TriggerEntries.Count; $i++) { Arrange-Effects "Buff:$($row.Id):TriggerEntries[$i].Effects" $row.TriggerEntries[$i].Effects }
}
$additions = Read-Document 'Assets/Res/Data/SkillAdditionDataTable.json'
foreach ($row in $additions.Rows) {
    for ($i = 0; $i -lt $row.Callbacks.Count; $i++) {
        for ($j = 0; $j -lt $row.Callbacks[$i].Actions.Count; $j++) {
            $action = $row.Callbacks[$i].Actions[$j]
            if ($null -ne $action.Effects) { Arrange-Effects "SkillAddition:$($row.Id):Callbacks[$i].Actions[$j].Effects" $action.Effects }
        }
    }
}
$props = Read-Document 'Assets/Res/Data/PropDataTable.json'
foreach ($row in $props.Rows) { Arrange-Effects "Prop:$($row.Id):EffectChain" $row.EffectChain }
$layoutText = [ordered]@{ Version = $layouts.Version; Entries = @($entries.ToArray()) } | ConvertTo-Json -Depth 30
$plans.Add(@{ Path = $layoutPath; Text = $layoutText + [Environment]::NewLine })

# Do all validation first and refuse to overwrite files changed while the tool was running.
foreach ($path in $originals.Keys) {
    if ([IO.File]::ReadAllText((Join-Path $projectRoot $path)) -cne $originals[$path]) { throw "File changed during layout: $path" }
}
if ($Apply) {
    $backup = Join-Path $projectRoot ('Temp/GraphLayoutBackup-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff'))
    [IO.Directory]::CreateDirectory($backup) | Out-Null
    foreach ($plan in $plans) {
        $path = Join-Path $projectRoot $plan.Path
        [IO.File]::Copy($path, (Join-Path $backup ([IO.Path]::GetFileName($path))))
        [IO.File]::WriteAllText($path, $plan.Text, [Text.UTF8Encoding]::new($true))
    }
    Write-Output "Applied layouts. Backup: $backup"
} else { Write-Output 'Validation only; pass -Apply to persist layouts.' }
Write-Output ($stats | ConvertTo-Json -Compress)
Write-Output 'PASS: no rectangle overlaps, forward-edge ordering, behavior sibling ordering, and unchanged gameplay JSON.'
