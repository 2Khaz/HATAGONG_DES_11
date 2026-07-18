# 페이즈별 무입력 안내 구현 보고서

## 이번 단계 핵심 요약

- 최종 판정: `STATIC READY / IDLE GAMEPLAY GUIDE PLAY MODE VALIDATION REQUIRED`
- Branch: `OutGame_Contec`
- HEAD: `b577eea41d68c996eaeaeb8e501cdffd2eb930cd`
- Unity 신규 실행: 0회
- Play Mode 실행: 0회
- 미실행 사유: `Phase1PrototypeSetup`에 `InitializeOnLoadMethod`, `EditorApplication.delayCall`, `EditorSceneManager.SaveScene` 경로가 있어 지시서의 자동 Scene 저장 안전 조건에 따라 강행하지 않음
- 기존 Staged: 0개, 변경 없음
- 신규 파일: `GameplayInputActivity.cs`, `IdleGameplayGuidePresenter.cs`, 각 meta, 안내 전용 Material과 meta, 본 보고서
- 수정 파일: `INGAME.unity`, `PrePhase2Setup.cs`, `GameSessionController.cs`, `Phase1PhaseAdapter.cs`, `Phase2PhaseAdapter.cs`, `Phase1BoardController.cs`, `Phase3TangramManager.cs`
- 삭제 파일: 0개
- 예상 밖 변경: 0개
- Commit/Push: 미실행
- 작업 전 INGAME SHA256: `180CA49CED26BCCB4FA85EFC28EF02B7EE83C49B13D92E5510EF63BA2578ABE2`
- 작업 후 INGAME SHA256: `EB2A67965355C42350B126032421AE5D1D277F03C5F447994D2310785AF51CF7`
- 기존 작업과 겹친 파일: `INGAME.unity`, `PrePhase2Setup.cs`, `GameSessionController.cs`; 기존 변경을 보존하고 무입력 안내 관련 항목만 추가
- 작업 전부터 존재한 다른 변경: `GameStartOverlay.cs`, `PhaseTransitionOverlay.cs`, 성공 화면 보고서, Phase Clear Font/Material 관련 Untracked 파일. 이번 단계에서 삭제·복구·Stage하지 않음

## 안내 계약

- Idle Threshold: 정확히 `3.0초`
- Phase 1: `타일을 파괴하세요!`
- Phase 2: `드래그해서 칠하세요!`
- Phase 3: `조각을 맞추세요!`
- 표시 위치: 기준 해상도 1440×2560의 화면 중앙, 1100×180 영역
- 글꼴 크기: 80, 한 줄, 중앙 정렬, 자동 크기 조절 비활성
- 펄스 주기: `1.5초`
- 최소/최대 Alpha: `0.35 / 0.9`
- 배경: 없음. Root에는 RectTransform, CanvasGroup, Presenter만 존재
- Raycast Target: false
- 입력 차단: 없음. CanvasGroup의 Interactable/BlocksRaycasts도 false
- 시간 기준: `Time.unscaledDeltaTime`
- 펄스 구현: 단일 Update 상태 계산. Coroutine 생성 0

## 상태 계약

| 상태 | 표시 | 처리 |
|---|---|---|
| Preparing | 숨김 | 측정 초기화 |
| READY/GO (`Ready`) | 숨김 | 측정 초기화 |
| Playing + 현재 Phase 입력 가능 | 3초 뒤 표시 | 유효 입력 시 즉시 숨김 및 초기화 |
| Transitioning | 숨김 | 측정 초기화 |
| Timer Pause | 숨김 | 일시정지 시간 미포함, 재개 후 0초부터 측정 |
| Application Pause/Focus Loss | 숨김 | 복귀 후 0초부터 측정 |
| Settings/다른 Popup | 숨김용 `SetExternalUiBlocked` 및 `blockingUiRoots` 계약 제공 | 현재 Scene은 Settings 버튼만 있고 Popup/일시정지 구현이 없어 연결 대상 없음 |
| Completion Shine | 숨김 | Phase 3 `IsCleared` 및 입력 게이트 OFF 즉시 감지 |
| Success Result (`Completed`) | 숨김 | 측정 초기화 |
| Defeat Result (`Expired`) | 숨김 | 측정 초기화 |
| Scene Load 요청 후 | 숨김 | `IsSceneLoadRequested`로 차단 |

## 유효 입력 연결

- 공통 전달: `GameplayInputActivity.NotifyValidGameplayInput(GamePhaseId)` 단일 알림
- Phase 1: `Phase1BoardController.TryHitDetailed`에서 실제 `ApplyDamage` 성공 후 알림. 빈 공간, 파괴된 타일, 입력 차단, Damage 거부는 알림 없음
- Phase 2: `Phase2PhaseAdapter.ConsumeResult`의 `LogicAcceptedCount > 0`에서 알림. 보드 밖/입력 차단/Logic 거부 결과는 알림 없음
- Phase 3: 실제 조각 선택 성공, Drag 시작, 0.25초 간격으로 제한한 실제 위치 이동, 45도 회전, Snap 성공, 덱 복귀에서 알림
- Phase 3 빈 공간 클릭: `selectedPiece == null`이므로 알림 없음
- 설정 버튼: 게임 입력 Activity를 호출하지 않음
- 타이머 초기화: Presenter가 현재 Playing Phase와 일치하는 알림만 받아 `idleElapsed=0`, 즉시 Alpha 0
- Phase 판정 소유권: Presenter가 HP/Coverage/Snap을 재계산하지 않음

## UI 계층

- Root: `Canvas/IdleGuideRoot`
- TMP: `Canvas/IdleGuideRoot/GuideText`
- Font Asset: `Assets/Resources/Fonts/KERISKEDU_B SDF.asset`
- Font GUID: `35e2e0550b5946f4aa6016c1acff0865`
- Material: `Assets/Resources/Fonts/KERISKEDU_B Idle Guide.mat`
- Material GUID: `c3f301e569da4b75b6b59f64e8748003`
- Material 소유권: Phase Clear Material과 분리. Phase Clear Material 수정 없음
- Hierarchy: `Game_UI_General` 다음, `Game_UI_Settings`와 `Game_UI_Transition` 이전
- 초기 상태: GameObject 활성, CanvasGroup Alpha 0
- Scene 추가 GameObject: `IdleGuideRoot`, `GuideText`
- Scene 추가 Component: RectTransform 2, CanvasGroup 1, IdleGameplayGuidePresenter 1, TextMeshProUGUI 1, CanvasRenderer 1
- 관련 없는 YAML 변경: 이번 단계 추가 없음

## 검증 결과

| 항목 | 결과 |
|---|---|
| 세 문구 UTF-8 정확성 | PASS |
| 문구 내 줄바꿈 | 0, PASS |
| 3초 미만 숨김/3초 이상 표시 분기 | 정적 PASS |
| Phase 시작·변경 시 초기화 | 정적 PASS |
| 유효 입력 즉시 숨김·초기화 | 정적 PASS |
| 같은 Phase에서 3초 후 재표시 | 정적 PASS |
| 상태별 표시 금지 | 정적 PASS |
| 빈 공간/설정 버튼 Activity 호출 | 0, PASS |
| 중복 Coroutine | 0, PASS |
| Scene 문서 ID | 297개 / 고유 297개, PASS |
| Scene Font/Material/Script GUID 참조 | 정적 PASS |
| Runtime Compile | warning 0 / error 0 |
| Editor Compile | warning 0 / error 0 |
| Missing Script | 신규 Script GUID와 meta 및 Scene 참조 일치, 정적 PASS |
| Missing Reference | 신규 Scene 로컬 fileID 및 Asset GUID 일치, 정적 PASS |
| Console Warning/Error/Exception | Play Mode 미실행으로 미검증 |
| git diff --check | PASS |
| PNG 변경 | 0 |
| GameTimerController 변경 | 0 |
| GameScoreController 변경 | 0 |
| Seed 코드 변경 | 0 |
| OUTGAME 변경 | 0 |
| ProjectSettings 변경 | 0 |

## Play Mode 미검증 항목

- 각 Phase의 2.9초/3.0초 실제 프레임 경계
- 실제 TMP 한글 동적 글리프 표시와 중앙 가독성
- Alpha 펄스의 실제 시각 주기
- Phase 1 빈 공간/실제 타격 비교
- Phase 2 결과 없는 Drag/실제 도포 비교
- Phase 3 선택·Drag·회전·Snap·덱 복귀와 빈 공간 비교
- 전환, Completion Shine, 성공/실패 결과와 동시 표시 0
- RETRY 후 Phase 1 초기화
- 장시간 연속 플레이의 Activity/Presenter 누적 0
- Console Warning/Error/Exception 0

## 최종 결론

- 정확한 3초 조건, 세 문구, 중앙 TMP, 배경 없음, Raycast 비활성, 1.5초 Alpha 펄스는 정적으로 구현됨
- 유효 입력은 기존 Phase 성공 판정 지점에서만 알리고, Presenter는 기존 게임 판정을 소유하지 않음
- 빈 공간 클릭과 설정 버튼은 타이머를 초기화하지 않음
- Ready, Transition, Pause, Completion, Result, Scene Load 상태의 차단 조건을 구현함
- 게임 Timer, Score, Seed, Clear 판정, 성공·패배, RETRY 흐름을 변경하지 않음
- 자동 Scene 저장 위험 때문에 Unity/Play Mode를 실행하지 않았으므로 실제 시각·입력 검증은 PASS로 기록하지 않음

`STATIC READY / IDLE GAMEPLAY GUIDE PLAY MODE VALIDATION REQUIRED`
