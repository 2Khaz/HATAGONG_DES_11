# INGAME Phase 배경 및 GamePanelCase 계층 적용 보고

## 이번 단계 핵심 요약

- 최종 판정: 정적 계층·참조·컴파일 검증 PASS, Play Mode 렌더링·입력 육안 검증은 미수행.
- Branch: `OutGame_Contec`
- HEAD: `b3848fe10603d7a027a722d3611d4589f26fc88d`
- 작업 전 Git 상태: 기존 Phase 1 수정 6개와 보고서 1개, 사용자 추가 `Img_GamePanelCase.png/.meta` 2개가 미커밋 상태. Staged 변경 없음.
- 작업 후 Git 상태: 기존 변경을 보존하고 INGAME Scene, `Phase3TangramManager.cs`, 본 보고서를 추가 변경.
- 수정 파일: `Assets/Scenes/INGAME.unity`, `Assets/Scripts/Phase3Tangram/Phase3TangramManager.cs`, 본 보고서.
- 예상 밖 변경: 없음.
- Staged/Commit/Push: 모두 0.
- INGAME Scene 작업 전 SHA: `E304F937DE8BA6E38977A20385B5AA79FE5025FC8A728F43B685A0D1D7016438`.
- INGAME Scene 작업 후 SHA: `D5F678C497BE265B7CE633A276AABC0F5D3C45D0505101A975A1A3E9B1DDD172`.

## 실제 리소스

### Phase 2 시작 레이어

- GameObject: `DustLayer`.
- Hierarchy: `Canvas/Game_UI_General/Middle_GamePanel/Phase2Root/DustLayer`.
- Sprite 경로: `Assets/Resources/Ingame/Floor/Dust.png`.
- GUID: `e2aaa5132839d874791b8c2048c0069c`.
- RectTransform: Phase2Root 전체 Stretch, Anchor `(0,0)~(1,1)`, Pivot `(0.5,0.5)`, offset/size delta 0.
- Image 설정: `RawImage`, 흰색/Alpha 1, `raycastTarget=false`, Maskable On, Preserve Aspect 해당 없음.
- Material: `Phase2BlackCoverMask.mat` 기반 런타임 Material.
- 동적 의존성: 시작 화면은 단순 Sprite Image가 아니다. `Phase2MaskPresenter`가 Dust Sprite Texture와 1024×1024 Mask RenderTexture를 Material에 결합하며, 진행도와 CompletionFill에 따라 먼지 Alpha가 변한다.
- Phase 1 적용 방식: 동적 DustLayer GameObject나 Material은 공유하지 않고 동일한 Dust Sprite를 참조하는 별도 정적 Image를 사용했다.

### Phase 2 종료 레이어

- GameObject: `PaintLayer`.
- Hierarchy: `Canvas/Game_UI_General/Middle_GamePanel/Phase2Root/PaintLayer`.
- Sprite 경로: `Assets/Resources/Ingame/Floor/Paint.png`.
- GUID: `3396870eeed76a04292e7a04f910fcff`.
- RectTransform: Phase2Root 전체 Stretch, Anchor `(0,0)~(1,1)`, Pivot `(0.5,0.5)`, offset/size delta 0.
- Image 설정: 단순 `Image`, 흰색/Alpha 1, Simple, Preserve Aspect Off, `raycastTarget=false`, Maskable On, Material 없음.
- 동적 의존성: PaintLayer 자체는 정적이다. Phase 2 종료 시 DustLayer의 동적 Cover가 사라져 이 Image가 드러난다.
- Phase 3 적용 방식: 동적 Mask 결과를 공유하지 않고 Paint Sprite만 별도 Image로 재사용했다.

### Img_GamePanelCase

- 실제 경로: `Assets/Resources/Ingame/UI/Img_GamePanelCase.png`.
- 원본 크기: 1360×1378, GamePanel/Middle_GamePanel과 동일.
- GUID: `3e6bf4fa27b5df14cbb760bb05917a97`.
- Import 상태: Sprite (2D and UI), Single, Full Rect, Pivot Center, PPU 100, Mipmap Off, Bilinear, Clamp, Alpha Transparency On, Max Size 2048.
- Scene 연결: `Canvas/Game_UI_General/Middle_GamePanel/Img_GamePanelCase`의 Simple Image. Middle_GamePanel 전체 Stretch.
- Color/Material: 흰색/Alpha 1, Material 없음, Preserve Aspect Off, Maskable On.
- Raycast Target: Off.
- PNG SHA: `52E4592339F3B684356155074F160CFFDA001BD565D287E33456AAE22DBBE2C8`.
- PNG와 `.meta`는 사용자가 추가한 상태 그대로이며 수정하지 않았다.

## 최종 렌더링 계층

모든 UI는 기존 단일 Canvas를 사용한다. Canvas는 Scene 기본값에서 `overrideSorting=false`, Sorting Layer Default, Sorting Order 0이며 별도 Canvas를 추가하지 않았다. Phase 3 동안 기존 Manager가 Canvas를 Screen Space Camera로 전환하지만 같은 Canvas 내부 순서는 유지된다.

뒤쪽부터 앞쪽 순서:

1. `Middle_GamePanel/Game_Panel` — 공통 기본 패널.
2. Phase별 배경 — `Phase1_FieldRoot/Phase1_Background`, 기존 Phase2 `PaintLayer/DustLayer`, 또는 `Middle_GamePanel/Phase3_Background`.
3. Phase별 게임 콘텐츠 — Phase1 TileContainer, Phase2 Root 표현, Phase3 Root 및 World Mesh 조각.
4. `Middle_GamePanel/Img_GamePanelCase`.
5. `Middle_GameDeck/Deck_Panel` 및 기존 뒤쪽 형제보다 나중에 렌더되는 UI/전환 Overlay.

Sibling 근거:

- `Middle_GamePanel`: Game_Panel(0), Phase1Root(1), Phase2Root(2), Phase3Background(3), Phase3Root(4), Img_GamePanelCase(5).
- `Phase1_FieldRoot`: Phase1Background(0), TileContainer(1), EffectRoot(2), ScorePopupRoot(3).
- `Game_UI_General`: Middle_GamePanel(2), Middle_GameDeck(3). 같은 Canvas이므로 DeckPanel이 Case보다 앞이다.

필수 판정:

- 게임 콘텐츠 < Img_GamePanelCase: PASS.
- Img_GamePanelCase < DeckPanel: PASS.
- 별도 Canvas 충돌: 없음. 관련 계층에 중첩 Canvas/overrideSorting을 추가하지 않았다.
- 런타임 Sibling 변경 충돌: 없음. Phase1 효과의 `SetAsLastSibling()`은 EffectRoot 내부에만 적용된다. Phase2 Setup의 정렬 코드는 실행하지 않았으며 Phase2Root index 2 계약과 현재 Scene이 일치한다.

## Phase 1 배경

- 사용 Sprite: `Dust.png`, GUID `e2aaa5132839d874791b8c2048c0069c`.
- 별도 Image 여부: Yes, `Phase1_FieldRoot/Phase1_Background`.
- RectTransform: Phase1 1250×1250 Root 전체 Stretch, Anchor `(0,0)~(1,1)`, Pivot `(0.5,0.5)`, offset 0.
- Block보다 아래: PASS. Background가 Phase1Root 첫 번째 자식이고 TileContainer가 두 번째다.
- GamePanelCase보다 아래: PASS. Phase1Root 전체가 Case보다 앞선 Sibling이다.
- 입력 방해: 정적 PASS (`raycastTarget=false`, CanvasGroup 없음).
- Phase 전환 활성 상태: Phase1Root의 자식이므로 Phase1 Activate/Deactivate와 함께 On/Off된다. Phase2·3에서는 Phase1Root가 비활성화되어 중복되지 않는다.

## Phase 3 배경 검토

- Phase 2 종료 레이어 재사용 가능 여부: 가능.
- 판단 근거: 종료 시 보이는 `PaintLayer`는 동적 RenderTexture가 아닌 단순 Paint Sprite Image이며 Material/Mask 의존성이 없다. 동일 GamePanel 영역에 Preserve Aspect Off로 배치할 수 있다.
- 실제 적용 여부: 적용.
- 사용 Sprite: `Paint.png`, GUID `3396870eeed76a04292e7a04f910fcff`.
- Hierarchy: `Canvas/Game_UI_General/Middle_GamePanel/Phase3_Background`.
- RectTransform: Phase3Root와 동일한 Anchor `(0.5,0.5)`, Position `(-7,-1)`, Size `1250×1250`, Pivot `(0.5,0.5)`.
- Board·Puzzle보다 아래: PASS. Phase3Background Sibling 3, Phase3Root Sibling 4.
- 활성 제어: `Phase3TangramManager.phaseBackground` 직렬화 참조. Prepare 시 Off, Runtime visibility Off/On과 함께 Deactivate/Activate된다.
- Phase3Root 자식으로 넣지 않은 이유: 기존 `EnsureBuilt()`가 런타임 구성 전 `transform.childCount == 0`을 강제하므로, Root 자식 추가는 Production Prepare를 실패시킨다.
- Shine 충돌: 정적 구조상 배경은 Phase3Root보다 아래이며 Completion Image/Shine은 Phase3Root 런타임 Field에 생성되므로 배경보다 앞이다. Shine 로직·Material은 수정하지 않았다.
- 입력 충돌: `raycastTarget=false`, Material 없음.
- 미적·가독성 문제: Paint는 Phase 2 종료 화면과 동일한 정적 바닥 Sprite이고 Alpha 1이다. 실제 퍼즐 색상 대비와 Shine 시각 품질은 Play Mode 육안 검증이 필요하다.

## 입력 검증

- Phase 1 Block: 정적 PASS. Background는 TileContainer 아래이며 Raycast 대상이 아니다.
- Phase 2 Drag: Production 입력·Mask 코드 변경 없음. Case는 Raycast 대상이 아니다.
- Phase 3 Puzzle: Production Drag/회전/Snap 코드는 변경하지 않았다. Phase3Background와 Case 모두 Raycast 대상이 아니다.
- DeckPanel: 같은 Canvas에서 Case의 부모 계층보다 나중에 렌더되며 기존 버튼·페이지 코드는 변경하지 않았다.
- 장식 Image Raycast: Phase1Background, Phase3Background, Img_GamePanelCase 모두 Off.
- GraphicRaycaster 최상위: 정적 구성상 신규 Image 3개는 Raycast 후보에서 제외된다. 실제 Pointer Raycast 결과는 Play Mode 미검증.

## 회귀검증

- Phase 전환: 정적 PASS. Phase1 Background는 Phase1Root 생명주기를 따르고 Phase3 Background는 Manager의 기존 Runtime visibility와 함께 제어된다.
- Phase 2 레이어: 코드·참조·Hierarchy 변경 없음.
- Phase 3 Shine: 코드·Material 변경 없음. 배경은 Shine보다 뒤.
- Deck 페이지: 코드·Hierarchy 변경 없음. DeckPanel은 Case보다 앞.
- Runtime Compile: warning 0, error 0.
- Editor Compile: warning 0, error 0.
- Console: 현재 열린 Unity Editor가 외부 변경 이후 Asset Refresh를 수행한 기록이 없어 최신 Scene 기준 Console 검증은 미수행.
- Missing Script: Scene YAML의 `m_Script: {fileID: 0}` 0개.
- Missing Reference: 신규 Sprite GUID 3개와 Phase3 Manager Image 참조의 실제 대상 존재 확인. Scene 전체 일반 Missing Reference 검사는 미수행.
- Scene 문서 무결성: YAML document 282개, fileID 중복 0.
- Play Mode: 미수행.

## 미검증 항목

- 열린 Unity Editor가 외부 Scene/C# 변경을 아직 Refresh하지 않은 상태라 Scene을 강제로 다시 열거나 저장하지 않았다.
- 안전한 자동 Phase 순환 경로가 없어 Phase1/2/3 Play Mode 육안 렌더링, 실제 GraphicRaycaster top target, Phase3 Paint 배경의 최종 가독성 및 Shine 화면은 PASS로 기록하지 않았다.
- Unity Scene 저장, Setup 메뉴, Prefab Apply는 실행하지 않았다.

## 최종 결론

- Phase 1에 Phase 2 시작 이미지 원본 배경이 적용됐는가: Yes. Dust Sprite 별도 Image 적용.
- Phase 1 블록보다 뒤에 표시되는가: Yes. Phase1Root 첫 Sibling.
- Img_GamePanelCase가 모든 게임 콘텐츠 위에 표시되는가: Yes, 정적 Canvas/Phase3 Screen Space Camera 구조 기준.
- DeckPanel이 Img_GamePanelCase 위에 표시되는가: Yes. Middle_GameDeck이 Middle_GamePanel 다음 Sibling.
- 장식 이미지가 입력을 막지 않는가: Yes, 신규 세 Image 모두 `raycastTarget=false`.
- Phase 3에 Phase 2 종료 이미지를 안전하게 사용할 수 있는가: Yes. 정적 Paint Sprite만 독립 Image로 재사용.
- 적용한 경우 Phase 3 입력과 Shine이 정상인가: 코드·계층 충돌은 없으나 실제 Play Mode는 미검증.
- 관련 없는 Scene·코드·리소스 변경이 없는가: 최종 Git diff 기준 Yes.
- PNG, `.meta`, Material, Shader, Phase1 HP/Grade, Phase2 도포, Phase3 퍼즐 메커니즘, Deck 기능, Timer, Score, OUTGAME, ProjectSettings는 수정하지 않았다.
- Git add, commit, push는 수행하지 않았다.
