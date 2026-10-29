# Release status

## 0.1.6 clipboard images (local candidate)

- The image design panel accepts the Paste image button and Ctrl+V outside text inputs. Clipboard PNG is preferred to retain transparency; native Windows bitmap and copied-image-file formats are also supported. Import runs on the existing worker thread and failed/empty clipboard reads preserve the previous image.
- Native run `20261029-130606-clipboard-native-set` (DLL `bb0e3289`) passed all 22 steps with no step errors. Actual UI button clicks verified PNG colour/aspect/transparency, rejecting text while preserving the design, screenshot bitmaps, and copied files. The following file-import, placement, drag/rotate/resize, build/edit, save/reload and cancel-edit flow also passed. The external clipboard fixture restored the clipboard after testing.
- The first implementation crashed in Mono's `System.Windows.Forms.XplatUIWin32.DIBtoImage` during native run `20261029-130215-clipboard-images`; it was not installed. Final code uses native Windows clipboard reads and has no Windows.Forms reference. `tools/suite/Clipboard-Fixture.ps1` runs outside Unity under Windows PowerShell -STA; set the first `pasteImage` step's `fixtureDir` to that helper's absolute Directory when running `suite/scenarios/clipboard-images.json`.
- Browser tests cover the button, Ctrl+V, normal text-field paste and leaving image mode; all 15 flower UI tests passed.

## 0.1.5 flag input fix (local candidate)

- Flags and imported images never fall through to polygon drawing, including before placement and after cancelling placement. Area mode/stage changes and polygon corner overlays are disabled for artwork. Whole-pattern placement, drag, rotation and size controls remain available.
- Native run `20261029-125007-flag-input-lock` passed both scenarios with no step errors: actual UI entry, arbitrary world clicks before placement, cancellation then clicking, fixed template ratio (1.5 in the fixture), blocked corner editing, image-mode clicks, flag dragging/rotation, build/save/reload, and normal subdivision drawing/building. The runtime behavior was tested with DLL `cfc301c1` before the metadata-only bump to 0.1.5.
- Two added stage-machine cases verify that artwork cannot enter or fall back to the area stage, both before and after placement. The existing 18 stage-machine tests also passed.

## 0.1.4 public preview

Published as a public preview on 2026-10-29 with the user's explicit approval to proceed with the documented remaining validation gaps. This is not a claim that all native release gates have passed.

## Published artifact

- Paradox Mods: https://mods.paradoxplaza.com/mods/165361/Windows (`ModId` 165361, version 0.1.4, Public).
- Uploaded the previously validated package from commit `a957fd45c902efecf38d0ecbb7635968e8e226cc`: 737 core tests and 14 UI tests passed. DLL SHA-256: `27BF37D082FD5FA39BF8BDFDCEA09820375FD35AEDCB5F6D35F6BC5AA0BBE2C0`.
- Official ModPublisher returned `Mod published with Id=165361` and exit code 0. The ID is recorded in `Properties/PublishConfiguration.xml`; subsequent file uploads must use `NewVersion` with a new version number, never `Publish`.
- On this machine, creating the publisher's temporary ZIP through the C: user-data junction failed. Temporarily setting the toolchain's user-level `CSII_PDXCACHEPATH` to the same cache's physical E: path resolved it; the original setting was restored after upload. The runtime package was unchanged.

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

## Flower workflow (local 0.1.4 update)

- Choose the planting method first. Images and flags go directly through design, cursor placement, adjustment and planting; area-based flowers keep area drawing and design. Completion requires an explicit repeat action.
- Clicking existing flowers opens editing. Cancel leaves the original build intact; copying has its own action. Mode drafts and unapplied region choices last for the current game session only.
- 726 core tests and 12 browser UI tests pass. Release builds have zero errors and four inherited warnings. Native flag-direct and circle-subdivisions scenarios in `20261029-104852-flower-workflow` completed with zero step errors.
- Fixed a native failure where a missing cached LOD permanently blocked later regeneration of the same patch. Missing-cache restoration now leaves the model eligible for regeneration, and validates both geometry levels before registering render prefabs.
- Native run `20261029-110833-image-workflow` (DLL `32259f6c`): all 21 steps completed with zero step errors, including mode/draft switching, import, placement, repeated Enter, click-to-edit, resize/rotate/drag, rebuilding, normal save/reload and cancel-edit preservation. Real Chinese UI screenshots were inspected. Cache-eviction evidence remains the earlier dedicated runs: repeating that injection against pre-existing models was correctly refused. The workflow scenario now tests normal reload and explicitly resets its drawing stage for repeated runs.

## Completed locally

### Current publication media and missing archives (local 0.1.4 update)

- Three current native screenshots replace old workflow shots: method entry with a built imported illustration, actual cursor-following image placement, and partition design with unapplied choices retained. Five earlier effect screenshots and the existing cover remain. `media-sources.json` records exact native originals and hashes; only JPEG encoding is applied.
- Missing-archive detection checks unresolved flower IDs before prefab resolution and resolved flower instances afterwards. It warns only when flower references remain; it cannot recover references or model data already removed by the game. A successful new capture from intact local cache clears the missing-archive warning.
- Native run `20261029-120830-flower-publication` passed both scenarios (18 steps). A suite save deliberately omitted this mod's archive, then reloaded with all 9 flower instances and the expected missing-archive warning. A subsequent full save/reload preserved the same instances and cleared the warning. This is controlled missing-archive injection, not a completed disable-mod/overwrite/re-enable test.
- Images were reframed in `20261029-121151-flower-publication`. Replaying the partition setup exposed a fixture reusing a previous site's method draft; the suite's fresh-type setup now preserves the newly drawn frame and resets its settings after method selection. Product draft behavior is unchanged.
- The corrected partition fixture passed all eight steps in `20261029-121614-partition-publication`; its final screenshot shows the complete design frame and retained unapplied choices.
- Diagnostics adds raw MiB; the player page uses an explicitly scoped 701,038-byte fixture example, not a per-block estimate. It warns against overwriting the only save while the mod is disabled, and does not promise all upgrade/downgrade paths are lossless.

### Support and release workflow (local 0.1.4 update)

- Archive warnings use the game's native notification system even when the flower panel is closed; clicking opens the panel. Copy diagnostics uses the native clipboard and includes actual game/mod versions, archive state, capture timing and loaded assemblies matching Placemaker. This detection is diagnostic information, not a coexistence guarantee.
- Native run `20261029-115234-flower-support` (DLL `2190c7a2`) passed all eight steps. A synthetic warning was registered with the panel closed; the real Copy diagnostics button populated the clipboard, and save/reload retained all 13 flower instances. Actual game version reported `1.6.2f1`. The fixture archive was 718,372 raw bytes; capture took 95.7 ms in this one run (not a large-city benchmark).
- UI screenshots are no longer tracked; existing files remain on disk. Default test output is `.forensics/ui-shots`, with `PM_SHOTS_DIR` still supported. Publish screenshots under Properties are unchanged.
- Player documentation leads with image planting and explains independent installation, verified game version and reporting steps. Parameterized save generation, broad inherited-code/language deletion, and a final v0.1.4 tag are deferred; existing release gates still apply.

### Archive failure recovery (local 0.1.4 update)

- Archive capture checks each model's size before reading its files, continues after missing-file/metadata failures, and keeps fitting models within the 512 MiB budget. Encoding no longer collects every model plus a final full-array copy. Successful serialization releases the temporary buffer.
- Restore failures retain the affected original model data for subsequent saves. Unsupported or malformed archive payloads are preserved byte-for-byte; new models cannot be merged into an unreadable payload. Failed game-private-field resolution retains all archived models. Recovery buffers are intentionally retained while needed, and are cleared when another city is loaded.
- The flower panel shows persistent English/Chinese warnings. EXIF orientation ignores properties shorter than two bytes. Invalid outer save lengths, truncation in the game's reader and process-wide out-of-memory/disk-full failures are not claimed recoverable.
- 13 archive unit tests pass, covering missing exports, restoration write failures, unsupported/truncated payloads, changed reflection fields, bounded partial capture, recovery precedence and city isolation.
- Native run `20261029-113642-flower-archive-recovery` (DLL `97367228`) completed all 10 steps with zero step errors. Evicted 12 new models; all 13 instances returned. A second save/reload also retained all 13 instances, and both saves released their temporary buffers. Each archive contained 701,038 uncompressed bytes; this is not the compressed save-file increase or a large-city benchmark.

- One version authority (`Directory.Build.props`) checked against UI, lock file and publishing metadata.
- Player page and changelog; the publish XML uses exactly the player page text.
- 1024 x 1024 thumbnail and eight actual native game screenshots, with source-run and original-file hashes. No mock image is submitted as a game screenshot.
- Steam-managed-assembly discovery and standalone UI dependencies installed with `npm ci`.
- Supported standalone test scope: 715 passing unit tests, zero failures, zero skips at preparation time. Packaging reruns the complete supported suite and FlowerPainter browser tests.
- Runtime no longer embeds the 271 legacy full-resolution albedo images. Legacy v6 flag surfaces can be rebuilt from the embedded compact color maps; current flower geometry is unchanged.
- Hidden seven inherited parking/park economy settings. Runtime packaging includes English and Simplified Chinese; other languages use English fallback.
- Runtime and corresponding full source archives are packaged from the same committed revision. Package metadata includes the commit and DLL hash.

## Remaining validation and release follow-up

1. The public source repository is https://github.com/liao2233/FlowerPainter; keep it synchronized with the corresponding released version.
2. Complete the remaining native checks, particularly drag cancellation/undo and a real second-PC import. The cache-removal suite covers direct flags, regions, curves and reload.
3. The 0.1.3 direct-flag build and local reload passed with Placemaker loaded; verify the documented result when disabling/uninstalling FlowerPainter and loading a backup save. Use a copy of the save.
4. Verify the new legacy-material reconstruction on a backup containing v6 albedo flags, if those old local development saves are still supported.
5. A new save carries the flower meshes it uses. On 2026-10-29 the native suite removed newly created model caches and restored flag, region and curve builds on reload; a pre-0.1.3 save was loaded and resaved locally. A second physical computer has not been tested. Old saves must be opened and resaved on the original machine before transfer; models larger than the 512 MB archive limit cannot be guaranteed portable.
6. The first platform upload assigned `ModId` 165361. Keep that ID for every subsequent update and increment the version for new runtime files.

`tools/Check-Release.ps1 -ForUpload` refuses upload readiness without a real source URL and explicit `-NativeRegressionConfirmed`. This tool never uploads. Leave ModId blank only for the first upload.

## Evidence limits

Screenshots are genuine earlier native game runs of the flower geometry, not a new final-build certification. The earlier move/rotate scenario had a first-placement UI timing error; later build and local reload completed, but that run does not count as a clean release pass. On 2026-10-29 the cache-removal suite restored all referenced flag, region and curve patches after evicting respectively 17, 11 and 12 newly created assets. A pre-0.1.3 save loaded and was resaved with 13 distinct models; a 0.1.3 flag build and reload passed with Placemaker loaded. Several flag screenshot settling checks timed out. The cache-removal suite tested portable restoration for the affected assets; it does not replace a complete clean-profile regression or a real second-machine test.

The much older README's 99% geometry reduction and 123-test statements describe a discontinued flat-canopy implementation and are archived only as historical notes in `docs/development-history.md`.
