# FlowerPainter

Independent flower planting mod extracted from Placemaker under GPL-3.0. Runtime and UI IDs, ECS namespaces, and data folders must remain separate.

Use the original tested selection/build infrastructure; expose only floral tools. Preserve attribution.

Build FlowerPainter.csproj with Deploy=false before testing. Use tools/suite queue for real game tests. Use the shared PLACEMAKER_SUITE_RUNS location from the parent checkout as FLOWERPAINTER_SUITE_RUNS so both projects use the same suite lock. Never close a user game. Commit completed changes.
