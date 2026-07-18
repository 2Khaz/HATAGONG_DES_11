# OUTGAME 의뢰 보드 UI 재배치 및 데이터 표시 보고서

## 이번 단계 핵심 요약

- 최종 판정: `STATIC READY / OUTGAME REQUEST BOARD UI PLAY MODE VALIDATION REQUIRED`
- Branch: `OutGame_Contec`
- HEAD: `b577eea41d68c996eaeaeb8e501cdffd2eb930cd`
- Unity 실행: 안 함. `InitializeOnLoadMethod`, `EditorApplication.delayCall`, `EditorSceneManager.SaveScene`, `AssetDatabase.SaveAssets`를 사용하는 기존 Editor 코드가 있어 열린 dirty Scene의 자동 저장 위험을 피했다.
- Play Mode 실행: 안 함. 따라서 화면 표시, Swipe, 버튼 클릭, Scene Load는 PASS로 기록하지 않는다.
- 기존 Staged 보존: 작업 전·후 Staged 파일 0개이며 변경하지 않았다.
- 신규 파일: 본 보고서 1개. 의뢰 PNG와 meta는 작업 전에 이미 존재한 untracked 파일이며 이번 작업에서 생성하지 않았다.
- 이번 작업 수정 파일: `OutgameRequestCard.prefab`, `OutgameRequestCardView.cs`, `OutgameRequestCarouselController.cs`, `OutgameRequestStage4Validation.cs`, `OutgameRequestStage6Validation.cs`, 본 보고서.
- 삭제 파일: 0개.
- 예상 밖 변경: 0개. 기존 INGAME/OUTGAME Scene 및 다른 dirty 파일은 보존했다.
- Commit/Push: 수행하지 않음.

## 레퍼런스 반영

- 세로형 보드는 `Img_questui` 원본 비율 1015:1512를 사용한다.
- Header, 좌상단 Portrait, 우측 의뢰주/제목/난이도, 중단 설명, 하단 효과 3칸, 최하단 수행 버튼 순으로 정보 계층을 재배치했다.
- `PortraitKey`와 `EffectIconKey`를 명시적 Sprite 표로 해석한다. Request 순번 modulo 연산은 없다.
- 효과가 0~2개여도 슬롯 오브젝트 3개를 유지하고 빈 칸에 `Img_icon_quest_null`을 표시한다.
- 수행 버튼 이미지는 `Img_button_request`, 버튼 TMP는 별도 `의뢰 수락`이다. 기존 Button과 런타임 Listener를 재사용한다.
- 의뢰 종류, 의뢰 제목, 특수효과 이름은 `Jua-Regular SDF`와 Bold를 사용하며 효과명 크기는 의뢰 제목의 20pt 기준과 일치시켰다.
- 기존 별 Sprite 및 난이도별 활성 별 수 계산은 변경하지 않았다.
- Request 카드에는 별도 공용 시계 GameObject가 없다. 기존 `Img_icon_time` 또는 시간 계산 로직을 교체하지 않았으며, 현재 `EFFECT_RUSH`가 명시적으로 가리키는 신규 효과 아이콘만 해당 슬롯에 표시한다.

## 리소스

| 용도 | Asset | 실제 경로 | GUID | 원본 크기 | Aspect | Git 추적 | Prefab 참조 |
|---|---|---|---|---:|---:|---|---|
| 보드 | Img_questui | `Assets/Resources/Img_questui.png` | `8f7e9dc1f7d0fca46acba0c7fe03f259` | 1015×1512 | 0.671296 | PNG/meta 추적 | 직접 Sprite |
| 버튼 | Img_button_request | `Assets/Resources/Outgame/Request/Img_button_request.png` | `7f59b727a9e99824c8806ee1154642a8` | 627×213 | 2.943662 | PNG/meta untracked | 직접 Texture2D, 런타임 Sprite 생성 |
| 효과 1 | Img_icon_quest1 | `Assets/Resources/Outgame/Request/Img_icon_quest1.png` | `6ca4048862aefc448bbe8559d9570bf0` | 308×312 | 0.987179 | PNG/meta untracked | 직접 Sprite binding |
| 효과 2 | Img_icon_quest2 | `Assets/Resources/Outgame/Request/Img_icon_quest2.png` | `eaf80c06810a2b1438d64972199b297d` | 308×312 | 0.987179 | PNG/meta untracked | 직접 Sprite binding |
| 효과 3 | Img_icon_quest3 | `Assets/Resources/Outgame/Request/Img_icon_quest3.png` | `cea2626654837f848bc4a3f9bb0e78e5` | 308×312 | 0.987179 | PNG/meta untracked | 직접 Sprite binding |
| 효과 4 | Img_icon_quest4 | `Assets/Resources/Outgame/Request/Img_icon_quest4.png` | `b0a11e209ad28064db8feea30a76640e` | 308×312 | 0.987179 | PNG/meta untracked | 직접 Sprite binding |
| 효과 5 | Img_icon_quest5 | `Assets/Resources/Outgame/Request/Img_icon_quest5.png` | `3b5324df35eef384abf1f5d9c9b5b319` | 308×312 | 0.987179 | PNG/meta untracked | 직접 Sprite binding |
| 효과 6 | Img_icon_quest6 | `Assets/Resources/Outgame/Request/Img_icon_quest6.png` | `3fa5605cf3cec1c448349fb725c0da58` | 308×312 | 0.987179 | PNG/meta untracked | 직접 Sprite binding |
| 효과 7 | Img_icon_quest7 | `Assets/Resources/Outgame/Request/Img_icon_quest7.png` | `5deded05cba6d3b4e943cd5073bbddec` | 308×312 | 0.987179 | PNG/meta untracked | 직접 Sprite binding |
| 빈 효과 | Img_icon_quest_null | `Assets/Resources/Outgame/Request/Img_icon_quest_null.png` | `133d1da0916c0c74d9d7e12c0e7f2133` | 308×312 | 0.987179 | PNG/meta untracked | 빈 슬롯 3개 기본 Sprite |
| 초상 1 | Img_portrait_quest1 | `Assets/Resources/Outgame/Request/Img_portrait_quest1.png` | `007e7209dc28417449bfeed4fd52ad9c` | 345×388 | 0.889175 | PNG/meta untracked | `portrait_client_01` |
| 초상 2 | Img_portrait_quest2 | `Assets/Resources/Outgame/Request/Img_portrait_quest2.png` | `f8649ce177ce1564aa1dad7c0585a741` | 345×388 | 0.889175 | PNG/meta untracked | `portrait_client_02` |
| 초상 3 | Img_portrait_quest3 | `Assets/Resources/Outgame/Request/Img_portrait_quest3.png` | `00a5485920c13b74bbedf5497d2efb1f` | 345×388 | 0.889175 | PNG/meta untracked | `portrait_client_03` |
| 초상 4 | Img_portrait_quest4 | `Assets/Resources/Outgame/Request/Img_portrait_quest4.png` | `31d69c3cb2a5c1049b34b5b98673e211` | 345×388 | 0.889175 | PNG/meta untracked | `portrait_client_04` |
| 초상 5 | Img_portrait_quest5 | `Assets/Resources/Outgame/Request/Img_portrait_quest5.png` | `42f236caf536bdf42b315ff1b7aefd20` | 345×388 | 0.889175 | PNG/meta untracked | `portrait_client_05` |
| 초상 6 | Img_portrait_quest6 | `Assets/Resources/Outgame/Request/Img_portrait_quest6.png` | `2eafc4bcddfdeaa47876476876f76843` | 345×388 | 0.889175 | PNG/meta untracked | `portrait_client_06` |
| 초상 7 | Img_portrait_quest7 | `Assets/Resources/Outgame/Request/Img_portrait_quest7.png` | `dcdda3c60c26d2e49b3d9564274c0f0a` | 345×388 | 0.889175 | PNG/meta untracked | `portrait_client_07` |
| 활성 별 | Img_icon_star2 | `Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star2.png` | `b0ab8e005eea08d47af5a5ece7efd4d5` | 기존 | 기존 | 기존 추적 | 기존 참조 유지 |
| 비활성 별 | Img_icon_star1 | `Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star1.png` | `dc4a0032fea6bdc40b69f9980876e7b2` | 기존 | 기존 | 기존 추적 | 기존 참조 유지 |
| 공용 시계 | Img_icon_time | `Assets/Resources/Ingame/ICON/Img_icon_time.png` | `b96315092749e8943ac555ee46891129` | 기존 | 기존 | 기존 추적 | Request 카드 신규 참조 없음, 변경 없음 |

17개 요청 UI PNG는 실제 PNG signature이며 LFS pointer가 아니다. GUID 중복은 0개였다. `Img_button_request`만 기존 meta가 `textureType: 0`, `spriteMode: 0`이고 meta 수정이 금지되어 있어 Texture2D를 직렬화하고 `Sprite.Create`로 버튼 표시용 Sprite를 1회 생성·정리한다. 나머지 보드/초상/효과 리소스는 Sprite Single Import 상태이며 alpha transparency가 활성화되어 있다. PNG와 meta는 수정하지 않았다.

## Request 데이터 연결

- 별도 `PortraitIndex`를 추가하지 않고 이미 존재하는 명시적 `PortraitKey`를 재사용했다.
- `portrait_client_01`~`07`을 초상 1~7에 직접 매핑한다.
- `request_effect_focus/rush/bonus`는 현재 CSV 순서대로 효과 1/2/3에 직접 매핑한다.
- 향후 명시적 키 `request_effect_01`~`07`도 효과 1~7에 직접 연결했다. RequestId나 페이지 인덱스 계산은 없다.
- `REQ-N-EASY-001`: portrait 1, 효과 0개, null/null/null.
- `REQ-N-NORMAL-002`: portrait 2, focus/null/null.
- `REQ-S-HARD-003`: portrait 3, focus/rush/null.
- `REQ-S-NORMAL-004`: portrait 4, focus/rush/bonus.
- CSV 데이터 순서를 유지하고 UI에서 재정렬하지 않는다. 현재 CSV와 C# 데이터 모델은 수정하지 않았다.

## UI 계층

- `OutgameRequestCard`가 RequestBoardRoot 역할을 한다.
- `ClipboardBackground`: `Img_questui`, 전체 Stretch, preserveAspect, raycast false.
- `RequestTypeLabel`: HeaderText.
- `PortraitPlaceholder`: PortraitImage. 명시적 Portrait Sprite, preserveAspect, raycast false이며 신규 PNG 없이 검정 UI Outline을 적용한다.
- `RequesterNameLabel`: 기존 단일 TMP에 `의뢰주: {이름}` 정책 유지.
- `TitleLabel`, `DifficultyLabel`, `DifficultyStars`, `DescriptionTitleLabel`, `DescriptionLabel`을 재사용했다.
- `EffectSlots/EffectSlot1~3`: 오브젝트 수를 3개로 고정했다. 각 `IconPlaceholder`는 항상 활성이고 비어 있으면 null Sprite다.
- `PerformButton`: 기존 Button 및 target graphic 재사용.
- `PerformButton/PerformButtonLabel`: 별도 TMP `의뢰 수락`, raycast false.
- 기존 계층을 삭제하거나 Request 페이지 생성 구조를 재작성하지 않았다.

## Anchor·반응형 레이아웃

- Board Root: anchor `(0.5,0.5)`, pivot `(0.5,0.5)`, rotation 0, scale 1.
- Carousel 보드 Fit: `width <= viewportWidth×0.88`, `height <= viewportHeight×0.80`, 비율 `1512/1015` 유지.
- Board Y offset: viewport height의 `+0.045`.
- Header: `(0.12,0.825)`~`(0.88,0.915)`. 상단 클립과 간격을 확보하도록 기존 위치에서 보드 높이의 4%만큼 아래로 이동했다.
- Portrait: `(0.075,0.575)`~`(0.365,0.835)`.
- Requester: `(0.39,0.755)`~`(0.92,0.83)`.
- Title: `(0.39,0.655)`~`(0.92,0.755)`.
- Difficulty label: `(0.39,0.585)`~`(0.56,0.655)`.
- Stars root: `(0.56,0.565)`~`(0.92,0.66)`; 3개 별은 내부 3등분 Anchor와 preserveAspect를 사용한다.
- Description label: `(0.075,0.49)`~`(0.35,0.555)`.
- Description text: `(0.075,0.355)`~`(0.925,0.495)`, Top Left, wrapping, overflow 잘림 없음.
- Effects root: `(0.075,0.125)`~`(0.925,0.35)`; 슬롯 X 비율은 `0~0.30`, `0.35~0.65`, `0.70~1.0`.
- Effect icon: 슬롯 내부 `(0.05,0.02)`~`(0.95,0.76)`, preserveAspect.
- Perform button: `(0.18,0.035)`~`(0.82,0.125)`.
- Button text: 버튼 전체 Stretch.
- 기존 RequestViewport/RectMask/Canvas safe 영역을 유지하고 별도 Canvas Scaler를 추가하거나 변경하지 않았다.

## 기존 별·시계

- 별 Sprite GUID와 `GetActiveStarCount(Easy=1, Normal=2, Hard=3)` 로직은 변경하지 않았다.
- 기존 별 Rect는 root `(55,78)/(150,30)`, 개별 X `-48/0/48`의 픽셀 배치였다. 변경 후 root와 각 별 모두 정규화 Anchor다.
- Request 카드에는 별도 시계 Rect가 없어 변경 대상이 없었다. 기존 공용 `Img_icon_time`과 시간 로직은 변경하지 않았다.
- 신규 quest icon은 CSV의 `EffectIconKey`로만 연결되며 기존 시계 Sprite를 임의 교체하지 않는다.

## Swipe·수행 흐름

- `OutgameRequestPopupView`는 현재와 같이 Offer당 카드 Prefab 1개, 총 3개 페이지를 생성한다.
- `OutgameRequestCarouselController`의 drag threshold, page boundary, snap duration, CurrentPage 변경 로직은 그대로다.
- Carousel 변경은 보드의 최대 폭/높이, 원본 비율, 수직 offset뿐이다.
- `OutgameRequestCardView.Awake`의 Button Listener는 1회 등록되고 `OnDestroy`에서 해제된다. Prefab persistent OnClick은 0개다.
- `PerformRequested`, SelectionController, Pending Request, Snapshot, Difficulty, Permanent/Phase Seed, INGAME load guard 코드는 변경하지 않았다.
- Rebind 시 Portrait를 새 키로 교체하고 모든 효과 슬롯을 null Sprite로 초기화한 뒤 현재 데이터만 채우므로 이전 페이지 Sprite 잔류를 방지한다.

## 화면비 정적 계산

아래 값은 전체 화면을 Safe Area로 가정한 공식 계산값이다. 실제 기기 notch 및 Scene Viewport 결과는 Play Mode에서 확인해야 한다.

| 해상도 | 계산 보드 크기 | 중심 Y offset | 정적 결과 |
|---|---:|---:|---|
| 720×1280 | 633.6×943.8 | +57.6 | 폭 제한, 원본 비율 유지 |
| 1080×1920 | 950.4×1415.8 | +86.4 | 폭 제한, 원본 비율 유지 |
| 1080×2340 | 950.4×1415.8 | +105.3 | 폭 제한, 원본 비율 유지 |
| 1440×2560 | 1267.2×1887.7 | +115.2 | 폭 제한, 원본 비율 유지 |
| 1440×3120 | 1267.2×1887.7 | +140.4 | 폭 제한, 원본 비율 유지 |
| 1536×2048 | 1099.9×1638.4 | +92.2 | 높이 제한, 원본 비율 유지 |

정규화 Rect 사이에는 Description→Effects 0.005H 간격이 있고 Effects와 Button은 경계를 공유하되 겹치지 않는다. Header/Portrait/별/설명/효과/버튼의 실제 glyph clipping, Safe Area 침범, 클릭 영역은 Play Mode 검증 필요다.

## 검증

- Runtime Roslyn: `Assembly-CSharp.csproj --no-restore`, exit 0, warning 0, error 0.
- Editor Roslyn: `Assembly-CSharp-Editor.csproj --no-restore`, exit 0, warning 0, error 0.
- `git diff --check`: PASS.
- Prefab YAML: 보드/버튼/초상 7/효과 7/null GUID가 모두 존재한다.
- CSV: PortraitKey 4건 모두 binding 존재, EffectIconKey 3건 모두 binding 존재, 효과 슬롯 결과는 항상 3칸이다.
- GUID 중복: 0.
- Missing Script/Unity Missing Reference/Console: Unity를 실행하지 않아 미검증. 정적 YAML 참조와 meta GUID 존재만 확인했다.
- Swipe/PerformButton/Pending/Scene Load: 관련 로직 무변경을 정적으로 확인했으나 Play Mode 미검증.
- Scene SHA: 작업 전·후 `OUTGAME_LOBBY.unity` = `13EAA5D4A6BA6C166BAB8DD7E35A2B78E4E3A441BCE9E751860DD661DE1AE1D8`, 변경 없음.
- Prefab SHA: 작업 전 `6763C92508B1B314EDAFF2F817C37FF0E862CE264725E82299F0755FCD280E97`, 작업 후 `1F6EBDBDB338E79AB55AC3E116C993E7884FBE6CAA2FAD3EA41BF508C99A6608`.
- `requests.csv` SHA: 전·후 `106B5E922CCABD3BFCA06F8C2FDDB68E20D5A46F3DA038B928FB42095DAA894D`.
- `request_effects.csv` SHA: 전·후 `F829A9086300346FAACA7B14FF793BE67F6B6AE14C8D9797056BEAD60ABB124F`.
- PNG/meta 변경: 0개.
- INGAME Scene, Phase 1/2/3, Seed, 진행도, 종료 UI, 설정 메뉴, Canvas Scaler, ProjectSettings 변경: 0개.

## 최종 결론

- `Img_questui`: 보드판에 직접 적용됨.
- `Img_button_request`: 원본 meta 변경 없이 Texture2D 직접 참조 및 런타임 Sprite로 버튼에 적용됨.
- 버튼 문구: 별도 TMP `의뢰 수락`.
- Request별 Portrait: 명시적 `PortraitKey` binding 적용.
- Request별 효과: 명시적 `EffectIconKey` binding 적용.
- 빈 효과: `Img_icon_quest_null`, 항상 3칸.
- 기존 별 Sprite/계산: 유지.
- 기존 시계 Sprite/계산: 유지, 카드에 없던 시계 오브젝트를 임의 추가하지 않음.
- 레퍼런스 정보 계층 및 정규화 Anchor: 적용.
- Sprite 종횡비: 보드, 초상, 효과, 버튼 모두 유지하도록 설정.
- Swipe/수행/Seed/Scene Load 계약: 코드 변경 없음.
- 실제 시각, 입력, Safe Area 결과: Play Mode 검증 필요.
- 의뢰 PNG/meta가 아직 untracked이므로 사용자가 커밋할 때 PNG와 meta를 함께 포함해야 한다.

`STATIC READY / OUTGAME REQUEST BOARD UI PLAY MODE VALIDATION REQUIRED`
