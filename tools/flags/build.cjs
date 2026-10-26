// Rebuild the embedded flag maps and UI thumbnails from the checked-in vectors.
// Uses the project's existing Playwright Chromium; no runtime image decoder or network.
const fs = require('fs'), path = require('path');
const { chromium } = require('../../UI/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), dir = path.join(root, 'Assets/Flags');
const groups = {
  asia: 'ae af am az bd bh bn bt cc cn cx cy ge hk id il in io iq ir jo jp kg kh kp kr kw kz la lb lk mm mn mo mv my np om ph pk ps qa sa sg sy th tj tl tm tr tw uz vn ye',
  europe: 'ad al at ax ba be bg by ch cz de dk ee es fi fo fr gb gb-eng gb-nir gb-sct gb-wls gg gi gr hr hu ie im is it je li lt lu lv mc md me mk mt nl no pl pt ro rs ru se si sj sk sm ua va xk',
  americas: 'ag ai ar aw bb bl bm bo bq br bs bz ca cl co cr cu cw dm do ec fk gd gf gl gp gs gt gy hn ht jm kn ky lc mf mq ms mx ni pa pe pm pr py sr sv sx tc tt us uy vc ve vg vi',
  oceania: 'as au ck fj fm gu hm ki mh mp nc nf nr nu nz pf pg pn pw sb tk to tv um vu wf ws',
  africa: 'ao bf bi bj bw cd cf cg ci cm cv dj dz eg eh er et ga gh gm gn gq gw ke km lr ls ly ma mg ml mr mu mw mz na ne ng re rw sc sd sh sl sn so ss st sz td tg tn tz ug yt za zm zw',
  polar: 'aq bv tf',
};
const simple = new Set('ae am at az bd be bg bh bj bo bs bw cd cf cg ch ci cm co cr cu cz de dk dz ee fi fr ga gb-eng gb-sct ge gh gm gn gr gw hn hu id ie il is it jm jo jp la lb lr lt lu lv ly mc mg ml mr mu mv na ne ng ni nl no nr om pa pe pl ps qa ro ru rw sc sd se sg si sl sn so sr ss st sy td tg th tn tr tt ua ve vn ye za'.split(' '));
const medium = new Set('al ar au bb bf bi br ca cn cv eu fm gb gd gl gy hk in ke kg kh ki km kn kp kr kw kz lc ls mk mn mo my nc np nz ph pk pt pw sb sk sm sz tl to tv tz ug us uy uz va vc ws xk'.split(' '));
const overrides = {
  'af-republic': ['Afghanistan (Republic, 2004–2021)', '阿富汗（共和国，2004—2021）'],
  'gb-eng':['England','英格兰'], 'gb-sct':['Scotland','苏格兰'], 'gb-wls':['Wales','威尔士'],
  'gb-nir':['Northern Ireland (Ulster Banner, historical)','北爱尔兰（历史乌尔斯特旗）'],
};
function theme(id,en,zh,body,bg='white') {
  fs.writeFileSync(path.join(dir,'svg',id+'.svg'),`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 200"><path fill="${bg}" d="M0 0h300v200H0z"/>${body}</svg>`);
  overrides[id]=[en,zh];
}
theme('red-cross','Red Cross','红十字','<path fill="#d8202f" d="M130 40h40v40h40v40h-40v40h-40v-40H90V80h40z"/>');
theme('red-crescent','Red Crescent','红新月','<circle fill="#d8202f" cx="145" cy="100" r="65"/><circle fill="white" cx="170" cy="100" r="55"/>');
theme('red-crystal','Red Crystal','红水晶','<path fill="none" stroke="#d8202f" stroke-width="17" d="m150 35 65 65-65 65-65-65z"/>');
theme('first-aid','First aid','急救（绿底白十字）','<path fill="white" d="M130 40h40v40h40v40h-40v40h-40v-40H90V80h40z"/>','#168242');
theme('medical','Medical / hospital','医疗／医院','<path fill="white" d="M105 45h25v42h40V45h25v110h-25v-43h-40v43h-25z"/>','#2352b4');
theme('peace','Peace','和平','<g fill="none" stroke="white" stroke-width="12"><circle cx="150" cy="100" r="65"/><path d="M150 35v130m0-65-46 46m46-46 46 46"/></g>','#48b5cd');
theme('rainbow','Rainbow pride','彩虹骄傲旗',['#d8202f','#f17d25','#ffd338','#168242','#2352b4','#78399d'].map((c,i)=>`<path fill="${c}" d="M0 ${i*200/6}h300v${200/6+0.1}H0z"/>`).join(''));
theme('transgender','Transgender pride','跨性别骄傲旗',['#48b5cd','#f288b2','white','#f288b2','#48b5cd'].map((c,i)=>`<path fill="${c}" d="M0 ${i*40}h300v40H0z"/>`).join(''));
theme('checkered','Racing / checkered','赛车／方格旗',Array.from({length:6},(_,y)=>Array.from({length:9},(_,x)=>(x+y)%2?'':`<path d="M${x*300/9} ${y*200/6}h${300/9+.1}v${200/6+.1}h-${300/9+.1}z"/>`).join('')).join(''));
theme('white','White flag','白旗','');
const extras = JSON.parse(fs.readFileSync(path.join(dir,'extra-sources.json'),'utf8'));
for(const e of extras) overrides[e.id]=[e.en,e.zh];
const en = new Intl.DisplayNames(['en'], {type:'region'}), zh = new Intl.DisplayNames(['zh-Hans'], {type:'region'});
const palette = ['D8202F','FFD338','2352B4','F5F0E4','F288B2','78399D','F17D25','B72A74','201D2D','168242','48B5CD','182D68'].map(h=>h.match(/../g).map(n=>parseInt(n,16)));
async function main() {
 const browser=await chromium.launch({headless:true}), page=await browser.newPage();
 const all=[];fs.mkdirSync(path.join(root,'UI/src/flags/images'),{recursive:true});
 for(const file of fs.readdirSync(path.join(dir,'svg')).filter(s=>s.endsWith('.svg')).sort()) {
  const code=file.slice(0,-4), id=({cn:'china',jp:'japan',fr:'france'})[code]||code;
  const names=overrides[code]||[en.of(code.toUpperCase()),zh.of(code.toUpperCase())];
  const group=Object.keys(groups).find(g=>groups[g].split(' ').includes(code))||(code==='af-republic'?'asia':extras.some(e=>e.id===code)||code==='eu'?'organizations':'themes');
  const svg=fs.readFileSync(path.join(dir,'svg',file),'utf8');
  const result=await page.evaluate(async({svg,palette})=>{
   const doc=new DOMParser().parseFromString(svg,'image/svg+xml'), el=doc.documentElement;
   if(doc.querySelector('parsererror')) throw Error('Invalid SVG');
   const vb=el.getAttribute('viewBox')?.trim().split(/[ ,]+/).map(Number);
   const ratio=vb?vb[2]/vb[3]:parseFloat(el.getAttribute('width'))/parseFloat(el.getAttribute('height'));
   if(!vb)el.setAttribute('viewBox',`0 0 ${parseFloat(el.getAttribute('width'))} ${parseFloat(el.getAttribute('height'))}`);
   const width=512,height=Math.round(width/ratio);
   el.setAttribute('width',String(width));el.setAttribute('height',String(height));
   const img=new Image();img.src='data:image/svg+xml;base64,'+btoa(unescape(encodeURIComponent(new XMLSerializer().serializeToString(doc))));
   await img.decode();const canvas=document.createElement('canvas');canvas.width=width;canvas.height=height;
   const c=canvas.getContext('2d');c.drawImage(img,0,0,width,height);const pixels=c.getImageData(0,0,width,height), indices=[];
   const cache=new Map();
   for(let i=0;i<pixels.data.length;i+=4){
    const p=pixels.data;let v=255;
    if(p[i+3]>=128){
     const key=(p[i]<<16)|(p[i+1]<<8)|p[i+2];v=cache.get(key);
     if(v===undefined){
      const hi=Math.max(p[i],p[i+1],p[i+2]),lo=Math.min(p[i],p[i+1],p[i+2]);
      let best=Infinity;palette.forEach((rgb,j)=>{
       // A dark saturated navy is still blue, not the nearest neutral black flower.
       if(j===8&&hi>45&&(hi-lo)/hi>.45)return;
       const d=rgb.reduce((s,x,k)=>s+(x-p[i+k])**2,0);if(d<best){best=d;v=j+4;}
      });cache.set(key,v);
     }
    }
    indices.push(v); if(v===255)p[i+3]=0;else {const rgb=palette[v-4];p[i]=rgb[0];p[i+1]=rgb[1];p[i+2]=rgb[2];p[i+3]=255;}
   }
   c.putImageData(pixels,0,0);
   const thumb=document.createElement('canvas');thumb.width=128;thumb.height=Math.round(128/ratio);thumb.getContext('2d').drawImage(canvas,0,0,thumb.width,thumb.height);
   return {ratio,width,height,indices,png:thumb.toDataURL('image/png').split(',')[1]};
  },{svg,palette});
  const recommendedColumns=['red-cross','first-aid','medical','rainbow','transgender','white','checkered'].includes(code)?40:
    ['red-crescent','red-crystal','peace'].includes(code)?60:code==='us'?200:code==='un'||code==='who'?220:simple.has(code)?40:medium.has(code)?100:200;
  const entry={id,code,en:names[0],zh:names[1],group,ratio:result.ratio,columns:recommendedColumns};
  if(code==='np') {
   const left=[],right=[];
   for(let y=0;y<result.height;y++) {
    let lo=0,hi=result.width-1;while(lo<=hi&&result.indices[y*result.width+lo]===255)lo++;
    while(hi>=lo&&result.indices[y*result.width+hi]===255)hi--;
    if(lo<=hi){left.push([lo/result.width,1-y/result.height]);right.push([(hi+1)/result.width,1-y/result.height]);}
   }
   function simplify(p) {
    if(p.length<3)return p;const a=p[0],b=p[p.length-1];let best=0,k=0;
    for(let i=1;i<p.length-1;i++){const d=Math.abs((b[0]-a[0])*(a[1]-p[i][1])-(a[0]-p[i][0])*(b[1]-a[1]))/Math.hypot(b[0]-a[0],b[1]-a[1]);if(d>best){best=d;k=i;}}
    return best>.002?simplify(p.slice(0,k+1)).slice(0,-1).concat(simplify(p.slice(k))):[a,b];
   }
   entry.outline=simplify(left).concat(simplify(right).reverse());
  }
  all.push(entry);
  // RLE: width, height, followed by (uint16 length, byte palette index). Transparent = 255.
  const chunks=[], header=Buffer.alloc(4);header.writeUInt16LE(result.width,0);header.writeUInt16LE(result.height,2);chunks.push(header);
  for(let i=0;i<result.indices.length;){let end=i+1;while(end<result.indices.length&&result.indices[end]===result.indices[i]&&end-i<65535)end++;const b=Buffer.alloc(3);b.writeUInt16LE(end-i);b[2]=result.indices[i];chunks.push(b);i=end;}
  fs.mkdirSync(path.join(dir,'maps'),{recursive:true});fs.writeFileSync(path.join(dir,'maps',id+'.bin'),Buffer.concat(chunks));
  fs.writeFileSync(path.join(root,'UI/src/flags/images',id+'.png'),Buffer.from(result.png,'base64'));
 }
 await browser.close();
 fs.writeFileSync(path.join(dir,'catalog.json'),JSON.stringify(all,null,2)+'\n');
 fs.writeFileSync(path.join(root,'UI/src/flags/catalog.ts'),'// Generated by tools/flags/build.cjs.\n'+all.map((f,i)=>`import image${i} from "./images/${f.id}.png";`).join('\n')+'\nexport const flags = '+JSON.stringify(all).replace(/\{"id":"([^"]+)"/g,(_,id)=>`{"image":image${all.findIndex(f=>f.id===id)},"id":"${id}"`)+';\n');
 for(const lang of ['en-US','zh-HANS'])fs.writeFileSync(path.join(root,`Lang/${lang}.flags.json`),JSON.stringify(Object.fromEntries(all.map(f=>['FlowerPainter.Floral.flagTemplate.'+f.id,lang==='en-US'?f.en:f.zh])),null,2)+'\n');
 console.log(`Built ${all.length} flag templates.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
