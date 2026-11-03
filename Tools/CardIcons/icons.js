// 카드 아이콘 그림 소스(페르소나 5 앱 아이콘 스타일, 2026-11-04 사용자 확정).
// 먹색 판(기호 실루엣을 부풀려 따라감) + 흰 기호 + 강조색 한 곳. 강조색 = 카드 SO의 IconColor(주사위·페인트·칩은 의미 색),
// 바깥 테두리 = 흰 줄 + 카드 종류 색(SO_CardHandConfig의 *CardColor). 판 두께 8, 어긋난 그림자 켬.
// index.html(미리보기)과 export.html(PNG 내보내기)이 함께 쓴다. 브라우저 스크립트지만 DOM을 건드리지 않아 node에서도 읽힌다.
const KIND = { dice:'#f5056c', skill:'#40e6b3', exclusive:'#9e5cff', hero:'#59a6ff', chip:'#ffba33' };
const COLORS = [
  ['빨강','#e63333'],['파랑','#336bf2'],['하늘','#66ccf5'],['초록','#33b84d'],
  ['자홍','#e640bf'],['노랑','#f5d933'],['하양','#ffffff'],['검정','#33333b'],
];
const INK = '#0d0b12';
const GOLD = '#ffe04a';
const state = { outline:'white', shadow:true, pad:8 };

// ── 공통: 기호(색 있는 SVG)를 받아 판·그림자를 붙인다.
// 판과 그림자는 기호에서 색 속성만 지운 같은 도형이라, 기호 모양을 그대로 따라간다.
function strip(svg) { svg = svg.replace(/<[^>]*class="d"[^>]*\/>/g, ''); return svg.replace(/ (fill|stroke|stroke-width|fill-opacity|fill-rule)="[^"]*"/g, ''); }
function layer(svg, color, width) {
  return `<g fill="${color}" stroke="${color}" stroke-width="${width}" stroke-linejoin="miter" stroke-miterlimit="3">${svg}</g>`;
}
function icon(kind, svg) {
  const sil = strip(svg);
  const silAll = state.shadow ? sil + `<g transform="translate(9 9)">${sil}</g>` : sil;
  const p = state.pad * 2, k = 7, w = 5;
  let s = '';
  if (state.outline === 'white') s += layer(silAll, kind, p + 2 * (k + w)) + layer(silAll, '#fff', p + 2 * w);
  else s += layer(silAll, kind, p + 2 * k);
  s += layer(silAll, INK, p);
  if (state.shadow) s += `<g transform="translate(9 9)">${layer(sil, kind, 0)}</g>`;
  return s + `<g stroke-linejoin="miter">${svg}</g>`;
}

// ── 도형 도우미
const r1 = v => v.toFixed(1);
function pt(r, deg, cx = 100, cy = 100) { const a = deg * Math.PI / 180; return [cx + r * Math.cos(a), cy + r * Math.sin(a)]; }
function pts(ps) { return ps.map(p => r1(p[0]) + ',' + r1(p[1])).join(' '); }
function P(ps, st) { return `<polygon points="${pts(ps)}" ${st}/>`; }
function Circ(cx, cy, r, st) { return `<circle cx="${cx}" cy="${cy}" r="${r}" ${st}/>`; }
function Rect(x, y, w, h, st) { return `<rect x="${x}" y="${y}" width="${w}" height="${h}" ${st}/>`; }
function Rot(deg, cx, cy, inner) { return `<g transform="rotate(${deg} ${cx} ${cy})">${inner}</g>`; }
function arcBand(cx, cy, ra, rb, a0, a1, n = 28) {
  const o = [], i = [];
  for (let k = 0; k <= n; k++) { const a = a0 + (a1 - a0) * k / n; o.push(pt(rb, a, cx, cy)); i.push(pt(ra, a, cx, cy)); }
  return o.concat(i.reverse());
}
// 호 끝의 화살촉. dir = 1이면 각이 커지는 쪽, -1이면 작아지는 쪽을 가리킨다.
function arcHead(cx, cy, rm, a1, dir, len = 24, half = 18) {
  const p = pt(rm, a1, cx, cy), t = a1 * Math.PI / 180;
  const dx = -Math.sin(t) * dir, dy = Math.cos(t) * dir, nx = Math.cos(t), ny = Math.sin(t);
  return [[p[0] + dx * len, p[1] + dy * len], [p[0] + nx * half - dx * 4, p[1] + ny * half - dy * 4], [p[0] - nx * half - dx * 4, p[1] - ny * half - dy * 4]];
}
function star(cx, cy, R, r, n, rot = -90) {
  const o = [];
  for (let i = 0; i < n * 2; i++) o.push(pt(i % 2 ? r : R, rot + i * 180 / n, cx, cy));
  return o;
}
function quad(p0, p1, w) {
  const dx = p1[0] - p0[0], dy = p1[1] - p0[1], l = Math.hypot(dx, dy), nx = -dy / l * w / 2, ny = dx / l * w / 2;
  return [[p0[0] + nx, p0[1] + ny], [p1[0] + nx, p1[1] + ny], [p1[0] - nx, p1[1] - ny], [p0[0] - nx, p0[1] - ny]];
}
// 칠하기: W = 흰 면 + 먹 선, A = 강조색 + 흰 테, K = 먹, KW = 먹 + 흰 테
const W = `fill="#fff" stroke="${INK}" stroke-width="3.5"`;
const K = `fill="${INK}"`;
const WF = `fill="#fff"`;
const A = c => `fill="${c}" stroke="#fff" stroke-width="5"`;
const AF = c => `fill="${c}"`;
const KW = `fill="${INK}" stroke="#fff" stroke-width="5"`;
// 안쪽 무늬 표시: 판 모양을 계산할 때 빠진다(모서리가 바깥 선을 울퉁불퉁하게 만들지 않게).
const D = st => `class="d" ${st}`;
function isDark(c) { return c.toLowerCase() === '#33333b'; }

// ── 아이콘들 (viewBox 200)
const ICONS = {
  allIn(a) { // 밀어 넣는 칩 더미
    let s = '';
    for (const [y, l] of [[66, 30], [96, 42], [126, 26]]) s += Rect(46 - l, y, l, 9, W);
    let c = '';
    for (let i = 0; i < 4; i++) {
      const y = 132 - i * 30, x = 62 + (i % 2) * 8;
      c += Rect(x, y, 96, 24, A(a));
      for (const sx of [74, 98, 122, 146]) c += Rect(x + sx - 62, y, 8, 24, D(WF));
    }
    return s + Rot(-8, 110, 110, c);
  },
  backspin(a) { // 아래를 친 큐볼 + 공을 감싸고 거꾸로 도는 화살표
    const cx = 108, cy = 108;
    return P(arcBand(cx, cy, 58, 74, -25, -210), A(a)) + P(arcHead(cx, cy, 66, -210, -1, 26, 22), A(a))
      + Circ(cx, cy, 44, W) + Circ(cx, cy + 26, 9, A(a));
  },
  check(a) { // 룩 + 멈춤
    const top = [[58,38],[80,38],[80,52],[92,52],[92,38],[108,38],[108,52],[120,52],[120,38],[142,38],[142,76],[58,76]];
    return Rect(48, 150, 104, 20, W) + Rect(64, 76, 72, 74, W) + P(top, W)
      + Circ(100, 113, 24, A(a)) + Rect(90, 101, 7, 24, K) + Rect(103, 101, 7, 24, K);
  },
  checkmate(a) { // 쓰러지는 킹 + 베기
    const king = 'M70 168 L130 168 L130 154 L120 150 L112 104 L124 98 L120 90 L110 90 C 124 80 122 62 104 58 L104 50 L114 50 L114 42 L104 42 L104 32 L96 32 L96 42 L86 42 L86 50 L96 50 L96 58 C 78 62 76 80 90 90 L80 90 L76 98 L88 104 L80 150 L70 154 Z';
    return Rot(24, 100, 168, `<path d="${king}" ${W}/>`) + P([[22,84],[182,124],[178,138],[18,98]], A(a));
  },
  jackpot(a) { // 왕관 + 눈이 최대인 주사위
    let s = P([[58,72],[48,30],[78,52],[100,22],[122,52],[152,30],[142,72]], A(a));
    s += Rect(62, 82, 76, 76, W);
    for (const x of [82, 118]) for (const y of [99, 120, 141]) s += Rect(x - 7, y - 7, 14, 14, K);
    return Rot(-6, 100, 100, s);
  },
  pocket(a) { // 포켓으로 빠지는 해골 공 — 구멍 앞 테가 공 아래를 가린다
    const front = [], inner = [];
    for (let k = 0; k <= 24; k++) { const t = Math.PI * k / 24; front.push([100 + 64 * Math.cos(t), 150 + 22 * Math.sin(t)]); }
    for (let k = 24; k >= 0; k--) { const t = Math.PI * k / 24; inner.push([100 + 46 * Math.cos(t), 150 + 13 * Math.sin(t)]); }
    return `<ellipse cx="100" cy="150" rx="64" ry="22" ${A(a)}/>` + `<ellipse cx="100" cy="150" rx="46" ry="13" ${K}/>`
      + Circ(100, 106, 46, KW) + Circ(100, 96, 20, WF)
      + Circ(100, 93, 11, K) + Rect(94, 98, 12, 9, K) + Circ(96, 92, 3.5, WF) + Circ(104, 92, 3.5, WF)
      + P(front.concat(inner), A(a));
  },
  rack(a) { // 삼각 랙에 모인 공
    let s = `<path d="M100 26 L170 150 L30 150 Z M100 58 L144 136 L56 136 Z" fill-rule="evenodd" ${A(a)}/>`;
    for (const [x, y] of [[100, 84], [88, 104], [112, 104], [76, 124], [100, 124], [124, 124]]) s += Circ(x, y, 10.5, W);
    return s;
  },
  raise(a) { // 위 화살표 + 겹 무늬
    return P([[100,22],[160,84],[126,84],[126,172],[74,172],[74,84],[40,84]], A(a))
      + P([[100,104],[118,122],[118,134],[100,116],[82,134],[82,122]], WF)
      + P([[100,132],[118,150],[118,162],[100,144],[82,162],[82,150]], WF);
  },
  reroll(a) { // 주사위 + 도는 화살표 두 개
    let s = P(arcBand(100, 100, 56, 70, -40, -150), A(a)) + P(arcHead(100, 100, 63, -150, -1), A(a))
      + P(arcBand(100, 100, 56, 70, 140, 30), A(a)) + P(arcHead(100, 100, 63, 30, -1), A(a));
    s += Rot(12, 100, 100, Rect(70, 70, 60, 60, W) + Rect(78, 78, 12, 12, K) + Rect(94, 94, 12, 12, K) + Rect(110, 110, 12, 12, K));
    return s;
  },
  paint(a) { // 물감 튐 + 붓
    const splash = [[60,112],[44,92],[70,94],[66,66],[90,84],[104,58],[114,86],[142,72],[130,100],[160,108],[130,120],[146,146],[114,132],[104,160],[92,132],[64,148],[74,122]];
    return P(splash, A(a)) + Rot(42, 100, 100,
      Rect(92, 4, 16, 58, W) + Rect(88, 60, 24, 16, `fill="#9a9aa8" stroke="${INK}" stroke-width="3.5"`)
      + P([[88,76],[112,76],[108,100],[100,110],[92,100]], A(a)));
  },
  // ── 전용
  breakShot(a) { // 당구 첫 샷: 큐볼이 날아와 부딪히고 공들이 흩어진다
    let t = '';
    for (const [y, l] of [[90, 34], [112, 46], [134, 30]]) t += Rect(38 - l, y - 4, l, 8, W);
    t += P(star(98, 112, 34, 16, 8, -90), A(a));
    t += Circ(64, 112, 26, W);
    for (const [x, y, r, ang] of [[142, 64, 18, -40], [158, 116, 17, 5], [128, 160, 15, 50]]) {
      const tail = pt(r + 22, ang + 180, x, y), tail2 = pt(r + 6, ang + 180, x, y);
      t += P(quad(tail, tail2, 6), W) + Circ(x, y, r, A(a));
    }
    return t;
  },
  stackedDeck(a) { // 겹친 카드 + 더하기
    let s = '';
    for (const r of [-16, -2]) s += Rot(r, 100, 160, Rect(66, 42, 62, 88, W));
    s += Rot(12, 100, 160, Rect(70, 40, 62, 88, W) + P([[101,70],[113,84],[101,98],[89,84]], A(a)));
    s += Circ(150, 138, 24, A(a)) + Rect(146, 124, 8, 28, WF) + Rect(136, 134, 28, 8, WF);
    return s;
  },
  doubleUp(a) { // 겹 위 화살
    const ch = y => P([[100, y], [164, y + 60], [164, y + 88], [100, y + 28], [36, y + 88], [36, y + 60]], A(a));
    return ch(84) + ch(28);
  },
  coinFlip(a) { // 기울어 도는 동전
    return P(arcBand(100, 100, 74, 84, 200, 250), W) + P(arcBand(100, 100, 74, 84, 20, 70), W)
      + Rot(18, 100, 100, `<ellipse cx="100" cy="100" rx="44" ry="60" ${A(a)}/><ellipse cx="100" cy="100" rx="30" ry="44" ${A(a)}/>`
      + P(star(100, 100, 22, 9, 5).map(p => [100 + (p[0] - 100) * 0.75, p[1]]), WF));
  },
  cashOut(a) { // 칩을 거둬 위로
    let s = Circ(86, 124, 42, W);
    for (let k = 0; k < 6; k++) s += Rot(k * 60, 86, 124, Rect(80, 82, 12, 13, D(K)));
    s += Circ(86, 124, 22, `fill="none" stroke="${INK}" stroke-width="3.5"`);
    s += P(arcBand(86, 124, 56, 70, 190, 300), A(a)) + P(arcHead(86, 124, 63, 300, 1, 28, 22), A(a));
    return s;
  },
  hotStreak(a) { // 불꽃
    return P([[100,20],[124,62],[140,46],[154,96],[148,140],[126,172],[74,172],[52,140],[48,96],[64,58],[80,82]], A(a))
      + P([[100,82],[116,112],[124,98],[130,132],[118,160],[82,160],[70,132],[78,108],[92,120]], WF);
  },
  fullHouse(a) { // 7색이 터지는 주사위
    let s = '';
    const c7 = COLORS.slice(0, 7);
    for (let k = 0; k < 7; k++) {
      const ang = -90 + k * 360 / 7;
      s += P([pt(46, ang - 13), pt(92, ang), pt(46, ang + 13)], `fill="${c7[k][1]}" stroke="#fff" stroke-width="4"`);
    }
    s += Rot(-8, 100, 100, Rect(66, 66, 68, 68, A(a)) + Circ(84, 84, 7, K) + Circ(116, 84, 7, K) + Circ(100, 100, 7, K) + Circ(84, 116, 7, K) + Circ(116, 116, 7, K));
    return s;
  },
  // ── 칩(빅 스택은 테만 금)
  chip(a, gold) {
    const ring = gold ? GOLD : '#fff';
    let s = Circ(100, 100, 62, `fill="${a}" stroke="${ring}" stroke-width="${gold ? 9 : 5}"`);
    for (let k = 0; k < 6; k++) s += Rot(k * 60 + 30, 100, 100, Rect(92, 40, 16, 16, D(isDark(a) ? `fill="#888"` : a.toLowerCase() === '#ffffff' ? K : WF)));
    s += Circ(100, 100, 36, `fill="${a}" stroke="${ring}" stroke-width="${gold ? 8 : 5}"`);
    return Rot(-10, 100, 100, s);
  },
  // ── 영웅 강화
  attack(a) { // 칼
    return Rot(45, 100, 100, P([[92,150],[92,42],[100,24],[108,42],[108,150]], W) + Rect(74, 146, 52, 12, A(a)) + Rect(94, 158, 12, 22, A(a)) + Circ(100, 184, 8, A(a)));
  },
  critChance(a) { // 조준점
    return P(arcBand(100, 100, 46, 60, 0, 359.9, 60), A(a)) + Rect(94, 24, 12, 36, W) + Rect(94, 140, 12, 36, W)
      + Rect(24, 94, 36, 12, W) + Rect(140, 94, 36, 12, W) + Circ(100, 100, 16, A(a));
  },
  critDamage(a) { // 폭발
    return P(star(100, 100, 66, 38, 9, -80), A(a)) + P(star(100, 100, 36, 18, 9, -60), WF);
  },
  projectile(a) { // 날아가는 카드 셋
    let s = '';
    for (const [y, l] of [[60, 28], [100, 38], [140, 24]]) s += Rect(44 - l, y - 4, l, 8, W);
    for (const [dx, dy, r] of [[0, -38, -12], [18, 0, 0], [0, 38, 12]])
      s += Rot(r + 20, 104 + dx, 100 + dy, Rect(80 + dx, 82 + dy, 48, 34, W) + P([[104 + dx, 89 + dy], [112 + dx, 99 + dy], [104 + dx, 109 + dy], [96 + dx, 99 + dy]], AF(a)));
    return s;
  },
};

// ── 주사위: 정면 실루엣 + 가운데 면 숫자
function diceGeom(faces) {
  if (faces === 6) { const o = [[48,46],[152,40],[160,150],[54,158]]; return { outer:o, center:o, facets:[], num:[100,100], size:64 }; }
  if (faces === 12) {
    const outer = [], center = [], facets = [];
    for (let k = 0; k < 10; k++) outer.push(pt(66, -90 + k * 36));
    for (let k = 0; k < 5; k++) center.push(pt(34, -90 + k * 72));
    for (let k = 0; k < 5; k++) facets.push([center[k], outer[2*k], outer[2*k+1], outer[(2*k+2)%10], center[(k+1)%5]]);
    return { outer, center, facets, num:[100,102], size:30 };
  }
  const outer = [], center = [];
  for (let k = 0; k < 6; k++) outer.push(pt(70, -90 + k * 60));
  for (const d of [-90, 30, 150]) center.push(pt(40, d));
  const facets = [[center[0],outer[5],outer[0]],[center[0],outer[0],outer[1]],[center[1],outer[1],outer[2]],[center[1],outer[2],outer[3]],
    [center[2],outer[3],outer[4]],[center[2],outer[4],outer[5]],[center[0],outer[1],center[1]],[center[1],outer[3],center[2]],[center[2],outer[5],center[0]]];
  return { outer, center, facets, num:[100,103], size:25 };
}
function diceSvg(c, faces) {
  const g = diceGeom(faces), ink = isDark(c) ? '#fff' : INK;
  let s = P(g.outer, `fill="${c}" stroke="#fff" stroke-width="7"`);
  for (const f of g.facets) s += P(f, D(`fill="#000" fill-opacity="0.28" stroke="${INK}" stroke-width="2.5"`));
  if (g.facets.length) s += P(g.center, D(`fill="${c}" stroke="${INK}" stroke-width="2.5"`));
  s += P(g.outer, D(`fill="none" stroke="${INK}" stroke-width="2.5"`));
  s += `<text x="${g.num[0]}" y="${g.num[1]}" text-anchor="middle" dominant-baseline="central" font-family="Black Han Sans" font-size="${g.size}" fill="${ink}">${faces}</text>`;
  return Rot(-8, 100, 100, s);
}

// ── 카드 목록. 강조색은 각 SO의 IconColor.
const SKILLS = [
  ['올인', 'allIn', '#ffd933'], ['백스핀', 'backspin', '#66bfff'], ['체크', 'check', '#bfebff'],
  ['체크메이트', 'checkmate', '#f24d59'], ['잭팟', 'jackpot', '#ffd133'], ['포켓', 'pocket', '#40cc66'],
  ['랙', 'rack', '#ff5966'], ['레이즈', 'raise', '#ff8c26'], ['리롤', 'reroll', '#d9d9eb'],
];
const EXCLUSIVE = [
  ['브레이크 샷', 'breakShot', '#ff8c33'], ['스택 덱', 'stackedDeck', '#d98cff'], ['더블 업', 'doubleUp', '#66e673'],
  ['코인 플립', 'coinFlip', '#3fe0ff'], ['캐시 아웃', 'cashOut', '#bfbfcc'], ['핫 스트릭', 'hotStreak', '#ff7340'],
  ['풀 하우스', 'fullHouse', '#f5056c'],
];
const HERO = [['공격력', 'attack', '#ff5966'], ['치명타 확률', 'critChance', '#ffd933'], ['치명타 피해', 'critDamage', '#ff8c26'], ['투사체', 'projectile', '#3fe0ff']];
const CHIPS = [['공격력', '#e63333'], ['공격 속도', '#33b84d'], ['사거리', '#336bf2'], ['화상', '#e640bf'], ['얼림', '#66ccf5'], ['둔화', '#f5d933'], ['취약', '#ffffff']];
