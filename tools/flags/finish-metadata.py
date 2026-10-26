"""Update template-local strings and produce a readable source/coverage index."""
import json, pathlib
root=pathlib.Path(__file__).resolve().parents[2]
strings={
 'en-US':{
  'FlowerPainter.Floral.Stat.FlagWidth':'Flag width', 'FlowerPainter.Floral.Stat.FlagHeight':'Flag height',
  'FlowerPainter.Floral.Issue.FlagSmall':'This flag is {0} × {1} m. At this flower spacing, try {2} × {3} m or larger to retain its main details. Small emblems and lettering may still be simplified.',
  'FlowerPainter.Floral.Issue.FlagClipped':'The selection clips part of this flag. Use a rectangular selection to preserve the complete design.'},
 'zh-HANS':{
  'FlowerPainter.Floral.Stat.FlagWidth':'旗帜宽度', 'FlowerPainter.Floral.Stat.FlagHeight':'旗帜高度',
  'FlowerPainter.Floral.Issue.FlagSmall':'当前旗帜 {0} × {1} 米。按此花头间距，建议至少 {2} × {3} 米以保留主要细节；小徽章和文字仍可能简化。',
  'FlowerPainter.Floral.Issue.FlagClipped':'选区裁掉了部分旗帜。使用矩形选区可以保留完整图案。',
  'FlowerPainter.UI.Flags.Tip':'每面旗帜保持自己的比例。异形选区可能裁掉图案，建议用矩形选区摆放完整旗帜。',
  'FlowerPainter.UI.Flags.All':'全部','FlowerPainter.UI.Flags.Asia':'亚洲','FlowerPainter.UI.Flags.Europe':'欧洲','FlowerPainter.UI.Flags.Americas':'美洲','FlowerPainter.UI.Flags.Oceania':'大洋洲','FlowerPainter.UI.Flags.Africa':'非洲','FlowerPainter.UI.Flags.Polar':'极地区域','FlowerPainter.UI.Flags.Organizations':'国际组织','FlowerPainter.UI.Flags.Themes':'医疗与主题',
  'FlowerPainter.UI.Flags.Ratio':'宽 : 高 {0} : 1','FlowerPainter.UI.Flags.Search':'搜索名称／英文／地区代码',
  'FlowerPainter.UI.Flags.Count':'{0} / {1} 个模板','FlowerPainter.UI.Flags.NoMatches':'没有匹配的旗帜',
  'FlowerPainter.UI.Flags.ActualSize':'当前旗帜：{0} × {1} 米','FlowerPainter.UI.Flags.RecommendedSize':'按当前花头间距，建议 {0} × {1} 米或更大',
  'FlowerPainter.UI.Flags.SizeTip':'小旗帜仍可种植，但星徽、文字和细小图案可能丢失。这是辨识参考，不是硬性限制。'}
}
for lang,values in strings.items():
 p=root/f'Lang/{lang}.flowerpatch.json';data=json.loads(p.read_text(encoding='utf-8-sig'))
 for code in ['china','japan','france']:data.pop('FlowerPainter.Floral.flagTemplate.'+code,None)
 if lang=='en-US':data.pop('FlowerPainter.UI.Flags.Tip',None)
 data.update(values);p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
catalog=json.loads((root/'Assets/Flags/catalog.json').read_text(encoding='utf-8'))
rows=['# Flag template catalogue','',f'{len(catalog)} bundled templates. Width/height ratios come from source SVG view boxes. Suggested widths are artistic sampling guides at the default 0.28 m bloom spacing, not official flag specifications.','',
 '| Code | English | 中文 | Group | Width / height | Suggested width (m) |','|---|---|---|---|---:|---:|']
for f in catalog:rows.append(f'| {f["code"]} | {f["en"]} | {f["zh"]} | {f["group"]} | {f["ratio"]:.3f} | {f["columns"]*.28:.1f} |')
(root/'Assets/Flags/CATALOG.md').write_text('\n'.join(rows)+'\n',encoding='utf-8')
