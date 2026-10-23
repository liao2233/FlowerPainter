# FlowerPainter 花卉

Independent Cities: Skylines II flower planting mod. No Placemaker DLL or enabled Placemaker mod is required.

Select an area, fill it with the existing flowing/island carpets or compose circles, petal/heart motifs and editable curves. Choose flowers per ribbon and fill connected gaps independently. Preview uses real temporary flower meshes without their cyan outlines; geometry remains temporary until Build.

Build `FlowerPainter.csproj` in Release with `Deploy=false` and `ManagedPath` pointing to the installed game's `Cities2_Data/Managed`. UI dependencies and the UI build use npm in `UI`. Deploy while the game is closed.

GPL-3.0. Extracted from the locally developed Placemaker project. Shared selection/build infrastructure derives from ParkingLotTool (KruemelGames), ParkManager (tomkpunkt) and Grid Road Generator (Ti4goc); their GPL notices and bundled Harmony MIT license are retained. FlowerPainter has separate namespaces, UI bindings, components, prefabs and `ModsData/FlowerPainter` storage.
