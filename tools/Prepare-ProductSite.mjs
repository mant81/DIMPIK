import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const root = path.resolve(import.meta.dirname, '..');
const site = path.join(root, 'site-staging');
const version = '1.0.6';
const filename = `dimpik_inst_x64_${version}.exe`;
const binary = fs.readFileSync(path.join(root, 'release', 'setup', filename));
const sha256 = crypto.createHash('sha256').update(binary).digest('hex');
const size = `${(binary.length / 1_000_000).toFixed(1)} MB`;
fs.writeFileSync(path.join(site, 'release', filename), binary);
fs.copyFileSync(path.join(root, 'LICENSE.txt'), path.join(site, 'release', 'DIMPIK-LICENSE.txt'));
fs.copyFileSync(path.join(root, 'docs', 'screenshots', 'dimpik-settings-general.png'), path.join(site, 'dimpik', 'assets', 'dimpik-settings-3.png'));
fs.writeFileSync(path.join(site, 'release', `dimpik-${version}.json`), JSON.stringify({ product:'DIMPIK', version, date:'2026-11-09', publisher:'AAStudio.kr', license:'개인·기업·관공서 모두 무료', platform:'Windows 10/11 x64', filename, url:`/release/${filename}`, sizeBytes:binary.length, sha256, installDirectory:path.win32.join('C:/','Program Files','DIMPIK') }, null, 2) + '\n');

const pagePath = path.join(site, 'dimpik', 'index.html');
let page = fs.readFileSync(pagePath, 'utf8');
page = page.replace(/<dd(?: data-release-file)?>dimpik_inst_x64_\d+\.\d+\.\d+\.exe · [^<]*<\/dd>/, `<dd data-release-file>${filename} · ${size}</dd>`);
page = page.replace(/<dd(?: data-release-sha256)?>(?:RELEASE_SHA256|[a-f0-9]{64})<\/dd>/, `<dd data-release-sha256>${sha256}</dd>`);
fs.writeFileSync(pagePath, page);

const indexPath = path.join(site, 'index.html');
let index = fs.readFileSync(indexPath, 'utf8');
if (!index.includes('dimpik/product.css')) index = index.replace('<link rel="stylesheet" href="style.css">', '<link rel="stylesheet" href="style.css">\n  <link rel="stylesheet" href="dimpik/product.css">');
index = index.replace('href="#tools">Free tools', 'href="#products">Products');
const section = `    <section id="products" class="product-launch" aria-labelledby="products-title">
      <div class="section-top"><h2 id="products-title">Tools for a clearer day.</h2><span>FREE SOFTWARE / AASTUDIO</span></div>
      <article class="product-launch-card"><div><div class="product-identity"><img src="dimpik/assets/dimpik.svg" alt="" width="36" height="36">WINDOWS · VERSION ${version}</div><h3>DIMPIK</h3><p>캡처부터 설명, 전달까지.<br>여러 캡처를 탭으로 관리하고 화살표·텍스트·모자이크·블러로 필요한 부분을 편집하세요.</p><div class="product-actions"><a class="snap-button" href="dimpik/">제품 설명 ↗</a><a class="snap-button secondary" href="release/${filename}" download>무료 다운로드 ↓</a></div><span class="product-caption">개인·기업·관공서 모두 무료 · Windows 10 / 11 x64</span></div><figure class="product-shot"><a href="dimpik/"><img src="dimpik/assets/dimpik-gallery.png" alt="DIMPIK의 캡처 탭과 편집 도구, 선택형 갤러리 화면" width="1280" height="800" loading="lazy"></a><figcaption>실제 앱 화면 · 예시 캡처 사용</figcaption></figure></article>
    </section>
`;
if (!index.includes('id="products"')) index = index.replace('    <section id="tools"', section + '    <section id="tools"');
fs.writeFileSync(indexPath, index);

const workflowPath = path.join(site, '.github', 'workflows', 'pages.yml');
let workflow = fs.readFileSync(workflowPath, 'utf8');
if (!workflow.includes('cp -R dimpik release _site/')) workflow = workflow.replace('cp index.html style.css script.js site-config.js favicon.svg CNAME .nojekyll _site/', 'cp index.html style.css script.js site-config.js favicon.svg CNAME .nojekyll _site/\n          cp -R dimpik release _site/');
fs.writeFileSync(workflowPath, workflow);
console.log(JSON.stringify({ version, filename, sizeBytes:binary.length, sha256, site }, null, 2));
