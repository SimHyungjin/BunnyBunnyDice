"""카드 아이콘 PNG 내보내기.

icons.js + list.js의 아이콘을 헤드리스 Edge로 찍어 512px PNG로 Assets/_Project/<경로>.png에 쓴다.
그림마다 빈 여백을 잘라내 칸을 꽉 채운다(가장자리 2%) — 카드 칸에서 아이콘이 작아 보이지 않게(2026-11-04 사용자).
이미 있는 파일은 덮어써 GUID(카드 연결)가 유지되고, 새 파일은 스프라이트 메타를 만들어 둔다 — 카드 SO의 Icon 연결은 따로 한다.

실행: python Tools/CardIcons/export.py        (필요: Edge, node, Pillow)
"""
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import uuid

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project'))
EDGE = os.environ.get('EDGE', r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe')
CELL = 512
PER_SHOT = 24
COLS = 6
EDGE_MARGIN = 0.04
# 새 파일 메타의 본(스프라이트, 밉맵 없음). 다른 아이콘과 같은 임포트 설정이 되게 기존 아이콘 메타를 빌린다.
META_TEMPLATE = os.path.join(PROJECT, 'Skill', 'Icons', 'Icon_Skill_CashOut.png.meta')


def names():
    # icons.js는 DOM을 건드리지 않아 node에서 그대로 읽힌다 — 목록 순서를 export.html과 같게 얻는다.
    js = ''.join(open(os.path.join(HERE, f), encoding='utf-8').read() + '\n' for f in ('icons.js', 'list.js'))
    js += 'console.log(JSON.stringify(iconList().map(x => x[0])));'
    out = subprocess.run(['node', '-e', js], capture_output=True, text=True, encoding='utf-8', check=True)
    return json.loads(out.stdout)


def shoot(start, path):
    url = 'file:///' + os.path.join(HERE, 'export.html').replace('\\', '/') + '#%d' % start
    subprocess.run([EDGE, '--headless=new', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000',
                    '--force-device-scale-factor=1', '--window-size=%d,%d' % (CELL * COLS, CELL * (PER_SHOT // COLS)),
                    '--virtual-time-budget=5000', '--screenshot=' + path, url],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)


def trim(cell):
    box = cell.getbbox()
    if box is None:
        return cell
    if box[0] == 0 or box[1] == 0 or box[2] == CELL or box[3] == CELL:
        print('  경고: 칸 가장자리에 닿았다(잘렸을 수 있다)')
    w, h = box[2] - box[0], box[3] - box[1]
    side = round(max(w, h) * (1 + EDGE_MARGIN))
    square = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    square.paste(cell.crop(box), ((side - w) // 2, (side - h) // 2))
    return square.resize((CELL, CELL), Image.LANCZOS)


def ensure_meta(png):
    meta = png + '.meta'
    if os.path.exists(meta):
        return False
    text = open(META_TEMPLATE, encoding='utf-8').read()
    text = re.sub(r'(?m)^guid: .*$', 'guid: ' + uuid.uuid4().hex, text, count=1)
    open(meta, 'w', encoding='utf-8', newline='\n').write(text)
    return True


def main():
    only = set(sys.argv[1:])  # 경로 일부를 주면 그것만 쓴다(예: Icon_Skill_Rack)
    all_names = names()
    tmp = tempfile.mkdtemp()
    try:
        for start in range(0, len(all_names), PER_SHOT):
            shot = os.path.join(tmp, 'part_%d.png' % start)
            shoot(start, shot)
            sheet = Image.open(shot).convert('RGBA')
            for k, name in enumerate(all_names[start:start + PER_SHOT]):
                if only and not any(o in name for o in only):
                    continue
                x, y = (k % COLS) * CELL, (k // COLS) * CELL
                print(name)
                png = os.path.join(PROJECT, *name.split('/')) + '.png'
                os.makedirs(os.path.dirname(png), exist_ok=True)
                trim(sheet.crop((x, y, x + CELL, y + CELL))).save(png)
                if ensure_meta(png):
                    print('  새 파일 — 카드 SO의 Icon에 연결할 것')
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == '__main__':
    main()
