# INGAME 패배 화면·RETRY·빈 화면 로비 복귀 구현 보고

## 이번 단계 핵심 요약

- 최종 판정: `STATIC READY / DEFEAT UI, RETRY, AND LOBBY TAP PLAY MODE VALIDATION REQUIRED`
- Branch: `OutGame_Contec`
- HEAD: `b3848fe10603d7a027a722d3611d4589f26fc88d`
- 작업 전 Git 상태: INGAME 및 Phase 1/3/GameFlow 기존 Modified, 리소스/문서 Untracked가 존재하는 dirty worktree
- 기존 Staged: 리소스 참조 안정화 작업의 6개 파일을 그대로 유지
- 이번 작업 수정 파일:
  - `Assets/Scenes/INGAME.unity`
  - `Assets/Scripts/GameFlow/Runtime/GameSessionController.cs`
  - `Assets/Scripts/GameFlow/Runtime/PhaseTransitionController.cs`
  - `Assets/Scripts/GameFlow/UI/PhaseTransitionOverlay.cs`
  - `Assets/Scripts/Outgame/Runtime/OutgameRequestRunSelection.cs`
  - `Assets/Scripts/Outgame/Runtime/OutgameRequestSelectionStore.cs`
  - `Assets/Scripts/Outgame/Editor/OutgameRequestStage7Validation.cs`
  - 본 보고서
- 이번 작업 기존 제공 리소스: `Img_playerdown.png/.meta`, `retry_button.png/.meta`는 작업 전부터 Untracked였으며 수정하지 않음
- 예상 밖 변경: 0
- Commit/Push: 수행하지 않음
- Unity 실행: 수행하지 않음
- Play Mode: 수행하지 않음

## 기존 종료 구조

- Timer 소유자: `GameTimerController`가 `GameCountdownTimer`를 소유
- 만료 진입 메서드: `GameCountdownTimer.Expire()` → `TimerExpired` → `GameSessionController.OnTimerExpired()` → `TryCommitTerminalDefeat()`
- Session State: `GameSessionModel`; `Expired`와 `Completed`는 상호 배타적인 terminal 상태
- 성공 Terminal Commit: `TerminalPhaseClearCommitGate`와 `GameSessionController.TryCommitTerminalPhaseClear()`
- Score Lock: `GameScoreController.LockScore()` 및 `GameSessionModel.CanAddScore`
- Input Lock: `GamePhaseRegistry.DisableAllInput()`과 `GamePhaseTransaction.SetCurrentInputEnabled(false)`
- Request Context: `GameRunContext`; 선택 Offer 원본은 `OutgameRequestSelectionStore`의 active snapshot으로 유지
- INGAME Scene: `INGAME`, Build Settings 등록 확인
- OUTGAME Lobby Scene: `OUTGAME_LOBBY`, Build Settings 등록 확인

## 확정 리소스

### Img_playerdown

- 실제 경로: `Assets/Resources/Ingame/Img_playerdown.png`
- 파일명 대소문자: `Img_playerdown.png`
- GUID: `5ff4e5e412ef2f04fb1f9d2aa62a46ec`
- 원본 크기: `1122 × 1402`
- Aspect Ratio: `0.800285` (width/height)
- `.meta`: 존재, Sprite Mode Single, Texture Type Sprite, Alpha transparency 활성
- Git 추적: PNG/meta 모두 현재 Untracked
- Ignore: 아님
- LFS: `filter=lfs` 대상, 현재 파일은 pointer가 아닌 실제 PNG binary
- Scene 연결: INGAME의 `PhaseTransitionOverlay.defeatCharacterSprite`에 GUID 직렬화
- Preserve Aspect: `true`
- Raycast Target: `false`
- 이미지 내용: 사용자 확정 실패 캐릭터와 투명 영역을 직접 확인

### retry_button

- 실제 경로: `Assets/Resources/Ingame/UI/Button/retry_button.png`
- 파일명 대소문자: `retry_button.png`
- GUID: `2a21ef1aa0575bb4681092604d4d312b`
- 원본 크기: `1308 × 427`
- Aspect Ratio: `3.063232` (width/height)
- 투명 배경: 있음
- `.meta`: 존재, Sprite Mode Single, Texture Type Sprite, Alpha transparency 활성
- Git 추적: PNG/meta 모두 현재 Untracked
- Ignore: 아님
- LFS: `filter=lfs` 대상, 현재 파일은 pointer가 아닌 실제 PNG binary
- Scene 연결: INGAME의 `PhaseTransitionOverlay.retryButtonSprite`에 GUID 직렬화
- 최종 표시 크기: `360 × 117.523`
- 문구 대비 폭 비율: `360 / 720 = 0.5`
- Target Graphic: `retry_button` Sprite를 사용하는 단일 Image
- 별도 TMP 텍스트: 0
- 별도 사각형 배경: 0
- Raycast 우선순위: Dim의 자식이며 마지막 Content Graphic인 Retry Button이 우선
- Preserve Aspect: `true`

### Jua

- 원본 Font: `Assets/Resources/Fonts/Jua-Regular.ttf`
- 원본 GUID: `8f3aef096eb2a8c4fa5f5e981a5ed3ae`
- TMP Font Asset: `Assets/Resources/Fonts/Jua-Regular SDF.asset`
- GUID: `3f2407402af4faf46ade1facb5b8a52e`
- Material: 내부 `Jua-Regular SDF Material`, fileID `5834738053712705277`
- Atlas Population Mode: Dynamic (`1`), Source Font 참조 정상
- Scene 연결: INGAME의 `PhaseTransitionOverlay.defeatFont`에 GUID 직렬화
- 한글 출력: 원본 TTF `CharacterToGlyphMap`에서 `의`, `뢰`, 공백, `실`, `패`, `.` 전부 glyph 존재 확인
- Missing Glyph: 정적 원본 Font 기준 0; TMP 동적 Atlas 실제 생성은 Play Mode 미검증

## Terminal 패배

- Commit 메서드: `GameSessionController.TryCommitTerminalDefeat()`
- 중복 방어: `GameSessionModel.SetState(Expired)`가 첫 성공 이후 모든 terminal 재진입을 거부
- 성공 후 패배 차단: Phase 3 terminal clear gate가 먼저 commit되면 Timer를 즉시 정지하고 만료 요청을 무시
- 패배 후 성공 차단: Expired 상태에서는 `TryCommitTerminalPhaseClear`가 Playing 조건을 통과하지 못하며 `CompleteGame`도 Completed 전환 실패
- Timer 정지: 만료된 Timer는 Expired 상태로 Tick이 중단되고 `StopTimer()`도 호출
- Score 잠금: Expired의 `CanAddScore=false`와 `LockScore()` 이중 방어
- 입력 차단: 전체 Registry와 현재 Phase gate를 모두 비활성화
- Phase 전환 차단: Expired terminal 상태 및 exit-ready gate 상태 검사로 차단
- 패배 이벤트 횟수: `GameSessionModel.GameExpired` 최대 1회
- 패배 화면 표시 횟수: 첫 패배 commit에서만 요청, Overlay 자체 `defeatVisible` guard와 `DefeatShowCount` 보유

## 패배 화면

- Root: 런타임 `DefeatResultRoot`
- 구조: `DefeatResultRoot/ContentRoot/{Image_PlayerDown, Text_RequestFailed, Button_Retry}`
- Canvas: 기존 `Canvas/Game_UI_Transition` 재사용
- Sorting: 기존 override sorting order `32760` 유지
- EventSystem/GraphicRaycaster: 기존 것만 재사용; 신규 생성 0
- 초기 Active: 패배 전 미생성/비표시. 최초 구성 시 Inactive로 만든 뒤 패배 commit에서 한 번 Active
- Dim Color/Alpha: `(0, 0, 0, 0.7)`, 전체 Stretch
- 캐릭터 위치와 크기: center `(0, 170)`, width `700`, 원본 비율 높이 약 `874.688`
- 문구 위치와 크기: center `(0, -410)`, `720 × 130`, font size `100`, 한 줄 중앙 정렬, 붉은색
- RETRY 위치와 크기: center `(0, -575)`, `360 × 117.523`
- Safe Area: 기존 Canvas Scaler `1440 × 2560`, match `0.5`를 변경하지 않고 중앙 안전 영역에 배치
- Raycast 구조: Dim Image/Button이 전체 화면을 받으며 캐릭터·문구는 raycastTarget=false, Retry Image/Button만 위에서 입력을 소비
- 기존 Settings·HUD·게임 화면: sorting order 32760의 반투명 Dim 뒤에 유지

## 입력 계약

- RETRY 클릭: 공용 navigation guard를 먼저 획득한 뒤 동일 Context를 Pending으로 복원하고 INGAME을 1회 reload
- 빈 화면 클릭: Dim Button이 OUTGAME_LOBBY load를 1회 요청
- 캐릭터 클릭: 캐릭터 raycastTarget=false이므로 Dim 클릭
- 문구 클릭: TMP raycastTarget=false이므로 Dim 클릭
- 동일 입력에서 Retry+Lobby 동시 실행: `resultNavigationStarted` 단일 guard로 두 번째 action 차단
- Navigation Guard: `PhaseTransitionOverlay.TryBeginResultNavigation()`
- 멀티터치 방어: 첫 유효 Button listener가 Retry/Dim 모두 interactable=false로 만들고 guard를 고정
- Scene Load 최대 횟수: `GameSessionController._resultSceneLoadRequested`와 `ResultSceneLoadRequestCount`; 실패해도 같은 패배 화면에서 재호출하지 않음
- Listener 중복: UI를 한 번만 만들고 각 Button listener를 한 번만 등록, OnDestroy에서 제거

## Retry

- 선택 방식: `INGAME` Scene Reload
- RequestId 보존: 동일 active `OutgameRequestRunSelection`
- Request Snapshot 보존: 원본 `OutgameRequestOffer` 객체를 `OfferSnapshot`으로 유지하고 같은 객체를 Pending으로 복원
- Difficulty 보존: 동일 selection/context
- PermanentSeed 보존: 동일 selection/context
- Phase1Seed 보존: 동일 selection/context
- Phase2Seed 보존: 동일 selection/context; 제거하지 않음
- Phase3Seed 보존: 동일 selection/context
- Timer 초기값: Scene Start에서 `ResetTimer()`, 설정값 `90`
- Score 초기값: Scene Start에서 `ResetForNewSession()`, `0`
- Phase 초기값: Scene 직렬화 `Phase1`
- Terminal/UI/입력/Phase Runtime 초기화: Scene Reload로 새 인스턴스 구성
- Retry 실행 횟수: Overlay action 최대 1, Scene load 최대 1
- Retry load 시작 실패: Pending을 다시 active snapshot으로 복구하되 Scene load guard는 잠금 유지

## 빈 화면 로비 복귀

- 실제 Lobby Scene: `OUTGAME_LOBBY`
- Load 방식: `SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single)`
- Dim 클릭: Lobby 요청
- 캐릭터 클릭: Dim에 도달하여 Lobby 요청
- 문구 클릭: Dim에 도달하여 Lobby 요청
- RETRY 옆 클릭: Dim에 도달하여 Lobby 요청
- 중복 방어: UI navigation guard + Session scene-load guard
- 클리어 기록: 없음
- 보상: 없음
- Context 정리: Lobby load가 시작된 경우 pending/active selection 모두 Clear
- 별도 Lobby 버튼: 0

## 경합 검증

- Success 먼저: terminal clear gate commit과 Timer stop이 패배를 차단
- Defeat 먼저: Expired state가 terminal clear, PhaseExitReady transition, Score 추가를 차단
- RETRY와 Dim 클릭 경합: 첫 action만 guard 승인
- 연속 터치: 첫 click 직후 두 Button 비활성
- 멀티터치: 공유 bool guard로 두 번째 pointer 거부
- 최종 Result 수: GameSessionModel terminal 1개
- 중복 Scene Load: Session guard로 최대 1

## 회귀검증

- Phase 1: 생성·HP·Grade 코드 수정 0; 기존 session/input gate 재사용
- Phase 2: 도포 계산 수정 0; adapter input만 공통 gate로 차단
- Phase 3: 생성·Geometry·Drag·Rotate·Snap 코드 수정 0
- Completion Shine: 수정 0; 성공 gate 이후 Timer가 정지하므로 패배와 경합하지 않음
- GamePanelCase: 계층/Sorting 변경 0
- DeckPanel: 계층/Sorting 변경 0
- Timer: 기존 Timer 재사용, 중복 Timer 생성 0
- Score: 기존 reset/lock/add 정책 재사용
- Request: 후보 생성 규칙 수정 0
- Seed: 계산식 수정 0; 동일 selection 객체 재사용
- OUTGAME UI: 레이아웃 및 입력 코드 수정 0

## Unity 검증

- Runtime Compile: PASS, warning 0 / error 0
- Editor Compile: PASS, warning 0 / error 0
- Missing Script: INGAME YAML 정적 검사 0
- Missing 내부 fileID Reference: INGAME YAML 289 documents/289 unique, 누락 0
- Sprite/TMP GUID: 실제 `.meta`와 Scene 직렬화 값 일치
- Missing Glyph: 원본 Jua TTF의 대상 문자 glyph 0개 누락; TMP runtime atlas 표시는 미검증
- Console Error: 미검증
- Console Warning: 미검증
- Play Mode: 미실행
- 사유: 작업 전부터 INGAME Scene이 dirty이고 Scene을 자동 저장하는 Setup 코드가 있어 사용자 변경 보존을 위해 Unity 실행을 강행하지 않음

## Scene 변경

- INGAME 작업 전 SHA: `C76B151037D5231A9890DCEF9D65A470A723F14EDC2A32CB2F6AAA1AFF7FA07F`
- INGAME 작업 후 SHA: `B04AF72D7311DAC40B136D5A625B684B9030B4D0E805704A452225BA25916F5D`
- OUTGAME 작업 전/후 SHA: `4DEC792FC35C24E070732D6C86FA8448E292E366DB3036E811039B1B8B6A4B3C`
- 변경 GameObject: 직렬화 GameObject 추가 0; 패배 시 런타임 Root/Content/Image/Text/Button 생성
- 변경 Component: 기존 `PhaseTransitionOverlay`에 Sprite 2개와 TMP Font 1개 참조 추가
- 연결 Sprite GUID: `5ff4e5e412ef2f04fb1f9d2aa62a46ec`, `2a21ef1aa0575bb4681092604d4d312b`
- 연결 TMP Font GUID: `3f2407402af4faf46ade1facb5b8a52e`
- Sibling/Canvas Sorting: 런타임 DefeatResultRoot를 Overlay의 마지막 sibling으로 설정, 기존 Transition Canvas order 32760 유지
- 관련 없는 YAML 의미 변경: 0. 기존 신규 Component의 빈 `m_Name:` 6행은 값·순서·GUID를 유지한 채 trailing whitespace만 정리
- `git diff --check`: PASS
- `git diff --cached --check`: PASS

## 기존 Staged 보존

- `Assets/Resources/Ingame/UI/Img_GamePanelCase.png`
- `Assets/Resources/Ingame/UI/Img_GamePanelCase.png.meta`
- `Assets/Scripts/GameFlow/Editor/IngameHudPersistenceFix.cs`
- `Assets/Scripts/GameFlow/Editor/PrePhase2Setup.cs`
- `Assets/Scripts/Phase1/Editor/Phase1PrototypeSetup.cs`
- `Docs/20260717_RESOURCE_SPRITE_REFERENCE_STABILITY_REPORT.md`

위 6개 staged 파일은 Unstage하거나 변경하지 않았다.

## 미검증 항목

- Phase 1/2/3 각각의 실제 Timer 0 패배 화면
- 마지막 Piece Snap 직전/직후 성공·패배 경합
- 실제 기기에서 Dim, 캐릭터, 문구, Retry의 시각 크기와 Safe Area
- GraphicRaycaster 기준 Retry와 Dim의 실제 hit 우선순위
- Retry 중앙/가장자리 클릭
- 캐릭터·문구·빈 영역 Lobby 클릭
- 연속 터치 및 멀티터치
- Scene Reload 후 Request/Difficulty/모든 Seed/Timer 90/Score 0
- Console Error/Warning 및 TMP 동적 Atlas 생성

## 최종 결론

- Timer 0 패배는 `TryCommitTerminalDefeat()`에서 최대 한 번 확정되도록 구현됨
- 성공과 패배는 기존 terminal model과 terminal clear gate로 동시에 확정되지 않음
- 전체 화면 검정 0.7 alpha Dim을 기존 INGAME 위에 표시하도록 구현됨
- `Img_playerdown`과 `retry_button`, Jua TMP Font가 실제 GUID로 Scene에 연결됨
- 캐릭터 아래 `의뢰 실패...`, 그 아래 문구 폭의 50%인 Retry Image를 배치함
- 캐릭터/Retry 원본 비율을 유지하고 이미지 파일은 수정하지 않음
- Retry는 동일 Offer Snapshot·Request·Difficulty·Permanent/Phase Seed로 INGAME을 reload함
- reload 후 기존 Start 흐름으로 Phase 1, Timer 90, Score 0이 적용됨
- Retry 외 영역은 OUTGAME_LOBBY로 이동하도록 구현됨
- Retry와 Lobby는 공유 navigation guard 및 scene-load guard로 상호 배타적임
- 패배를 클리어·보상으로 기록하는 코드는 추가하지 않음
- 정적 구조와 컴파일은 준비됐으나 실제 UI와 입력은 반드시 Play Mode에서 최종 검증해야 함
