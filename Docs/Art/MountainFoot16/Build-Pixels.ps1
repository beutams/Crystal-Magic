param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
# The foot atlas now includes summit-contact variants. Keep the three parts in sync.
& (Join-Path $PSScriptRoot '..\MountainComplete16\Build-Pixels.ps1') -ProjectRoot $ProjectRoot
