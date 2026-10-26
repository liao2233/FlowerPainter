"""One-time source import. Requires the pinned country-flags checkout passed as argv[1]."""
import sys, pathlib, shutil, urllib.request, re, html, json, hashlib
import xml.etree.ElementTree as ET

root = pathlib.Path(__file__).resolve().parents[2]
dest = root / 'Assets/Flags/svg'
dest.mkdir(parents=True, exist_ok=True)
for p in (pathlib.Path(sys.argv[1]) / 'svg').glob('*.svg'):
    shutil.copyfile(p, dest / p.name)
shutil.copyfile(dest / 'af.svg', dest / 'af-republic.svg')
sources = {
    'un': ('Flag_of_the_United_Nations.svg', 'United Nations', '联合国', 'PD-US-no notice-UN'),
    'who': ('Flag_of_WHO.svg', 'World Health Organization', '世界卫生组织', 'PD-US-no notice-UN'),
    'wto': ('WTO_Logo.svg', 'World Trade Organization (emblem on white)', '世界贸易组织（白底徽标）', 'PD-textlogo'),
    'nato': ('Flag_of_NATO.svg', 'NATO', '北约', 'PD-US-not renewed'),
    'au-union': ('Flag_of_the_African_Union.svg', 'African Union', '非洲联盟', 'PD-self / PD-ineligible'),
    'af': ('Flag_of_the_Taliban.svg', 'Afghanistan (2021–)', '阿富汗（2021—）', 'PD-script'),
}
metadata = []
for code, (file, en, zh, license) in sources.items():
    url = 'https://commons.wikimedia.org/wiki/File:' + urllib.parse.quote(file)
    def get(url):
        return urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent':'FlowerPainter asset importer/1.0'}), timeout=45).read()
    digest = hashlib.md5(file.encode()).hexdigest()
    link = 'https://upload.wikimedia.org/wikipedia/commons/' + digest[0] + '/' + digest[:2] + '/' + urllib.parse.quote(file)
    data = get(link).decode()
    if code == 'wto':
        # A theme composition, not a claim that this is an official WTO flag.
        ET.register_namespace('', 'http://www.w3.org/2000/svg')
        emblem = ET.fromstring(data)
        if 'viewBox' not in emblem.attrib:
            emblem.set('viewBox', '0 0 95 88')
        for k,v in dict(x='80',y='35',width='140',height='130').items(): emblem.set(k,v)
        data = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 200"><path fill="white" d="M0 0h300v200H0z"/>' + ET.tostring(emblem,encoding='unicode') + '</svg>'
    (dest / (code+'.svg')).write_text('\n'.join(line.rstrip() for line in data.splitlines())+'\n', encoding='utf-8')
    metadata.append(dict(id=code,en=en,zh=zh,source=url,download=link,license=license))
    print(code, len(data), flush=True)
(root / 'Assets/Flags/extra-sources.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
