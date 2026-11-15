# 규칙 책상 화면 그림

당구대 옆 규칙 책상(`Assets/_Project/RulesTable/`) 윗면에 깔린 화면(`UI_RulesScreen` 프리팹 — 월드 공간 캔버스)의 그림 스프라이트를 뽑는다.

- 화면은 정식 UI다(2026-11-13): 탭(합성 · 공격 · 칩)을 누르거나 저절로 넘어갈 때 장이 옆으로 밀린다. 글과 색은 SO에서 읽는다
  — 공격 이름·주사위 색은 `SO_DiceConfig.Types`, 칩 이름·색은 칩 SO, 합성 예시 결과는 얹기 규칙(`DiceRules.Stack`). 그래서 수치·이름을 바꿔도 그림을 다시 뽑지 않는다.
- 그림은 **흰색**으로 뽑고 게임이 SO 색으로 물들인다. 공격 그림은 두 장이 한 벌: `Attack_<색>_Shape`(물들일 부분) + `Attack_<색>_Mark`(공격선·적·노랑 강조, 색 그대로).
- 빛 번짐(glow)은 굽지 않는다 — 게임 블룸이 한다(화면 밝기는 `SO_RulesTableConfig.ScreenBrightness`, 1을 넘겨야 빛난다).
- `MonitorTall.dc.html` — 처음 시안(claude.ai 디자인 캔버스 "규칙 책상 시안"의 G2 판). 그림 모양의 출처.

## 다시 뽑기

```sh
python Tools/RulesScreen/sprites.py      # 필요: Chrome, Pillow
```

`Assets/_Project/RulesTable/Sprites/`에 PNG를 덮어쓴다(GUID 유지). 새 그림이면 스프라이트 메타(밉맵·aniso — 비스듬히 보는 화면, 판은 9-slice 테두리 40)를 만든다.
새 공격 그림을 더하면 `SO_RulesTableConfig.Attacks`에 색과 함께 연결한다.
