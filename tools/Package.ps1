param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $root 'dist'
$stage = Join-Path (Join-Path $output ('package-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))) 'FlowerPainter'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($source in @((Join-Path $root "bin\$Configuration"),(Join-Path $root 'UI\build'))) {
    foreach ($item in Get-ChildItem -LiteralPath $source -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $stage -Recurse -Force
    }
}
Copy-Item -LiteralPath (Join-Path $root 'README.md'),(Join-Path $root 'LICENSE') -Destination $stage -Force
Compress-Archive -LiteralPath $stage -DestinationPath (Join-Path $output 'FlowerPainter.zip') -Force
$sourceZip = Join-Path $output 'FlowerPainter-source.zip'
git -C $root archive --format=zip --output $sourceZip HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not archive the standalone source' }
Write-Host "Runtime: $(Join-Path $output 'FlowerPainter.zip')"
Write-Host "Source: $(Join-Path $output 'FlowerPainter-source.zip')"
