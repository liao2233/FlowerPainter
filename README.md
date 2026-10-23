# FlowerPainter 花卉

Independent Cities: Skylines II flower planting mod. No Placemaker DLL or enabled Placemaker mod is required.

Select an area, fill it with the existing flowing/island carpets or compose circles, petal/heart motifs and editable curves. Choose flowers per ribbon and fill connected gaps independently. Preview uses real temporary flower meshes without their cyan outlines; geometry remains temporary until Build.

Build `FlowerPainter.csproj` in Release with `Deploy=false` and `ManagedPath` pointing to the installed game's `Cities2_Data/Managed`. UI dependencies and the UI build use npm in `UI`. Deploy while the game is closed.

GPL-3.0. Extracted from the locally developed Placemaker project. Shared selection/build infrastructure derives from ParkingLotTool (KruemelGames), ParkManager (tomkpunkt) and Grid Road Generator (Ti4goc); their GPL notices and bundled Harmony MIT license are retained. FlowerPainter has separate namespaces, UI bindings, components, prefabs and `ModsData/FlowerPainter` storage.

## 使用

点击花朵图标或按 Ctrl+Shift+F 打开。先选定区域，再选择“直接铺满”或“曲线组合”。直接铺满保留流线拼色和花岛拼色。

曲线组合可画圆、摆花瓣或心形，也可添加自由曲线。图案可移动、缩放、旋转和单独调粗细；自由曲线支持拖动节点、增加节点、右键删除节点。点击“重新绘制曲线”后，在地图上逐点绘制，Enter 或右键结束。超出范围的部分不种植。

“曲线用花（独立设置）”提供独立花色盘，默认奶白色；各曲线和图案可单独指定花卉 prop，列表会显示用花。修改填充花色不会改掉曲线用花。新增两种原版花丛的 8 种花色（珊瑚红、橘橙、明黄、樱粉、玫紫、紫罗兰、矢车菊蓝、奶白），共 16 个独立 prop；保留原模型的绿叶、土壤和边框，花色用于真实模型预览和建造。

空白区域可以统一填充，也可以选择纯色、流线、花岛或留空，再点击地图中的单个区域。圆内与圆外可分别设置。使用具体花卉模型预览，修改后自动更新，预览不显示每个物件的蓝色描边。确认后点击“检查”，再建造。花卉作为装饰种植，不额外创建公园设施或公园吸引力。

## 验证与分发

74 项花卉核心测试、4 项界面测试通过。曲线与填充独立选花、两种花丛的全部 8 色通过真实游戏建造与截图检查：`send-curve-flower-colors-1024-023454`，3 个场景均建造完成且 0 步骤错误。真实游戏验证了两种铺满、圆与心形、曲线范围裁剪、分区选花及填充、从空白逐点绘制、节点增删、拖动、缩放、粗细调整和撤销重做。成功组合运行：`send-flower-composition-1023-220204`；每个场景均建造完成且 0 步骤错误。游戏日志和原图保存在原工作区的 `.forensics/suite/runs/` 中。

`tools/Package.ps1` 输出独立运行包和完整源码包到 `dist/`。开发时可共享已有 npm 依赖缓存；源码包和运行包都不包含对原 Placemaker 项目的运行时或项目引用。未发布至外部平台。

原 Placemaker 的新建花卉入口已隐藏，其旧花卉代码保留以读取和编辑原有项目；旧项目不会自动转换为 FlowerPainter 的分组。
