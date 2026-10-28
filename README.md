# FlowerPainter 花卉

Independent Cities: Skylines II flower planting mod. No Placemaker installation is required.

**Preview-release limitation:** generated flower meshes remain in the local game asset directory. Save-only transfer to another PC is not supported. Do not use this preview for publicly shared saves or maps that require portable flower assets.

Player instructions and the publication description: [docs/publish/MOD-PAGE.md](docs/publish/MOD-PAGE.md). Changes: [CHANGELOG.md](CHANGELOG.md).

## Build from source (Windows)

Install the .NET 9 SDK, Node.js 18 or newer, and an installed copy of Cities: Skylines II. The build finds Steam library installations automatically; game assemblies are not redistributed.

```powershell
dotnet build FlowerPainter.csproj -c Release -p:Deploy=false
dotnet test Tests/Placemaker.Tests/FlowerPainter.Tests.csproj
```

The first build runs `npm ci` in `UI` if dependencies are missing. Dependencies belong to this checkout; another Placemaker source tree is not needed. To install them explicitly, run `npm ci --prefix UI`.

If Steam discovery is unavailable, set `FLOWERPAINTER_GAME_DIR` to the installed game directory, or supply `-p:ManagedPath=".../Cities2_Data/Managed"`. Builds and tests do not start the game. Keep `Deploy=false` for development checks; install only while the game is closed.

## Release preparation

```powershell
powershell -NoProfile -File tools/Check-Release.ps1
powershell -NoProfile -File tools/Package.ps1
```

`Directory.Build.props` is the release version authority. `UI/package.json`, `UI/mod.json`, the lock file and `Properties/PublishConfiguration.xml` must match it; the release check rejects a mismatch. Publication `LongDescription` must match the player Markdown page. The package script builds and tests before producing `dist/FlowerPainter.zip` and `dist/FlowerPainter-source.zip` from the same committed revision. It rejects a dirty source tree and does not upload or deploy anything.

See [Tests/README.md](Tests/README.md) for the test scope and [docs/publish/RELEASE-STATUS.md](docs/publish/RELEASE-STATUS.md) for the remaining manual release checks. Real game publication images are in `Properties`; their original run and screenshot hashes are recorded in `Properties/media-sources.json`. UI mock screenshots are development fixtures and are not publication images.

## License and source

GPL-3.0. Distribute the corresponding source archive alongside the runtime archive and retain the licenses. The public source repository and first Paradox Mods ID have not yet been assigned; do not invent their URLs or IDs.

Extracted from Placemaker. Selection/build infrastructure derives from ParkingLotTool (KruemelGames), ParkManager (tomkpunkt), and Grid Road Generator (Ti4goc). Harmony is distributed under MIT; its license is included. Flag sources and artwork notices are in [Assets/Flags/NOTICE.md](Assets/Flags/NOTICE.md).
