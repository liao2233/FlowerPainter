# FlowerPainter release tests

Run from the repository root:

```powershell
dotnet test Tests/Placemaker.Tests/FlowerPainter.Tests.csproj
cd UI
npm ci
npm run test:ui -- flowers.spec.ts
```

The `Placemaker.Tests` directory name is retained for source history. Its project now explicitly compiles FlowerPainter floral tests and shared geometry, tool, build, asset-curation and selection tests. It does not register the removed Placemaker generators to make unrelated tests pass, and `TestKit.Gen` still fails for an unexpectedly missing generator.

Inherited parking, park, commercial, road-generator and other removed-feature test files remain in the source archive but are outside this standalone release gate. The handful of mixed-file assertions for those generators are marked `PLACEMAKER_LEGACY_GENERATORS`; this symbol is not enabled. They require the parent application's generator registry and are not claimed as FlowerPainter coverage.

Two outdated shared fixtures were corrected: a build collision test now supplies the selected lot asset required by PlanWork, and localization checks merge all English files as the runtime does. Recipe checks also cover synthesized registered recipes when the shipped recipe directory has no project JSON files.

Native game scenarios and screenshots are separate evidence. Unit and browser tests cannot establish save portability, native rendering, uninstall behavior or compatibility with another mod. The final clean-profile game regression remains a release prerequisite; the author starts the game manually.
