param([switch]$ForUpload,[switch]$NativeRegressionConfirmed)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version=([xml](Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$version=([string]$version).Trim()
if(-not $version){throw 'Missing release version'}
foreach($relative in @('UI/package.json','UI/mod.json','UI/package-lock.json')) {
    # Windows PowerShell 5 cannot turn npm's empty root-package key into a PSObject property.
    $json=Get-Content -LiteralPath (Join-Path $repo $relative) -Raw
    if($relative -like '*lock*'){$json=$json -replace '"":\s*\{','"__root__": {'}
    $metadata=$json | ConvertFrom-Json
    if($metadata.version -ne $version){throw "Version mismatch in $relative"}
    if($relative -like '*lock*' -and $metadata.packages.__root__.version -ne $version){throw 'Lockfile root version mismatch'}
}
$config=[xml](Get-Content -LiteralPath (Join-Path $repo 'Properties/PublishConfiguration.xml') -Raw -Encoding UTF8)
if($config.Publish.ModVersion.Value -ne $version){throw 'Publish version mismatch'}
$page=(Get-Content -LiteralPath (Join-Path $repo 'docs/publish/MOD-PAGE.md') -Raw -Encoding UTF8).Trim()
if(([string]$config.Publish.LongDescription).Trim() -ne $page){throw 'Publish LongDescription differs from MOD-PAGE.md'}
Add-Type -AssemblyName System.Drawing
$thumbnail=Join-Path $repo $config.Publish.Thumbnail.Value
$cover=[Drawing.Image]::FromFile($thumbnail)
try {if($cover.Width -ne 1024 -or $cover.Height -ne 1024){throw 'Thumbnail must be 1024 x 1024'}} finally {$cover.Dispose()}
$screens=@($config.Publish.Screenshot)
if($screens.Count -lt 1 -or $screens.Count -gt 10){throw 'Publish screenshot count must be 1-10'}
$media=@(Get-Content -LiteralPath (Join-Path $repo 'Properties/media-sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json)
foreach($screen in $screens) {
    if(-not(Test-Path -LiteralPath (Join-Path $repo $screen.Value))){throw "Missing screenshot: $($screen.Value)"}
    if(-not($media | Where-Object {$_.file -eq $screen.Value -and $_.kind -eq 'native-game-screenshot' -and $_.sourceSha256 -match '^[A-Fa-f0-9]{64}$'})){throw "Screenshot has no native-game provenance: $($screen.Value)"}
}
if(Test-Path -LiteralPath (Join-Path $repo 'UI/node_modules')) {
    if((Get-Item -LiteralPath (Join-Path $repo 'UI/node_modules') -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'UI/node_modules must not link to another checkout'}
}
if($ForUpload) {
    $sourceLink=@($config.Publish.ExternalLink | Where-Object {$_.Url -match '^https://[^\s]+$'})
    if($sourceLink.Count -eq 0){throw 'Upload blocked: provide a real public source repository/download URL in PublishConfiguration.xml'}
    if(-not $NativeRegressionConfirmed){throw 'Upload blocked: final clean-profile, reload, uninstall and Placemaker coexistence checks have not been confirmed'}
}
Write-Host "Release metadata OK: $version, $($screens.Count) native game screenshots, 1024 x 1024 thumbnail."
if(-not $ForUpload){Write-Host 'Local packaging metadata is ready; see RELEASE-STATUS.md for remaining upload checks.'}
