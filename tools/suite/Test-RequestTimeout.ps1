$ErrorActionPreference = 'Stop'
# Test the dispatcher deadline without starting a dispatcher, changing its files, or opening the game.
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Game-Dispatcher.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors -join "`n") }
$fn = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'D-Overdue' }, $true)
Invoke-Expression $fn.Extent.Text
function Q-Date($value) { [datetime]$value }
function Q-Rates { @{ overhead = 0; land = 0 } }
function Q-RowSec { 10 }
function Q-Duration($seconds) { "$seconds s" }
$now = [datetime]::UtcNow
$active = @{
    feed = @{ fedAt = $now.AddMinutes(-7); timeoutSec = 1200 }
    req = @{ timeoutMin = 20; rowPics = @(2); rowLand = @($true); rowType = @('floral'); land = 1 }
    rowsSeen = 0; rowAt = $now.AddMinutes(-7)
}
if ((D-Overdue $active $now) -ne '') { throw 'Explicit 20 minute budget was cut short by the six minute estimate' }
$active.req.timeoutMin = 0
if ((D-Overdue $active $now) -notlike '*made no progress*') { throw 'Default stall detection no longer works' }
$active.req.timeoutMin = 20
$active.feed.fedAt = $now.AddMinutes(-21)
if ((D-Overdue $active $now) -notlike 'no result after*') { throw 'Explicit request deadline no longer works' }
Write-Host 'PASS: explicit budget, default stall detection, and request deadline'
