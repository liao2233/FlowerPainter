param()
$ErrorActionPreference = 'Stop'
$candidates = @()
foreach ($gamePath in @($env:FLOWERPAINTER_GAME_DIR, $env:CSII_GAMEPATH)) {
    if ($gamePath) { $candidates += Join-Path $gamePath 'Cities2_Data\Managed' }
}
$candidates += Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Cities2_Data\Managed'
$steamPath = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
$libraries = @($steamPath, (Join-Path ${env:ProgramFiles(x86)} 'Steam')) | Where-Object { $_ }
if ($steamPath) {
    $libraryFile = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $libraryFile) {
        $libraryText = Get-Content -LiteralPath $libraryFile -Raw
        $libraries += @([regex]::Matches($libraryText, '"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value.Replace('\\', '\') })
    }
}
foreach ($library in $libraries) { $candidates += Join-Path $library 'steamapps\common\Cities Skylines II\Cities2_Data\Managed' }
foreach ($candidate in $candidates | Select-Object -Unique) {
    if (Test-Path -LiteralPath (Join-Path $candidate 'Game.dll')) {
        [IO.Path]::GetFullPath($candidate)
        exit 0
    }
}
throw 'Game not found. Set FLOWERPAINTER_GAME_DIR or pass -p:ManagedPath=.../Cities2_Data/Managed.'
