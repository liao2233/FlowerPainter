param([string]$Configuration='Release',[switch]$SkipValidation)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
    $dirty=git status --porcelain --untracked-files=all
    if($LASTEXITCODE -ne 0){throw 'Cannot read git state'}
    if($dirty){throw 'Commit or explicitly exclude all workspace changes before packaging; source is archived from HEAD.'}
    & (Join-Path $PSScriptRoot 'Check-Release.ps1')
    $version=([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    $commit=(git rev-parse HEAD).Trim()
    dotnet build FlowerPainter.csproj -c $Configuration -p:Deploy=false -v:q
    if($LASTEXITCODE -ne 0){throw 'Release build failed'}
    if(-not $SkipValidation) {
        dotnet test Tests/Placemaker.Tests/FlowerPainter.Tests.csproj -c $Configuration --logger 'trx;LogFileName=package-tests.trx' --results-directory .forensics/package-tests -v:q
        if($LASTEXITCODE -ne 0){throw 'Release unit tests failed'}
        $previousShots=$env:PM_SHOTS_DIR
        try {
            $env:PM_SHOTS_DIR=Join-Path $root '.forensics/package-ui-shots'
            Push-Location (Join-Path $root 'UI')
            try {npm run test:ui -- flowers.spec.ts;if($LASTEXITCODE -ne 0){throw 'FlowerPainter UI tests failed'}} finally {Pop-Location}
        } finally {$env:PM_SHOTS_DIR=$previousShots}
    }
    $output=Join-Path $root 'dist'
    $release=Join-Path $output ('package-'+$version+'-'+$commit.Substring(0,8)+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    $stage=Join-Path $release 'FlowerPainter'
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    foreach($name in @('FlowerPainter.dll','0Harmony.dll','0Harmony-LICENSE.txt')) {
        Copy-Item -LiteralPath (Join-Path $root "bin/$Configuration/$name") -Destination $stage
    }
    $dll=Join-Path $stage 'FlowerPainter.dll'
    $actualVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion
    if(-not $actualVersion.StartsWith([string]$version) -or $actualVersion -like '*dirty*'){throw "Unexpected DLL version: $actualVersion"}
    if((Get-Item $dll).Length -gt 12MB){throw 'Unexpected DLL size: check for reintroduced full flag albedo resources'}
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'CHANGELOG.md') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $root 'docs/publish/MOD-PAGE.md') -Destination (Join-Path $stage 'README.md')
    Copy-Item -LiteralPath (Join-Path $root 'Recipes') -Destination $stage -Recurse
    $lang=Join-Path $stage 'Lang';New-Item -ItemType Directory -Path $lang | Out-Null
    Get-ChildItem (Join-Path $root 'Lang') -File | Where-Object {$_.Name -match '^(en-US|zh-HANS)(\.|\.json$)'} | Copy-Item -Destination $lang
    $notices=Join-Path $stage 'FlagSources';New-Item -ItemType Directory -Path $notices | Out-Null
    Get-ChildItem (Join-Path $root 'Assets/Flags') -File | Where-Object {$_.Extension -eq '.md' -or $_.Name -eq 'extra-sources.json'} | Copy-Item -Destination $notices
    $ui=Join-Path $root 'UI/build'
    Get-ChildItem $ui -File | Where-Object {$_.Name -match '^FlowerPainter\.(mjs|css)(\.LICENSE\.txt)?$'} | Copy-Item -Destination $stage
    Copy-Item -LiteralPath (Join-Path $ui 'images') -Destination $stage -Recurse
    [ordered]@{version=[string]$version;commit=$commit;dllSha256=(Get-FileHash $dll -Algorithm SHA256).Hash;validation=$(if($SkipValidation){'Reused previous validation; build and metadata checked'}else{'Unit and FlowerPainter browser tests passed'});savePortability='Local generated assets required; save-only transfer unsupported'} | ConvertTo-Json | Set-Content (Join-Path $stage 'release.json') -Encoding utf8
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    Compress-Archive -LiteralPath $stage -DestinationPath (Join-Path $output 'FlowerPainter.zip') -Force
    $sourceZip=Join-Path $output 'FlowerPainter-source.zip'
    git archive --format=zip --output $sourceZip HEAD
    if($LASTEXITCODE -ne 0){throw 'Could not archive the matching source'}
    $publish=Join-Path $release 'publish';New-Item -ItemType Directory -Force -Path $publish | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'Properties') -Destination $publish -Recurse
    Copy-Item -LiteralPath (Join-Path $root 'docs/publish/MOD-PAGE.md') -Destination $publish
    @('FlowerPainter.zip','FlowerPainter-source.zip') | ForEach-Object {$hash=Get-FileHash (Join-Path $output $_) -Algorithm SHA256;$hash.Hash.ToLowerInvariant()+'  '+$_} | Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
    Write-Host "Runtime: $(Join-Path $output 'FlowerPainter.zip')"
    Write-Host "Source: $sourceZip"
    Write-Host "Publication assets: $publish"
    Write-Host 'No upload or deployment was performed. See docs/publish/RELEASE-STATUS.md before publishing.'
} finally {Pop-Location}
