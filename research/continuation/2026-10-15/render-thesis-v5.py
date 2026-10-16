"""Render every final thesis page and record text/layout diagnostics."""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

import pdfplumber
from PIL import Image, ImageDraw, ImageFont
from pypdf import PdfReader

ROOT = Path('C:/RideBoundContinuation/2026-10-15/thesis-v5')
OUT = ROOT / (sys.argv[1] if len(sys.argv) > 1 else 'qa-v5-03')
OUT.mkdir(exist_ok=False)
pdf = ROOT / 'main.pdf'
reader = PdfReader(pdf)
texts = [p.extract_text() or '' for p in reader.pages]
(OUT / 'all-pages.txt').write_text('\n\n'.join(f'=== Physical page {i+1} ===\n{t}' for i,t in enumerate(texts)), encoding='utf-8')
diagnostics = []
with pdfplumber.open(pdf) as document:
    for i, page in enumerate(document.pages):
        outside = [c['text'] for c in page.chars if c['x0'] < -1 or c['x1'] > page.width+1 or c['top'] < -1 or c['bottom'] > page.height+1]
        diagnostics.append(dict(page=i+1, width=page.width, height=page.height,
                                chars=len(page.chars), outside=outside))
poppler = Path('C:/Users/quang/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe')
subprocess.run([str(poppler), '-r', '120', '-png', str(pdf), str(OUT / 'page')], check=True, capture_output=True)
pngs = sorted(OUT.glob('page-*.png'))
assert len(pngs) == len(texts)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 22)
for start in range(0, len(pngs), 6):
    sheet = Image.new('RGB', (1500, 1480), '#dce1e6')
    draw = ImageDraw.Draw(sheet)
    for j, path in enumerate(pngs[start:start+6]):
        page = Image.open(path).convert('RGB')
        page.thumbnail((480, 685))
        x = (j % 3)*500 + (500-page.width)//2
        y = (j // 3)*740 + 35
        sheet.paste(page, (x,y))
        draw.text(((j % 3)*500+15, (j//3)*740+5), f'Page {start+j+1}', font=font, fill='#111111')
    sheet.save(OUT / f'contact-{start+1:02d}-{min(start+6,len(pngs)):02d}.jpg', quality=92)
log = (ROOT / 'main.log').read_text(encoding='utf-8', errors='replace')
issues = [line for line in log.splitlines() if re.search(r'Overfull|undefined|multiply defined|duplicate destination|^!|error:',line,re.I)]
summary = dict(pdfSha256=hashlib.sha256(pdf.read_bytes()).hexdigest(), pages=len(texts),
               renderCount=len(pngs), logIssues=issues, outsidePageChars=sum(len(d['outside']) for d in diagnostics),
               internalStageLabels=[i+1 for i,t in enumerate(texts) if re.search(r'\bstage\s*[12]',t,re.I)],
               sectionPages=[dict(page=i+1, text=t[:130]) for i,t in enumerate(texts) if any(q in t for q in ['Vì sao cần đổi', 'Hai thứ tự chọn', 'Kết quả và đánh đổi', 'Bảy lịch sử', 'Phép sàng lọc bỏ'])],
               diagnostics=diagnostics)
(OUT / 'automated-qa.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k!='diagnostics'},ensure_ascii=False,indent=2))
