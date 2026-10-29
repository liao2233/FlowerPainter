# 0.1.4 release status

The local release package is a preview candidate, not a claim that all native release gates have passed.

## Image import (0.1.4)

- 723 engine-free tests and 9 flower UI tests pass. The additional tests cover image colour/transparency, source aspect, transform sampling and recipe round-trip.
- PNG, JPEG, BMP, GIF, TIFF and ICO decoded successfully in the actual Unity game runtime (in addition to the Windows .NET decoder check).
- Native run `20261029-080903-image-flowers`: import, direct placement, dragging, rotation, resizing an existing build, source-file removal and cache-eviction save/reload completed with zero step errors. Nine new distinct models were evicted; all 10 flower instances returned. Two screenshot settling checks timed out; the reloaded view was visually inspected.
- Native run `queue-1029-082311`: clicked the real import button, opened the Windows file picker, selected `fixture.png`, and verified the filename, dimensions and flower preview after returning to the game. The earlier managed `OPENFILENAME` marshalling crashed Mono; the final picker uses a blittable structure and explicit native buffers.
- Fixed the imported filename/dimensions wrapping into separate lines in Cohtml. No change to the already-tested flower planning or archive code was needed for the picker fix.

## Expanded image colours (local 0.1.4 update)

- 251 image flower colours (legacy 12 plus 216 RGB cube samples and 23 neutral greys); existing colour indices remain stable. Image-only material v7 has neutral centres and less darkening; legacy geometry keeps its original material.
- 724 core tests pass, including neutral-ramp error <= 6/255, all palette indices round-tripping, and RGB-grid squared error below 20% of the old palette. An earlier run had three intermittent inherited BuildDeclutter failures; the unchanged full rerun passed. UI code is unchanged from the 9-test UI pass above.
- Native run `20261029-094733-image-flowers`, DLL `51836f246f4e` build: six decoder formats, grayscale/warm ramp, transforms, build/edit/rebuild and cache-eviction reload completed with zero step errors. Nine models were evicted and all 10 instances returned. Reload screenshot inspected; discrete colour bands and flower gaps remain visible. The first attempt rejected an unsupported test-script parameter; it was removed before this successful run.
- Reimport the original image for new colours: old indexed recipes cannot recover colours discarded by the 12-colour import.

## Completed locally

- One version authority (`Directory.Build.props`) checked against UI, lock file and publishing metadata.
- Player page and changelog; the publish XML uses exactly the player page text.
- 1024 x 1024 thumbnail and eight actual native game screenshots, with source-run and original-file hashes. No mock image is submitted as a game screenshot.
- Steam-managed-assembly discovery and standalone UI dependencies installed with `npm ci`.
- Supported standalone test scope: 715 passing unit tests, zero failures, zero skips at preparation time. Packaging reruns the complete supported suite and FlowerPainter browser tests.
- Runtime no longer embeds the 271 legacy full-resolution albedo images. Legacy v6 flag surfaces can be rebuilt from the embedded compact color maps; current flower geometry is unchanged.
- Hidden seven inherited parking/park economy settings. Runtime packaging includes English and Simplified Chinese; other languages use English fallback.
- Runtime and corresponding full source archives are packaged from the same committed revision. Package metadata includes the commit and DLL hash.

## Still required before public upload

1. The public source repository is https://github.com/liao2233/FlowerPainter; keep it synchronized with the corresponding released version.
2. Complete the remaining native checks, particularly drag cancellation/undo and a real second-PC import. The cache-removal suite covers direct flags, regions, curves and reload.
3. The 0.1.3 direct-flag build and local reload passed with Placemaker loaded; verify the documented result when disabling/uninstalling FlowerPainter and loading a backup save. Use a copy of the save.
4. Verify the new legacy-material reconstruction on a backup containing v6 albedo flags, if those old local development saves are still supported.
5. A new save carries the flower meshes it uses. On 2026-10-29 the native suite removed newly created model caches and restored flag, region and curve builds on reload; a pre-0.1.3 save was loaded and resaved locally. A second physical computer has not been tested. Old saves must be opened and resaved on the original machine before transfer; models larger than the 512 MB archive limit cannot be guaranteed portable.
6. First platform upload assigns `ModId`; record the real returned ID before subsequent uploads.

`tools/Check-Release.ps1 -ForUpload` refuses upload readiness without a real source URL and explicit `-NativeRegressionConfirmed`. This tool never uploads. Leave ModId blank only for the first upload.

## Evidence limits

Screenshots are genuine earlier native game runs of the flower geometry, not a new final-build certification. The earlier move/rotate scenario had a first-placement UI timing error; later build and local reload completed, but that run does not count as a clean release pass. On 2026-10-29 the cache-removal suite restored all referenced flag, region and curve patches after evicting respectively 17, 11 and 12 newly created assets. A pre-0.1.3 save loaded and was resaved with 13 distinct models; a 0.1.3 flag build and reload passed with Placemaker loaded. Several flag screenshot settling checks timed out. The cache-removal suite tested portable restoration for the affected assets; it does not replace a complete clean-profile regression or a real second-machine test.

The much older README's 99% geometry reduction and 123-test statements describe a discontinued flat-canopy implementation and are archived only as historical notes in `docs/development-history.md`.
