> 历史讨论方案，非当前实现。当前版本使用立体花头拼图；旧旗帜材质兼容路径使用紧凑花色数据，不再嵌入整张大贴图。

# FlowerPainter 国旗贴地图面 — 修复与实现方案（给实现者）

> 状态：上一版 PoC 已合入源码并部署，但进游戏 Preview/Build 时崩溃。本方案说明**为何崩**、**怎么修**、以及正确的目标架构。不要在「更密 prop」或「12 色 canopy 花纹」上继续堆。

## 0. 运行时错误（必须先修）

```
NullReferenceException
Error when initializing batch 1 for FlowerPainter Flag … mesh
  at Game.Rendering.ManagedBatchSystem+MaterialKey.Initialize(SurfaceAsset surface)
  at Game.Rendering.ManagedBatchSystem.CreateBatch(...)
With object FlowerPainter Flag … mesh (Game.Prefabs.RenderPrefab)
```

含义：`RenderPrefab.surfaceAssets[0]` 指向的 `SurfaceAsset` 在批处理初始化时，**材质键所需字段为空**（常见是 surface 未 LoadProperties、贴图槽为 null、或 Save 后纹理未落盘/不可读导致导入结果为空）。不是 mesh 顶点本身的问题。

### 0.1 嫌疑点（按优先级排查）

当前实现：`Game/Assets/FlowerPatchPrefabSystem.cs` → `FlagSurface(string flagId)`。

| # | 嫌疑 | 说明 |
|---|---|---|
| 1 | `ImageConversion.LoadImage(albedo, png, true)` 第三参 `markNonReadable=true` | Canopy 的 `MakeSurface` 用 `SetPixels`+`Apply`，纹理可读。Flag 路径用 LoadImage 且 markNonReadable，随后 `asset.SetData`→`Save` 可能写不出有效 `_BaseColorMap`，批处理读到 null → NRE。**先改成 `markNonReadable=false`，Save 后再按需压缩。** |
| 2 | 新建 Surface 后未 `LoadProperties` | 缓存命中分支有 `cached.LoadProperties(false)`；**新建分支 Save 后直接 return，没有 LoadProperties**。Canopy 能活，往往因为启动时早就建好且后续某路径加载了。Flag 是首次创建立即挂到 RenderPrefab → 更容易踩空。**Save 后立刻 `asset.LoadProperties(false)`，再挂到 `render.surfaceAssets`。** |
| 3 | `asset.textures.Values` 路径 / 目录 | Save 前 `Directory.CreateDirectory(Path.GetDirectoryName(texture.path))`；若某 texture.path 为 null 会静默坏掉。Save 后断言每个关键槽（至少 `_BaseColorMap`）非 null。 |
| 4 | 错误复用坏缓存 | `AssetDatabase.global.GetAsset<SurfaceAsset>(Id(name))` 若上次失败留下半残 asset，会一直 NRE。修复后改 material version / 名字（如 `v7`），或删除 `ImportedData/FlowerPainter/Patches` 下坏掉的 Flag Albedo。 |
| 5 | Shader 槽清空过狠 | `foreach ... SetTexture(property,null)` 后只填了 Base/Control/Mask/Normal/Emissive。若 `GetMaterial(0)` 模板还要求其它 map，MaterialKey 可能 NRE。对照**已能工作的** `MakeSurface(canopy:true)`：槽位集合必须一致；缺则补纯色占位。 |

### 0.2 最小修复步骤（先让中/日/法不崩）

1. 只改 `FlagSurface`，对齐 `MakeSurface` 的成功路径：
   - 贴图保持 **CPU 可读** 直到 `Save` 完成。
   - `Save` 后调用 `LoadProperties(false)`。
   - 与 canopy 相同的纹理槽 + float 参数；不要多开奇怪 keyword。
2.  bump 名称版本：`FlowerPainter Flag Albedo {id} v7`（避免脏缓存）。
3. `FlagMaterialVersion` 可升到 7，或清用户机上 `ImportedData/FlowerPainter/Patches` 里旧 flag 相关文件 + manifest。
4. 编译：`dotnet build FlowerPainter.csproj -c Release -p:Deploy=false -p:ManagedPath=<游戏 Managed>`，再 Deploy 到 Mods。
5. 进游戏：shape=flag，china/japan/france，框选 Preview。**不应再出现 MaterialKey.Initialize NRE**。
6. 加防御日志：`FlagSurface` 返回前检查 `surface != null`、关键 texture 非 null；失败抛明确异常（含 flagId），不要让 NRE 出现在引擎批处理里。

### 0.3 验收（崩溃修复）

- [ ] Preview / Build 中国旗不再抛 `MaterialKey.Initialize` NRE
- [ ] Player.log 无同类 Error
- [ ] 非 flag 的 canopy patch 仍正常（回归）

---

## 1. 目标架构（崩溃修完后保持/完善）

**目标观感**：贴地连续旗面（清晰色块/纹章），不是 12 色花纹马赛克；实体数 ≈ 1～数个 StaticObject / 旗块，不是几千 prop。

### 1.1 数据流

```
SVG (Assets/Flags/svg)
  → tools/flags/build.cjs
      → maps/{id}.bin          (保留：规划/裁剪/统计仍可用色索引)
      → textures/{id}.png      (新增：显示用 RGBA albedo，512–1024 宽)
  → 嵌入 DLL：FlowerPainter.Flags.textures.{id}.png

shape=flag
  → FloralLayer → FlowerPatchPlanner
      → FlowerPatchMesh.MakeFlag(...)   // UV = 连续旗面 0–1；只按地形/边界细分
      → Facts["floral.flagAlbedo"]=1
  → FlowerPatchPrefabSystem
      → GeometryAsset (mesh)
      → FlagSurface(flagId)：SurfaceAsset._BaseColorMap = 该旗 PNG
      → RenderPrefab.surfaceAssets = [that surface]
      → StaticObjectPrefab
```

### 1.2 已存在的关键文件（实现者应读后再改）

| 文件 | 职责 |
|---|---|
| `Core/Layers/Types/Floral/FloralLayer.cs` | `shape=="flag"` 强制 patch 路径 |
| `Core/Layers/Types/Floral/FlowerPatchPlanner.cs` | Fit / flagRegion / 调 `MakeFlag` |
| `Core/Layers/Types/Floral/FlowerPatchMesh.cs` | `MakeFlag`、`FlagPrefix`、`FlagMaterialVersion`、`FlagId` |
| `Core/Layers/Types/Floral/FlagCatalog.cs` | `Resource("textures."+id+".png")`、catalog、maps |
| `Game/Assets/FlowerPatchPrefabSystem.cs` | `ResolveSurface` / `FlagSurface` / `Register` — **崩溃点在此** |
| `tools/flags/build.cjs` | 导出 textures PNG |
| `Directory.Build.targets` | 嵌入 `Assets/Flags/textures/*.png` |
| `Tests/Placemaker.Tests/FloralPatchTests.cs` | flag albedo 断言 |

非 flag 必须继续走 canopy atlas（MaterialVersion 5），互不影响。

### 1.3 刻意不要做的

- 不要用更密 flower prop 摆国旗。
- 不要把显示层绑回 12 色量化采样。
- 不要上大规模 GPU instancing 万朵花。
- Decal 方案仅作平坦地形备选；本方案主路径是贴地 mesh + albedo。

---

## 2. 视觉与资产约定

- Albedo：主体颜色准确；允许极轻度亮度噪声保留「花坛感」，但纹章/星/十字边缘要可读。
- UV：平面映射到 Fit 后的旗矩形；`flagRegion` 外不生成三角（或 UV 在外丢弃）。
- 透明/异形（尼泊尔等）：PNG alpha；材质需 alpha clip 或等价。PoC 可先保证中/日/法矩形旗；异形单独立项。
- 旧存档：用 canopy 画过的 flag **不会自动变**；需用户用新版本重建。文档里写一句即可。

---

## 3. 建议实现顺序

1. **修崩**（§0）— 阻塞一切。
2. 手动删坏缓存 / bump v7，验证中日法三面。
3. 对比截图：旧 canopy vs 新 albedo（同尺寸选区）。
4. （可选）稀疏 3D 花混合层：间距 1.5–3 m，只装饰，不承担图案。
5. （可选）异形旗 alpha clip。
6. （可选）缩小嵌入：若 DLL 过大，只嵌入常用旗或外置到 Mods 旁 textures 目录按需加载。

---

## 4. 测试清单

- 单元/现有：`FloralPatch` 相关测试全绿。
- 游戏内：china / japan / france，Preview + Build，存档重开仍在。
- 回归：普通 floral patch（非 flag）外观与性能无回归。
- 日志：创建 flag surface 成功一行 Info（flagId、texture 尺寸）；失败要有旗 id。

---

## 5. 给实现者的「完成定义」

1. 游戏内选 flag 不再 NRE。
2. 旗面观感明显优于 12 色 canopy 花纹。
3. 非 flag patch 未坏。
4. 本文件 §0.3 / §4 勾选完成；Player.log 无 MaterialKey 错误。

---

## 6. 上一版 PoC 已知缺口（修崩时一并注意）

- `FlagSurface` 与 `MakeSurface` 成功路径不一致（可读性、LoadProperties）。
- 全量 271 张 PNG 嵌入导致 DLL 很大（~66MB）；修崩后再考虑裁剪。
- 尼泊尔等透明 outline 未做。
- UI 未改；沿用现有 flag 控件即可。
