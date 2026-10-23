<#
.SYNOPSIS
  The install step of `dotnet build FlowerPainter.csproj -c Release` (the csproj's DeployStandalone target calls it):
  bin\Release + UI\build into Mods\FlowerPainter, as the user's build.

.DESCRIPTION
  Mods\FlowerPainter is shared with the game queue (ModsInstall.ps1), so this install is guarded like the queue's own:
  * refused (the build fails, nothing is touched) while any Cities2.exe runs, and while a queue game owns the folder;
  * a copy in a staging folder, then two renames, under the queue's install.lock: never a half-written folder;
  * the result is the user's build: the game queue puts it back after each of its games.
  Build with -p:Deploy=false to skip installing (the game queue takes your build from bin\Release).
#>
param([Parameter(Mandatory = $true)][string]$From, [string]$Ui = '', [Parameter(Mandatory = $true)][string]$To)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ModsInstall.ps1')
$full = { param($p) [IO.Path]::GetFullPath($p).TrimEnd('\') }
$sources = @((& $full $From)) + @(if ($Ui -and (Test-Path -LiteralPath $Ui)) { (& $full $Ui) })
$real = Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\FlowerPainter'
$resolvedReal = Join-Path $script:MIUserData 'Mods\FlowerPainter'
$isReal = ((& $full $To) -eq (& $full $real)) -or ((& $full $To) -eq (& $full $resolvedReal))
if ($script:MITest -and $isReal -and (& $full $To) -ne (& $full $script:MI.Mods)) {
    Write-Host "FlowerPainter was NOT installed into $To`: a stand-in game queue (FLOWERPAINTER_GAME_QUEUE_MODS) is set in this shell; it never installs into the real folder"
    exit 1
}
# LocalLow can be a junction: its public C: spelling must still use the guarded E: install,
# never the unguarded custom-directory branch or the stand-in queue refusal.
if ($isReal -and -not $script:MITest) { $To = $script:MI.Mods }
if ((& $full $To) -ne (& $full $script:MI.Mods)) {
    # not the game's folder (a DeployDir of your own): a plain copy, as the csproj always did
    if (Test-Path -LiteralPath $To) { Remove-Item -LiteralPath $To -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $To | Out-Null
    foreach ($s in $sources) { foreach ($c in Get-ChildItem -LiteralPath $s -Force) { Copy-Item -LiteralPath $c.FullName -Destination $To -Recurse -Force } }
    Write-Host "FlowerPainter deployed (standalone, no Burst) to $To"
    exit 0
}
$allow = 0; if ($env:FLOWERPAINTER_SUITE_LOCK_PID) { [void][int]::TryParse($env:FLOWERPAINTER_SUITE_LOCK_PID, [ref]$allow) }
$r = MI-DeployOnPurpose $sources $allow
if (-not $r.ok) {
    # the csproj turns this line into a build error (CustomErrorRegularExpression)
    Write-Host "FlowerPainter was NOT installed into $To`: $($r.message)"
    exit 1
}
Write-Host "FlowerPainter deployed (standalone, no Burst) to $To`: $($r.message) (the game queue puts it back after its own games)"
exit 0
