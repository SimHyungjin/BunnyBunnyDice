"""규칙 책상 화면(UI_RulesScreen)의 그림 스프라이트를 뽑는다.

색은 굽지 않는다 — 흰 그림을 게임이 SO 색(주사위·칩 색)으로 물들인다. 그래서 색을 바꿔도 그림을 다시 뽑지 않는다.
공격 그림은 두 장이 한 벌이다: *_Shape(주사위·그 색으로 칠할 부분, 흰색 — 물들인다) + *_Mark(선·적·노랑 강조, 색 그대로).
빛 번짐(glow)은 굽지 않는다 — 게임 블룸이 하므로 겹치면 뿌옇다(사용자 2026-11-12).

이미 있는 PNG는 덮어써 GUID가 유지되고, 메타가 없으면 스프라이트 메타를 만든다(판은 9-slice 테두리 포함).

실행: python Tools/RulesScreen/sprites.py      (필요: Chrome, Pillow)
"""
import os
import subprocess
import tempfile
import uuid

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'RulesTable', 'Sprites'))
CHROME = os.environ.get('CHROME', r'C:\Program Files\Google\Chrome\Application\chrome.exe')

LINE = '#fbeefc'    # 공격선(흰빛)
ENEMY = '#a9d3df'   # 적
YELLOW = '#ffe04a'  # 치명·표적 강조
W = '#ffffff'       # 물들일 부분


# 이름 → (가로, 세로, viewBox, 내용, 9-slice 테두리 px 또는 0)
SPRITES = {}

# 둥근 판 — 탭·칸 바탕(채움)과 테두리(선). 9-slice라 어느 비율로 늘려도 모서리가 그대로다.
SPRITES['Panel_Fill'] = (128, 128, '0 0 128 128', f'<rect x="0" y="0" width="128" height="128" rx="28" fill="{W}"/>', 40)
SPRITES['Panel_Line'] = (128, 128, '0 0 128 128',
                         f'<rect x="3" y="3" width="122" height="122" rx="25" fill="none" stroke="{W}" stroke-width="6"/>', 40)

# 면체 윤곽(합성 장) — 6면 둥근 네모, 12면 오각형, 20면 육각형. 굵기는 같은 눈금(100)에서 같게.
SPRITES['Die_6'] = (256, 256, '-4 -4 108 108', f'<rect x="8" y="8" width="84" height="84" rx="14" fill="none" stroke="{W}" stroke-width="7"/>', 0)
SPRITES['Die_12'] = (256, 256, '-4 -4 108 108',
                     f'<polygon points="50,4 96,38 79,94 21,94 4,38" fill="none" stroke="{W}" stroke-width="7" stroke-linejoin="round"/>', 0)
SPRITES['Die_20'] = (256, 256, '-4 -4 108 108',
                     f'<polygon points="50,3 93,27 93,73 50,97 7,73 7,27" fill="none" stroke="{W}" stroke-width="7" stroke-linejoin="round"/>', 0)

# 칩 고리(칩 장).
SPRITES['Ring'] = (256, 256, '0 0 100 100', f'<circle cx="50" cy="50" r="42" fill="none" stroke="{W}" stroke-width="12"/>', 0)

# 공격 그림 — 시안(MonitorTall G2)과 같은 그림을 두 층으로 나눴다. 주사위는 왼쪽(난사만 가운데), 적은 오른쪽.
DIE = f'<rect x="1" y="9" width="10" height="10" rx="2" fill="{W}"/>'
ATTACKS = {
    # 강타: 굵은 선 + 맞은 적에 노란 충격.
    'Red': (DIE,
            f'<path d="M12 14 L30 14" stroke="{LINE}" stroke-width="4"/><circle cx="34" cy="14" r="5" fill="{ENEMY}"/>'
            f'<path d="M34 3 L35 8 M34 25 L35 20 M39 9 L37 11 M39 19 L37 17" stroke="{YELLOW}" stroke-width="2"/>'),
    # 속사: 짧은 선 여러 개.
    'Green': (DIE,
              f'<path d="M13 10 L18 10 M20 14 L25 14 M13 18 L18 18" stroke="{LINE}" stroke-width="2"/><circle cx="34" cy="14" r="5" fill="{ENEMY}"/>'),
    # 저격: 먼 적에 점선 + 노란 표적.
    'Blue': (DIE,
             f'<path d="M12 14 L32 14" stroke="{LINE}" stroke-width="1.5" stroke-dasharray="2 2"/><circle cx="35" cy="14" r="4" fill="{ENEMY}"/>'
             f'<circle cx="35" cy="14" r="6.5" fill="none" stroke="{YELLOW}" stroke-width="1"/>'),
    # 폭발: 터지는 원(주사위 색) 안의 적 여럿.
    'Yellow': (DIE + f'<circle cx="30" cy="14" r="9" fill="none" stroke="{W}" stroke-width="1.5"/>',
               f'<path d="M12 14 L21 14" stroke="{LINE}" stroke-width="2"/><circle cx="28" cy="14" r="3" fill="{ENEMY}"/>'
               f'<circle cx="33" cy="10" r="3" fill="{ENEMY}"/><circle cx="33" cy="19" r="3" fill="{ENEMY}"/>'),
    # 체인: 적에서 적으로 튀는 선.
    'Magenta': (DIE,
                f'<path d="M12 14 L20 8 L28 18 L36 10" stroke="{LINE}" stroke-width="2" fill="none"/><circle cx="20" cy="8" r="3" fill="{ENEMY}"/>'
                f'<circle cx="28" cy="18" r="3" fill="{ENEMY}"/><circle cx="36" cy="10" r="2.5" fill="{ENEMY}"/>'),
    # 관통: 줄 선 적을 꿰는 선(주사위 색).
    'Cyan': (DIE + f'<path d="M12 14 L40 14" stroke="{W}" stroke-width="2.5"/>',
             f'<circle cx="20" cy="14" r="3" fill="{ENEMY}"/><circle cx="28" cy="14" r="3" fill="{ENEMY}"/><circle cx="36" cy="14" r="3" fill="{ENEMY}"/>'),
    # 난사: 가운데 주사위에서 사방으로.
    'White': (f'<rect x="15" y="9" width="10" height="10" rx="2" fill="{W}"/>',
              f'<path d="M15 12 L5 4 M15 16 L4 22 M25 12 L35 4 M25 16 L36 22 M20 9 L20 2" stroke="{LINE}" stroke-width="1.5"/>'
              f'<circle cx="5" cy="4" r="2.5" fill="{ENEMY}"/><circle cx="4" cy="22" r="2.5" fill="{ENEMY}"/>'
              f'<circle cx="35" cy="4" r="2.5" fill="{ENEMY}"/><circle cx="36" cy="22" r="2.5" fill="{ENEMY}"/>'),
    # 기본(검정): 속이 빈 주사위 + 가는 선 하나.
    'Clear': (f'<rect x="1.5" y="9.5" width="9" height="9" rx="2" fill="none" stroke="{W}" stroke-width="1.2"/>',
              f'<path d="M12 14 L28 14" stroke="{LINE}" stroke-width="1.5"/><circle cx="32" cy="14" r="4" fill="{ENEMY}"/>'),
}
for name, (shape, mark) in ATTACKS.items():
    SPRITES[f'Attack_{name}_Shape'] = (440, 300, '-1 -1 44 30', shape, 0)
    SPRITES[f'Attack_{name}_Mark'] = (440, 300, '-1 -1 44 30', mark, 0)

# 찍을 때 가장자리에서 잘리지 않게 크게 찍어 줄인다.
SCALE = 2


def shoot(name, w, h, view, body, tmp):
    svg = os.path.join(tmp, name + '.html')
    with open(svg, 'w', encoding='utf-8') as f:
        f.write('<!doctype html><html><body style="margin:0;background:transparent;">'
                f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="{view}" style="display:block;overflow:visible;">'
                f'{body}</svg></body></html>')
    png = os.path.join(tmp, name + '.png')
    subprocess.run([CHROME, '--headless=new', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000',
                    f'--force-device-scale-factor={SCALE}', f'--window-size={w},{h}', '--virtual-time-budget=2000',
                    '--screenshot=' + png, 'file:///' + svg.replace('\\', '/')],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
    image = Image.open(png).convert('RGBA').resize((w, h), Image.LANCZOS)
    return image


META = '''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 8
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {b}, y: {b}, z: {b}, w: {b}}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
'''


def main():
    os.makedirs(OUT, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        for name, (w, h, view, body, border) in SPRITES.items():
            image = shoot(name, w, h, view, body, tmp)
            path = os.path.join(OUT, name + '.png')
            image.save(path)
            if not os.path.exists(path + '.meta'):
                with open(path + '.meta', 'w', encoding='utf-8', newline='\n') as f:
                    f.write(META.format(guid=uuid.uuid4().hex, b=border))
            print(name)


if __name__ == '__main__':
    main()
