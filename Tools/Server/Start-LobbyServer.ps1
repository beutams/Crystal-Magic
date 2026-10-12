# Run this from the uploaded Build/LobbyServer folder. No Steam login or graphics device is required.
# Cloud security group and Windows Firewall must separately allow the selected TCP port.
[CmdletBinding()]
param(
    [string]$ListenAddress = '0.0.0.0',
    [ValidateRange(1, 65535)][int]$Port = 10002
)

$ErrorActionPreference = 'Stop'
$parsedListenAddress = $null
if (-not [System.Net.IPAddress]::TryParse($ListenAddress, [ref]$parsedListenAddress)) {
    throw 'ListenAddress must be a local IP address; normally use 0.0.0.0, not the public domain.'
}
$lobbyExecutable = Join-Path $PSScriptRoot 'CrystalMagicLobbyServer.exe'
if (-not (Test-Path -LiteralPath $lobbyExecutable -PathType Leaf)) {
    throw 'Upload the entire Build/LobbyServer folder, including CrystalMagicLobbyServer.exe and its data/DLL files.'
}
$lobbyLogDirectory = Join-Path $PSScriptRoot 'Logs'
New-Item -ItemType Directory -Path $lobbyLogDirectory -Force | Out-Null
$lobbyLogFile = Join-Path $lobbyLogDirectory ('lobby-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$lobbyArguments = @(
    '-batchmode', '-nographics',
    '-lobbyAddress', $parsedListenAddress.ToString(),
    '-lobbyPort', $Port.ToString(),
    '-logFile', ('"{0}"' -f $lobbyLogFile)
)
$lobbyProcess = Start-Process -FilePath $lobbyExecutable -ArgumentList $lobbyArguments `
    -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
Write-Output ('Lobby process started: PID={0}, listen={1}:{2}' -f $lobbyProcess.Id, $parsedListenAddress, $Port)
Write-Output ('Log: ' + $lobbyLogFile)
Write-Output 'Check the log for: [TCP][Server] Listening at 0.0.0.0:10002 (or the selected address/port).'
