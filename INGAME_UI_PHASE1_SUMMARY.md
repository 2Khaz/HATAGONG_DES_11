# HATAGONG_DES_11 인게임 UI 및 Phase 1 요약 인수인계서

- 작성일: 2026-07-12
- Unity: 6000.3.19f1
- Scene: `Assets/Scenes/INGAME.unity`
- 상태: **UI 및 Phase 1 코어 프로토타입 완료 / 리소스·실기기·후속 페이즈 미완료**

---

## 1. 현재 완료된 것

- 1440×2560 Screen Space Overlay UI
- 5×5, 1250×1250 Phase 1 Field
- 12종 Tile Bag과 난이도별 Shuffle Bag
- 랜덤 백트래킹 배치와 완전 충전 검증
- 타격·HP·Damage State·파괴·클리어
- 단일 활성 포인터와 0.04초 debounce
- 점수: 타격 +10, 파괴 +50, 클리어 +300
- Layout Hash와 Variant Hash
- Beige/Brown/Gray/Marble Grade
- 모든 Final HP 최소 2
- Grade별 fallback Color
- 사운드 null-safe 구조와 모바일 진동 조건
- 난이도 별 3개 위치 고정
- Prefab·Config·Scene·Inspector 연결
- 120보드, 900타일 자동 검증 통과

---

## 2. 핵심 UI 계약

```text
Canvas
├─ Game_UI_General
│  ├─ Top_HUD
│  ├─ Middle_GamePanel
│  │  ├─ Game_Panel
│  │  └─ Phase1_FieldRoot
│  │     ├─ Phase1_TileContainer
│  │     ├─ Phase1_EffectRoot
│  │     └─ Phase1_ScorePopupRoot
│  ├─ Middle_GameDeck
│  └─ Item_Button01~03
├─ Game_UI_Settings
└─ EventSystem
```

- Canvas: Scale With Screen Size, 1440×2560, Match 0.5
- Middle_GamePanel: Position (40,-660), Size 1360×1378
- Phase1_FieldRoot: Position (-7,-1), Size 1250×1250
- Cell: 250×250
- 기존 계층·이름·RectTransform은 후속 작업에서도 유지

---

## 3. Phase 1 규칙

### Shape와 Base HP

| Shape | Easy | Normal | Hard | Role |
|---|---:|---:|---:|---|
| 1×1 | 금지(0) | 6 | 7 | Core |
| 1×2 / 2×1 | 2 | 2 | 2 | Pop |
| 1×3 / 3×1 | 4 | 5 | 6 | Standard |
| 2×2 | 7 | 8 | 9 | Standard |
| 2×3 / 3×2 | 10 | 10 | 10 | Anchor |
| 3×3 | 13 | 14 | 금지(0) | Anchor |

### Tile Bag

| 난이도 | Bag | 1×1 | 1×2 | 1×3 | 2×2 | 2×3 | 3×3 | 타일 수 | 면적 | Base HP |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Easy | EASY_A | 0 | 0 | 2 | 1 | 1 | 1 | 5 | 25 | 38 |
| Easy | EASY_B | 0 | 1 | 0 | 2 | 1 | 1 | 5 | 25 | 39 |
| Easy | EASY_C | 0 | 1 | 1 | 2 | 2 | 0 | 6 | 25 | 40 |
| Easy | EASY_D | 0 | 2 | 0 | 3 | 0 | 1 | 6 | 25 | 38 |
| Normal | NORMAL_A | 2 | 1 | 1 | 3 | 1 | 0 | 8 | 25 | 53 |
| Normal | NORMAL_B | 2 | 0 | 1 | 2 | 2 | 0 | 7 | 25 | 53 |
| Normal | NORMAL_C | 2 | 1 | 0 | 3 | 0 | 1 | 7 | 25 | 52 |
| Normal | NORMAL_D | 2 | 2 | 1 | 1 | 2 | 0 | 8 | 25 | 49 |
| Hard | HARD_A | 3 | 3 | 2 | 1 | 1 | 0 | 10 | 25 | 58 |
| Hard | HARD_B | 4 | 1 | 1 | 1 | 2 | 0 | 9 | 25 | 65 |
| Hard | HARD_C | 3 | 2 | 4 | 0 | 1 | 0 | 10 | 25 | 59 |
| Hard | HARD_D | 3 | 2 | 0 | 3 | 1 | 0 | 9 | 25 | 62 |

### 배치 검증

- 25셀 완전 충전
- 필드 바깥/중첩/빈칸 금지
- Core끼리 변 인접 금지
- 같은 Shape 3개 이상 연결 금지
- largest-first 랜덤 백트래킹

---

## 4. Grade

| Grade | Modifier | Easy Weight | Normal Weight | Hard Weight | Easy Max | Normal Max | Hard Max | Fallback RGBA |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Beige | -1 | 40 | 20 | 10 | 무제한 | 무제한 | 무제한 | (0.86, 0.76, 0.59, 1) |
| Brown | 0 | 35 | 40 | 30 | 무제한 | 무제한 | 무제한 | (0.48, 0.29, 0.16, 1) |
| Gray | +1 | 20 | 30 | 40 | 무제한 | 무제한 | 무제한 | (0.48, 0.51, 0.54, 1) |
| Marble | +2 | 5 | 10 | 20 | 1 | 1 | 2 | (0.88, 0.90, 0.92, 1) |

```text
허용 조건: Base HP + Grade Modifier >= 2
```

Grade를 먼저 잘못 고른 뒤 HP를 2로 Clamp하지 않는다.

현재 Sprite는 모두 null이며 fallback Color로 표시한다. 전용 리소스가 들어오면 Sprite는 `Color.white`로 표시한다.

---

## 5. Prefab 및 Controller

`Phase1_TilePrefab`:

- 루트 투명 Image: 입력
- `Tile_Visual`: 표시 전용, Raycast false
- 세로 타일은 `Tile_Visual`만 90도 회전

`Phase1_FieldRoot`:

- Phase1BoardController
- Phase1InputController
- Phase1ScoreController
- Phase1HUDPresenter
- Phase1FeedbackController
- AudioSource

---

## 6. 검증 결과

- 12 Bag × 10 Seed = 120보드
- 총 900타일
- Final HP 2 미만: 0
- HP 계산 불일치: 0
- Fixed Seed Layout Hash 재현 성공
- Fixed Seed Variant Hash 재현 성공
- Normal 샘플: Base 53 → Final 55, 55타격, 점수 1200 일치
- 컴파일 오류 0
- Grade fallback Color 4종 Play Mode 확인
- 기존 UI RectTransform 변경 없음
- 저장 후 `isDirty = false`

---

## 7. 현재 리소스 상태

- Tile Sprite: 미연결
- AudioClip: 미연결
- Grade별 fallback Color만 사용
- Filled Star: `Img_icon_star2`
- Empty Star: `Img_icon_star1`
- `Assets/resource`와 대응 `.meta`는 커밋에 반드시 포함

---

## 8. 즉시 수정이 필요한 Git 상태

### 필수

1. `SampleScene.unity.meta -> Assets/Scripts/Phase1/Data.meta` rename/GUID 재사용 정리
2. `Data.meta` 새 GUID 생성
3. `INGAME` Build Settings 등록 확인
4. Unity `.meta`, `Assets/resource`, 필요한 TMP 에셋 포함
5. 검토용 TXT 파일 커밋 제외

### 검토

- `Mobile_RPAsset.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `.vscode/`
- `Test.cs`

---

## 9. 알려진 기술 부채

- BoardState의 Sprite 이름/fallback 로그가 실제 TileView 선택 결과와 아직 동기화되지 않음
- 구형 `Phase1TileVisualDefinition` 경로가 현재 미사용
- fallback Bag 루프의 외부 종료 흐름 정리 필요
- `Generate(bool regenerate)` 인자 미사용
- Fixed Seed와 최근 Hash 차단을 같이 사용할 때 Controller 반복 생성 결과가 달라질 수 있음
- 실제 Pointer/모바일/Audio/Build Settings는 수동 확인 필요

---

## 10. 앞으로의 작업 순서

1. Git/Meta/Build Settings 정리 및 현재 상태 커밋
2. 실제 마우스·터치·멀티터치 UX 검증
3. Grade·비율·Damage State별 Sprite 연결
4. AudioClip과 실기기 진동 검증
5. 90초 Timer 및 Time_Value 연결
6. Phase Flow와 Phase 2/3
7. Request, Deck, Item
8. Settings 및 저장
9. 점수 팝업, 파티클, 전환 연출
10. 모바일 빌드와 Safe Area·성능 QA

---

## 11. 미구현 범위

- 실제 Timer
- Phase 2/3
- Request
- Deck
- Item
- Settings
- 점수 팝업
- 파티클
- 실제 Sprite/Audio
- 정밀 햅틱
- 한국어 최종 폰트/문구

---

## 12. 후속 작업 금지 원칙

- 기존 UI 계층과 RectTransform 임의 변경 금지
- Tile 루트 회전 금지
- Final HP 2 미만 생성 금지
- HP Clamp로 Grade 규칙 은폐 금지
- Layout Hash에 Grade 추가 금지
- 빈 별 GameObject 비활성화 금지
- Phase 1 코드에 Timer/Phase 2/Item을 임시 혼합하지 않음

상세 규칙, 전체 RectTransform, 코드 파일별 역할, 원문 보고서는 `INGAME_UI_PHASE1_FULL_REPORT.md`를 참조한다.
