<#
.SYNOPSIS
    Starts the auction LocalDB instance and gives you its named pipe, ready to paste
    into SSMS.

.DESCRIPTION
    SSMS 22 cannot resolve the "(localdb)\Name" moniker against SQL Server 2025 LocalDB
    on this machine, though every other client can. Connecting straight to the instance's
    named pipe works, because that path never calls the LocalDB API.

    The catch is that the pipe name is regenerated every time the instance starts, so it
    cannot simply be saved as a favourite. This prints the current one and copies it to
    the clipboard.

    LocalDB also shuts itself down after roughly five minutes idle, which is why the
    instance is started here rather than assumed to be up.

.EXAMPLE
    .\tools\db-pipe.ps1
    Starts the instance if needed, prints the pipe, copies it to the clipboard.

.EXAMPLE
    .\tools\db-pipe.ps1 -Instance MSSQLLocalDB
#>
[CmdletBinding()]
param(
    [string]$Instance = 'AuctionAppLocalDB',

    # Skip the clipboard copy — useful when piping the value somewhere else.
    [switch]$NoClipboard
)

$ErrorActionPreference = 'Stop'

function Get-InstanceField {
    param([string]$Name, [string]$Field)
    $info = & sqllocaldb info $Name 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    ($info | Select-String -Pattern "^$Field\s*:\s*(.+)$").Matches.Groups[1].Value.Trim()
}

if (-not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
    throw 'sqllocaldb was not found. Is SQL Server LocalDB installed?'
}

$state = Get-InstanceField -Name $Instance -Field 'State'
if ($null -eq $state) {
    Write-Host "Instance '$Instance' does not exist. Instances on this machine:" -ForegroundColor Yellow
    & sqllocaldb info
    exit 1
}

if ($state -ne 'Running') {
    Write-Host "Starting '$Instance' (was $state)..." -ForegroundColor Cyan
    & sqllocaldb start $Instance | Out-Null
}

$pipe = Get-InstanceField -Name $Instance -Field 'Instance pipe name'
if ([string]::IsNullOrWhiteSpace($pipe)) {
    throw "'$Instance' started but reported no pipe. Try: sqllocaldb stop $Instance -k, then re-run."
}

Write-Host ''
Write-Host '  Paste this into the SSMS "Server name" box:' -ForegroundColor Green
Write-Host ''
Write-Host "    $pipe" -ForegroundColor White
Write-Host ''
Write-Host '  Authentication: Windows Authentication. Database: AuctionAppDb.' -ForegroundColor DarkGray

if (-not $NoClipboard) {
    try {
        Set-Clipboard -Value $pipe
        Write-Host '  (copied to clipboard)' -ForegroundColor DarkGray
    } catch {
        Write-Host '  (could not reach the clipboard — copy it by hand)' -ForegroundColor Yellow
    }
}

Write-Host ''
$pipe
