# FlowerPainter 花卉

Independent Cities: Skylines II flower planting mod. No Placemaker DLL or enabled Placemaker mod is required.

Select an area and fill it with flowing/island carpets, subdivide it using circles and editable curves, or compose planted ribbons. Subdivision boundaries plant no flowers: choose and fill each enclosed region independently, then edit the boundaries to repartition. Ribbon mode retains independent curve flowers and gap fills. Preview uses real temporary flower meshes without their cyan outlines; geometry remains temporary until Build.

Build `FlowerPainter.csproj` in Release with `Deploy=false` and `ManagedPath` pointing to the installed game's `Cities2_Data/Managed`. UI dependencies and the UI build use npm in `UI`. Deploy while the game is closed.

GPL-3.0. Extracted from the locally developed Placemaker project. Shared selection/build infrastructure derives from ParkingLotTool (KruemelGames), ParkManager (tomkpunkt) and Grid Road Generator (Ti4goc); their GPL notices and bundled Harmony MIT license are retained. FlowerPainter has separate namespaces, UI bindings, components, prefabs and `ModsData/FlowerPainter` storage.

## 使用

点击花朵图标或按 Ctrl+Shift+F 打开。先选定区域，再选择“直接铺满”、“内部再分区”或“曲线组合”。直接铺满保留流线拼色和花岛拼色。

内部再分区：在外框中添加圆、心形、花瓣或自由曲线作为零宽分界线。圆的内外、嵌套区域以及交叉曲线形成的区域可分别选中；悬空且未闭合的线不会产生新区域。自由曲线可重新绘制、拖节点或点击“闭合曲线”。界面按三个步骤组织：先绘制分界线，点击“完成划线 → 选择分区”，再从地图或分区列表选中一块区域。选中只高亮，不会种花；随后设置这块区域自己的 prop 和填充样式，点击“填充此分区”才生效。纯色可选一个 prop，流线和花岛可选多个 prop；再次选中区域会恢复它的配置，修改不会影响其他分区。点击“返回修改分界线”可继续划线。未填区域保持空白，分界线本身不生成花带。移动或重绘边界会重新切分，已有填充按原点击位置重新归属。

曲线组合可画圆、摆花瓣或心形，也可添加自由曲线。图案可移动、缩放、旋转和单独调粗细；自由曲线支持拖动节点、增加节点、右键删除节点。点击“重新绘制曲线”后，在地图上逐点绘制，Enter 或右键结束。超出范围的部分不种植。

“曲线用花（独立设置）”提供独立花色盘，默认奶白色；各曲线和图案可单独指定花卉 prop，列表会显示用花。修改填充花色不会改掉曲线用花。新增两种原版花丛的 8 种花色（珊瑚红、橘橙、明黄、樱粉、玫紫、紫罗兰、矢车菊蓝、奶白），共 16 个独立 prop；保留原模型的绿叶、土壤和边框，花色用于真实模型预览和建造。

新增“连片花丛”：在直接铺满或分区填充中切换“花的模型 → 连片花丛”，沿分区轮廓裁剪，内部使用共享花色材质和合并网格。每块约含 196 朵花；花头间距和高度分别可调，不受原有 prop 重叠度影响。普通分区仍先划线，再选区设置各自的花色和填充样式。当前花色取自 FlowerPainter 彩色花 prop 名称，使用新花丛模型。

“旗帜”是独立入口，首批原型包含中国、日本、法国，用来检验五星、圆和条纹。它们保持 3:2 比例，使用相同的花丛生成方式。完整旗帜库尚未加入。自定义模型和注册表保存在游戏用户目录 `ImportedData/FlowerPainter/Patches`，材质贴图由游戏资产库保存；转移或分享此版本的存档时，需要同时保留这些生成资产。

“Prop 重叠度”直接控制花丛的堆叠密度，范围 0–90%，默认 0%。实际中心距离为基础间距 ×（1 − 重叠度）；50% 把距离减半，数量约为 4 倍。花卉尺寸单独设置，不再随着间距或选区大小自动变化。花毯的 3200 个、旧图案的 1800 个数量限制已取消，预览也完整显示所有 prop，按帧分批生成。

空白区域可以统一填充，也可以选择纯色、流线、花岛或留空，再点击地图中的单个区域。圆内与圆外可分别设置。使用具体花卉模型预览，修改后自动更新，预览不显示每个物件的蓝色描边。确认后点击“检查”，再建造。花卉作为装饰种植，不额外创建公园设施或公园吸引力。

## 验证与分发

87 项花卉核心测试、7 项界面测试通过。新增精确分区覆盖圆内外、嵌套、交叉、多条开曲线闭合、逐块填花、留空与重绘。分步填充实测 `send-flower-partitions-1026-152149`：圆内外、交叉曲线多分区、重绘边界 3 个场景均建造成功、0 步骤错误；通过真实界面与地图点击验证先选区后设置、选择不种花、应用只修改当前分区、多 prop 流线填充及重新选中修改。截图确认重叠度默认 0%、划线和填充界面分开。密度实测 `send-flower-density-1026-015045`：同一地块从 0% 重叠的 1185 个增加至 60% 的 7387 个，预览和建造均完整保留全部物件；铺满、重叠铺满及独立曲线填充 3 个场景均建造成功、0 步骤错误。曲线与填充独立选花、两种花丛的全部 8 色通过真实游戏建造与截图检查：`send-curve-flower-colors-1024-023454`，3 个场景均建造完成且 0 步骤错误。真实游戏验证了两种铺满、圆与心形、曲线范围裁剪、分区选花及填充、从空白逐点绘制、节点增删、拖动、缩放、粗细调整和撤销重做。成功组合运行：`send-flower-composition-1023-220204`；每个场景均建造完成且 0 步骤错误。游戏日志和原图保存在原工作区的 `.forensics/suite/runs/` 中。

`tools/Package.ps1` 输出独立运行包和完整源码包到 `dist/`。开发时可共享已有 npm 依赖缓存；源码包和运行包都不包含对原 Placemaker 项目的运行时或项目引用。未发布至外部平台。

原 Placemaker 的新建花卉入口已隐藏，其旧花卉代码保留以读取和编辑原有项目；旧项目不会自动转换为 FlowerPainter 的分组。
