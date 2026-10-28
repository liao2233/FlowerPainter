# 0.1.2 release status

The local release package is a preview candidate, not a claim that all native release gates have passed.

## Completed locally

- One version authority (`Directory.Build.props`) checked against UI, lock file and publishing metadata.
- Player page and changelog; the publish XML uses exactly the player page text.
- 1024 x 1024 thumbnail and eight actual native game screenshots, with source-run and original-file hashes. No mock image is submitted as a game screenshot.
- Steam-managed-assembly discovery and standalone UI dependencies installed with `npm ci`.
- Supported standalone test scope: 713 passing unit tests, zero failures, zero skips at preparation time. Packaging reruns the complete supported suite and FlowerPainter browser tests.
- Runtime no longer embeds the 271 legacy full-resolution albedo images. Legacy v6 flag surfaces can be rebuilt from the embedded compact color maps; current flower geometry is unchanged.
- Hidden seven inherited parking/park economy settings. Runtime packaging includes English and Simplified Chinese; other languages use English fallback.
- Runtime and corresponding full source archives are packaged from the same committed revision. Package metadata includes the commit and DLL hash.

## Still required before public upload

1. Supply the real public source repository or source-download URL, then add it to `ExternalLink`. There is no configured git remote or published repository. A local matching source ZIP is prepared; it must be made available to recipients.
2. The author manually starts a clean-profile game and verifies: area fill, independent partitions, curves, dense patches, direct flags, dragging/releasing/cancelling/undo, rotation, near/far display, build and local reload.
3. Verify with Placemaker enabled, and verify the documented result when disabling/uninstalling FlowerPainter and loading a backup save. Use a copy of the save.
4. Verify the new legacy-material reconstruction on a backup containing v6 albedo flags, if those old local development saves are still supported.
5. Decide whether a local-assets-only preview is acceptable. Save-only cross-PC portability has **not** been implemented or verified. The limitation appears at the top of the player page and runtime README; this is not a fix for portability.
6. First platform upload assigns `ModId`; record the real returned ID before subsequent uploads.

`tools/Check-Release.ps1 -ForUpload` refuses upload readiness without a real source URL and explicit `-NativeRegressionConfirmed`. This tool never uploads. Leave ModId blank only for the first upload.

## Evidence limits

Screenshots are genuine earlier native game runs of the flower geometry, not a new final-build certification. The earlier move/rotate scenario had a first-placement UI timing error; later build and local reload completed, but that run does not count as a clean release pass. Direct dragging has compile and browser UI evidence, not a completed native game regression. No game is started automatically during this preparation.

The much older README's 99% geometry reduction and 123-test statements describe a discontinued flat-canopy implementation and are archived only as historical notes in `docs/development-history.md`.
