// 내보낼 아이콘 목록 — [Assets/_Project 기준 경로(확장자 없이), 카드 종류 색, 기호 SVG]. 새 카드 아이콘은 여기 더한다.
function iconList() {
  const L = [];
  const SK = KIND.skill, EX = KIND.exclusive, HE = KIND.hero, CH = KIND.chip, DI = KIND.dice;
  const file = f => ({ raise: 'Raise', reroll: 'Reroll' })[f] || 'Icon_Skill_' + f[0].toUpperCase() + f.slice(1);
  for (const [t, f, c] of SKILLS) L.push(['Skill/Icons/' + file(f), SK, ICONS[f](c)]);
  for (const [t, f, c] of EXCLUSIVE) L.push(['Skill/Icons/' + file(f), EX, ICONS[f](c)]);
  const EN = ['Red', 'Blue', 'Cyan', 'Green', 'Magenta', 'Yellow', 'White', 'Clear'];
  for (let i = 0; i < 7; i++) L.push(['Skill/Icons/Paint_' + EN[i], SK, ICONS.paint(COLORS[i][1])]);
  const CHIPN = ['Damage', 'AttackSpeed', 'Range', 'Burn', 'Freeze', 'Slow', 'Vulnerable'];
  for (let i = 0; i < 7; i++) {
    L.push(['Chip/Icons/Icon_Chip_' + CHIPN[i], CH, ICONS.chip(CHIPS[i][1])]);
    L.push(['Chip/Icons/Icon_BigStack_' + CHIPN[i], EX, ICONS.chip(CHIPS[i][1], true)]);
  }
  const HEN = ['Attack', 'CritChance', 'CritDamage', 'Projectile'];
  for (let i = 0; i < 4; i++) L.push(['HeroUpgrade/Icons/Icon_Hero_' + HEN[i], HE, ICONS[HERO[i][1]](HERO[i][2])]);
  for (let i = 0; i < 8; i++) for (const f of [6, 12, 20]) L.push(['Dice/Icon/Card_' + EN[i] + '_D' + f, DI, diceSvg(COLORS[i][1], f)]);
  return L;
}
if (typeof module !== 'undefined') module.exports = { iconList };
