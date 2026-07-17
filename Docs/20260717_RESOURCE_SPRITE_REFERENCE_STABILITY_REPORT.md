# 리소스 경로 및 Sprite 참조 안정성 보고

## 이번 단계 핵심 요약

- 최종 판정: **조건부 FAIL — 로컬 참조는 정상이나 `Img_GamePanelCase.png`와 `.meta`가 Git 미추적이어서 Fresh Clone 안정성을 충족하지 못함**
- Branch: `OutGame_Contec`
- HEAD: `b3848fe10603d7a027a722d3611d4589f26fc88d`
- 작업 전 Git 상태: INGAME Scene 및 Phase 1/3/GameFlow 코드 등 기존 Modified와 문서/`Img_GamePanelCase` Untracked가 존재했음
- 작업 후 Git 상태: 기존 변경을 보존했으며, 이번 감사에서 C# 3개와 이 보고서만 추가 변경함
- 수정 파일: `PrePhase2Setup.cs`, `Phase1PrototypeSetup.cs`, `IngameHudPersistenceFix.cs`, 본 보고서
- 예상 밖 변경: 0
- Staged/Commit/Push: `0 / 0 / 0`
- Unity 실행 여부: 실행 안 함
- Play Mode 여부: 실행 안 함

## 집계 결과

- 조사 Sprite: 9개
- Missing PNG: 0
- Missing `.meta`: 0
- Untracked PNG: 1 (`Img_GamePanelCase.png`)
- Untracked `.meta`: 1 (`Img_GamePanelCase.png.meta`)
- 핵심 Sprite GUID mismatch: 0
- Assets 내 중복 GUID 그룹: 0
- 핵심 Sprite의 존재하지 않는 GUID 참조: 0
- 대상 Scene/Prefab 내부 존재하지 않는 fileID 참조: 0
- Missing Script: 0
- 명시적 null Sprite: 17개, 모두 색상 패널·입력 표면·Dim·placeholder 용도. 결함 판정 0
- Wrong Sprite Reference: 0
- Path Case Mismatch: 수정 전 2개, 수정 후 0
- `Resources.Load` Path Error: 핵심 대상 0
- `AssetDatabase` Path Error: 수정 전 4개 경로, 수정 후 0
- Null Overwrite Path: 수정 전 2개 유형, 수정 후 핵심 Setup/Apply 경로 0
- Setup Second-Run Diff: 미검증
- 핵심 PNG LFS Pointer Residue: 0
- Fresh Clone: **FAIL** (`Img_GamePanelCase` PNG/meta 미추적)

## 핵심 Sprite 표

| 용도 | 실제 경로 | GUID | PNG 추적 | Meta 추적 | LFS | Scene/Prefab 참조 | 판정 |
|---|---|---|---|---|---|---|---|
| Normal Request | `Assets/Resources/Ingame/ICON/RequestICON/Img_icon_normal.png` | `ecc57deb5af69204da79d9fce9fe8b9d` | Yes | Yes | 적용, 실제 binary | INGAME `RequestPresenter.normalIcon` | PASS |
| Sudden Request | `Assets/Resources/Ingame/ICON/RequestICON/Img_icon_sudden.png` | `3bc53aec751c8d84baf4dd7fca1b9d5e` | Yes | Yes | 적용, 실제 binary | INGAME `RequestPresenter.suddenIcon` | PASS |
| Phase 1 비활성 별 | `Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star1.png` | `dc4a0032fea6bdc40b69f9980876e7b2` | Yes | Yes | 적용, 실제 binary | INGAME, OutgameRequestCard | PASS |
| Phase 1 활성 별 | `Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star2.png` | `b0ab8e005eea08d47af5a5ece7efd4d5` | Yes | Yes | 적용, 실제 binary | INGAME, OutgameRequestCard | PASS |
| Phase 활성 Dot | `Assets/Resources/Ingame/ICON/PhaseICON/Img_icon_phaseOn.png` | `847d5a51f3969b04683ab9ba8c5c638c` | Yes | Yes | 적용, 실제 binary | INGAME `PhaseHUDPresenter.activeDotSprite` | PASS |
| Phase 비활성 Dot | `Assets/Resources/Ingame/ICON/PhaseICON/Img_icon_phaseOff.png` | `66bca4904b7953e47b7fc07d6d235227` | Yes | Yes | 적용, 실제 binary | INGAME `PhaseHUDPresenter.inactiveDotSprite` | PASS |
| Phase 1 배경 / Phase 2 Dust Cover | `Assets/Resources/Ingame/Floor/Dust.png` | `e2aaa5132839d874791b8c2048c0069c` | Yes | Yes | 적용, 실제 binary | INGAME, Phase2BlackCoverMask Material | PASS |
| Phase 2 Paint / Phase 3 배경 | `Assets/Resources/Ingame/Floor/Paint.png` | `3396870eeed76a04292e7a04f910fcff` | Yes | Yes | 적용, 실제 binary | INGAME | PASS |
| GamePanel Case | `Assets/Resources/Ingame/UI/Img_GamePanelCase.png` | `3e6bf4fa27b5df14cbb760bb05917a97` | **No** | **No** | 규칙 적용, 실제 binary | INGAME Image | **FAIL: Untracked pair** |

## 경로 감사

- `Assets/resource` 사용: 수정 전 `PrePhase2Setup` 2개 Request 경로와 `Phase1PrototypeSetup` 2개 Star 경로에서 발견
- `Assets/Resources` 사용: 수정 후 핵심 경로 전부 실제 추적 경로와 대소문자까지 일치
- 대소문자 불일치: 수정 후 0
- 절대 프로젝트 경로: Assets C# 코드 내 0
- 존재하지 않는 경로: 수정 후 핵심 로더 경로 0
- 수정한 경로 문자열:
  - `Assets/resource/Img_icon_normal.png` → `Assets/Resources/Ingame/ICON/RequestICON/Img_icon_normal.png`
  - `Assets/resource/Img_icon_sudden.png` → `Assets/Resources/Ingame/ICON/RequestICON/Img_icon_sudden.png`
  - `Assets/resource/Img_icon_star1.png` → `Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star1.png`
  - `Assets/resource/Img_icon_star2.png` → `Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star2.png`
- 핵심 `Resources.Load` 호출은 대상 Sprite를 직접 로드하지 않으며, 이번 대상과 관련된 잘못된 확장자 포함 경로는 없음

## GUID 감사

- 실제 `.meta` GUID: 위 표의 9개 값을 직접 추출함
- Scene 참조: INGAME에서 9개 핵심 GUID 모두 용도에 맞게 확인됨
- Prefab 참조: OutgameRequestCard가 star1/star2를 각각 inactive/active로 참조함
- ScriptableObject 참조: 핵심 9개 Sprite에 대한 직접 참조 없음
- Material 참조: `Phase2BlackCoverMask.mat`의 `_DustTex`가 Dust GUID를 참조함
- 존재하지 않는 핵심 GUID: 0
- 중복 GUID: Assets 전체 `.meta` 기준 0 그룹
- 잘못 연결된 핵심 Sprite: 0
- INGAME 내부 YAML 문서 289개/고유 fileID 289개, 누락 내부 참조 0
- OUTGAME_LOBBY 내부 YAML 문서 76개/고유 fileID 76개, 누락 내부 참조 0
- OutgameRequestCard 내부 YAML 문서 97개/고유 fileID 97개, 누락 내부 참조 0

## 명시적 null Sprite 판정

- INGAME 4개: `TransitionBanner`, `Phase2InputSurface`, `BackgroundDim` 2개
- OUTGAME_LOBBY 5개: `Page1`~`Page3`, `Dim`, `RequestViewport`
- OutgameRequestCard 8개: Portrait/Effect Icon placeholder, EffectSlot 배경, PerformButton
- 위 객체들은 Sprite 이미지가 아니라 색상·raycast·placeholder 용도의 uGUI Image이므로 의도된 null로 판정함
- 핵심 9개 Sprite 직렬화 필드의 null: 0

## Null 덮어쓰기 방어

- 문제 코드:
  - `Phase1PrototypeSetup`이 존재하지 않는 Star 경로의 null 결과를 직렬화 필드에 바로 대입
  - `IngameHudPersistenceFix`가 필수 Font/Sprite 로드 실패 여부를 확인하지 않고 Apply 가능
- 기존 동작: Star 또는 HUD Asset 탐색 실패 시 정상 직렬화 참조가 null로 교체될 수 있었음
- 수정 동작: 필수 Asset 전부가 유효할 때만 참조 설정 단계로 진행하며, 실패 시 명확한 `InvalidOperationException`으로 중단
- Request/Phase 아이콘: `PrePhase2Setup`에서 유효성 검사 후에만 Presenter 참조에 대입
- 탐색 실패 시 기존 참조 유지: 핵심 Sprite 설정 경로에서 보장
- Dirty 처리 조건: 실패 시 참조 대입 및 해당 Apply가 수행되지 않음. 성공 시 기존 Setup의 저장 정책은 유지
- 런타임 영향: 없음. Editor 전용 Setup/Apply 코드만 수정

## Setup 멱등성

- Setup 경로: Phase1 Prototype Setup, Pre-Phase2 Framework Setup, Phase2 Stage 5B Scene Setup, Outgame Stage 4/5 Setup, INGAME HUD Persistence Apply
- 자동 저장: 여러 Setup이 `SaveScene`/`SaveAssets`를 직접 호출함
- 자동 실행 위험: `Phase1PrototypeSetup`에 InitializeOnLoadMethod가 있으나, 추적 중인 `Phase1GameConfig.asset`이 존재하면 즉시 반환함. 현재 조건에서는 자동 Setup/Save 경로에 진입하지 않음
- `IngameHudPersistenceFix`: MenuItem 2개뿐이며 InitializeOnLoad/자동 저장 hook 없음
- 1회차 변경: 미실행
- 2회차 변경: 미실행
- Second-run diff: 미검증
- 미검증 사유: 작업 시작 전 INGAME Scene과 관련 코드가 이미 수정 상태이고 Setup들이 Scene을 직접 저장하므로, 사용자 변경을 섞거나 저장할 위험이 있어 실행 금지 원칙을 적용함
- 정적 판정: 리소스 로드 실패 시 null 덮어쓰기는 차단했으나, 전체 Setup의 두 번째 실행 diff 0은 Unity에서 별도 검증 필요

## Git 및 LFS

- PNG 추적 누락: `Img_GamePanelCase.png` 1개
- `.meta` 추적 누락: `Img_GamePanelCase.png.meta` 1개
- Ignore 문제: 핵심 9개 모두 ignore 대상 아님
- LFS 규칙: 핵심 PNG 모두 `filter=lfs`
- 현재 실제 binary: 9개 전부 PNG binary이며 LFS pointer text 잔류 0
- 추적된 8개 PNG: `git lfs ls-files` 대상 확인
- GamePanelCase: LFS 규칙은 적용되지만 아직 Git index 대상이 아니므로 `git lfs ls-files`에는 없음
- 다른 PC 위험: INGAME YAML은 GamePanelCase GUID를 참조하지만 Fresh Clone에는 PNG/meta가 생성되지 않아 해당 Image가 Missing Sprite가 됨
- 해결 조건: 사용자 승인 이후 PNG와 기존 `.meta`를 한 쌍으로 Git 추적·커밋해야 함. 이번 지시에 따라 `git add`는 수행하지 않음

## Scene·Prefab 변경

- INGAME 작업 전 SHA: `1C6EB6DDD371933EF7CFF0E48ED6D5A8A50E540D7FB5315258B218803AD88F0F`
- INGAME 작업 후 SHA: `1C6EB6DDD371933EF7CFF0E48ED6D5A8A50E540D7FB5315258B218803AD88F0F`
- OUTGAME 작업 전 SHA: `4DEC792FC35C24E070732D6C86FA8448E292E366DB3036E811039B1B8B6A4B3C`
- OUTGAME 작업 후 SHA: `4DEC792FC35C24E070732D6C86FA8448E292E366DB3036E811039B1B8B6A4B3C`
- 변경 GameObject: 0
- 변경 Component: 0
- 변경 Prefab: 0
- 관련 없는 YAML 변경: 0
- PNG, `.meta`, GUID, Import 설정 변경: 0

## Unity 검증

- Runtime Roslyn Compile: PASS, warning 0 / error 0
- Editor Roslyn Compile: PASS, warning 0 / error 0
- Missing Sprite: 핵심 직렬화 정적 감사 0; Unity Inspector/Console 검증은 미실행
- Missing Reference: 대상 Scene/Prefab 내부 fileID 및 Missing Script 정적 감사 0; Package 참조 포함 Unity 전체 검증은 미실행
- Console Error: 미검증
- Console Warning: 미검증
- Play Mode: 미실행
- Fresh Clone: 미실행 및 현재 상태로는 실패가 확정적임. 미추적 GamePanelCase pair는 Clone에 포함되지 않음
- `git diff --check`: PASS

## 회귀 방어

- Request 후보 생성, PermanentSeed, Phase 1 Board/HP/Grade, Phase 2 도포, Phase 3 생성/Drag/Rotate/Snap, DeckPanel, Timer, Score, 성공/실패 흐름은 이번 감사에서 수정하지 않음
- UI Layout, GamePanelCase 계층/Sorting, Scene/Prefab 직렬화는 변경하지 않음
- 변경은 Editor 전용 리소스 경로 및 실패 가드에 한정됨

## 미검증 항목

- Unity Editor Inspector에서의 실제 Sprite Preview
- Console Error/Warning
- Play Mode 시각 표시
- 전체 Package GUID를 포함한 Unity Missing Reference 검사
- 위험한 자동 저장 Setup의 1회/2회 실행과 second-run diff 0
- 별도 Fresh Clone 및 LFS checkout

## 최종 결론

- Normal/Sudden Request 아이콘: 로컬 GUID와 직렬화 참조는 정상이며 잘못된 Setup 경로를 수정함. 추적 상태도 정상
- Phase 1 별 및 Phase Dot: 로컬 GUID/Scene/Prefab 참조 정상, Setup 경로와 null 방어 수정 완료
- Phase별 배경: Dust/Paint GUID와 현재 INGAME/Material 연결 정상
- Img_GamePanelCase: 로컬 PNG/meta/GUID/Scene 연결은 정상이나 PNG와 `.meta`가 모두 Untracked라 다른 PC에서 유지되지 않음
- 경로 대소문자: 수정 후 핵심 코드 불일치 0
- Setup 실패 시 핵심 Sprite null 덮어쓰기: 수정 후 확인된 Production Setup/Apply 경로 0
- Setup second-run diff: 안전상 미검증이며 PASS로 간주하지 않음
- LFS pointer 잔류: 0
- 관련 없는 Scene/Prefab/게임 로직 변경: 이번 감사에서 0

따라서 현재 로컬 작업본은 정적 참조 기준으로 준비됐지만, **GamePanelCase PNG/meta를 함께 Git 추적·커밋하기 전에는 다른 PC/Fresh Clone 안정성 PASS가 아니다.**
