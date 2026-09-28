param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
& (Join-Path $PSScriptRoot '..\MountainComplete16\Verify-Assets.ps1') -ProjectRoot $ProjectRoot
