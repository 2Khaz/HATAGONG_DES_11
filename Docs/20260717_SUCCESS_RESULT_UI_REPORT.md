# Phase 3 성공 정산 화면 구현 보고서

## 이번 단계 핵심 요약

- 최종 판정: `STATIC READY / SUCCESS UI ANIMATION AND LOBBY TRANSITION PLAY MODE VALIDATION REQUIRED`
- Branch: `OutGame_Contec`
- HEAD: `b3848fe10603d7a027a722d3611d4589f26fc88d`
- 기존 Staged: 6개 파일을 그대로 유지했으며 Unstage 또는 내용 변경을 수행하지 않았다.
  - `Assets/Resources/Ingame/UI/Img_GamePanelCase.png`
  - `Assets/Resources/Ingame/UI/Img_GamePanelCase.png.meta`
  - `Assets/Scripts/GameFlow/Editor/IngameHudPersistenceFix.cs`
  - `Assets/Scripts/GameFlow/Editor/PrePhase2Setup.cs`
  - `Assets/Scripts/Phase1/Editor/Phase1PrototypeSetup.cs`
  - `Docs/20260717_RESOURCE_SPRITE_REFERENCE_STABILITY_REPORT.md`
- 이번 작업 수정 파일:
  - `Assets/Scenes/INGAME.unity`
  - `Assets/Scripts/GameFlow/Runtime/GameSessionController.cs`
  - `Assets/Scripts/GameFlow/Runtime/PhaseTransitionController.cs`
  - `Assets/Scripts/GameFlow/UI/PhaseTransitionOverlay.cs`
- 신규 파일: `Docs/20260717_SUCCESS_RESULT_UI_REPORT.md`
- 예상 밖 변경: 0개
- Commit/Push: 미실행
- Unity 실행: 미실행
- Play Mode: 미실행
- Unity 미실행 사유: `Phase1PrototypeSetup`의 `InitializeOnLoadMethod → EditorApplication.delayCall → EditorSceneManager.SaveScene` 자동 저장 경로가 존재한다. 현재 INGAME Scene에 사용자 미완료 변경이 있으므로 Domain Reload를 강행하지 않았다.

## 실제 리소스

검색 결과 각 이름은 프로젝트에 하나만 존재했다. 다섯 파일 모두 PNG 원본이며 Git LFS 포인터가 아니고, 대응 `.meta`가 존재하며 `Texture Type: Sprite (2D and UI)`, `Sprite Mode: Single` 상태다.

| 용도 | Asset 이름 | 실제 경로 | GUID | Git 추적 | Scene 참조 |
|---|---|---|---|---|---|
| 메인 클립보드 | Img_questui | `Assets/Resources/Img_questui.png` | `8f7e9dc1f7d0fca46acba0c7fe03f259` | PNG/meta untracked | 연결 |
| 골드 패널 | Img_goldshowui | `Assets/Resources/Img_goldshowui.png` | `ed7f2f79eb0051948bd542358c1b1b42` | PNG/meta untracked | 연결 |
| 골드 아이콘 | Img_icon_gold | `Assets/Resources/Ingame/ICON/Img_icon_gold.png` | `22f4a855f1c6e0d4fbecb4ff57703c40` | PNG/meta untracked | 연결 |
| 시계 아이콘 | Img_icon_time | `Assets/Resources/Ingame/ICON/Img_icon_time.png` | `b96315092749e8943ac555ee46891129` | PNG/meta tracked | 연결 |
| 별 아이콘 | Img_icon_star2 | `Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star2.png` | `b0ab8e005eea08d47af5a5ece7efd4d5` | PNG/meta tracked | 연결 |

- 원본 해상도: Img_questui 1015×1512, Img_goldshowui 844×371, Img_icon_gold 170×169, Img_icon_time 109×133, Img_icon_star2 71×68.
- Scene은 기존 `PhaseTransitionOverlay` 컴포넌트의 직렬화 필드에 실제 `.meta` GUID를 연결했다.
- PNG 또는 `.meta` 내용 수정·재생성: 0개
- 성공 UI TMP는 기존 `Jua-Regular SDF` 참조를 재사용한다.

## 성공 흐름

- Terminal Success 지점: `Phase3TangramManager.CheckCompletion()`에서 모든 Piece 배치 확인 후 `TryCommitTerminalPhaseClear(Phase3)`를 호출한다.
- Terminal 상호 배제: `TerminalPhaseClearCommitGate`가 성공을 한 번만 Commit하고 이후 Timer 만료를 무시한다. 패배가 먼저 Commit되면 `GameSessionModel`의 terminal state 규칙 때문에 성공 완료 상태로 전환할 수 없다.
- Shine 시작: 최종 Phase 3 점수 정산과 `PhaseCleared` 이후 기존 `StartCompletionPresentation()`을 그대로 사용한다.
- Shine 종료 콜백: 기존 `CompletionShineRoutine → FinishCompletionPresentation → RaiseExitReady → PhaseExitReady` 흐름을 그대로 사용한다.
- 성공 UI 표시 지점: `GameSessionController.OnPhaseExitReady(Phase3) → CompleteGame → PhaseTransitionController.ShowGameCompleted`이다. 따라서 Shine가 종료되기 전에 성공 UI가 열리는 신규 경로는 없다.
- 클릭 입력 허용 지점: 0.52초 Clipboard 등장 연출 완료, 모든 활성 포인터 Release 확인, 추가 1프레임 대기 후다.
- OUTGAME_LOBBY 이동 지점: 성공 InputCatcher의 첫 유효 포인터 클릭이 `ReturnToOutgameLobbyAfterSuccess()`를 호출한다.

## 성공 UI 계층

성공 UI는 기존 최상위 `PhaseTransitionOverlay` 아래에 Runtime으로 한 번만 생성되고 초기에는 Inactive다. 별도 Canvas, EventSystem, GraphicRaycaster를 만들지 않는다.

```text
SuccessResultRoot
├─ Dim
├─ InputCatcher
└─ ClipboardRoot
   ├─ Img_QuestPanel
   ├─ Title_Clear
   ├─ ScoreSection
   │  ├─ Img_StarIcon
   │  └─ Text_AcquiredScore
   ├─ TimeSection
   │  ├─ Img_TimeIcon
   │  └─ Text_RemainingTime
   ├─ TotalScoreSection
   │  └─ Text_TotalScore
   └─ GoldPanel
      ├─ Img_GoldPanel
      ├─ Img_GoldIcon
      └─ Text_GoldReward
```

- Result Root: 기존 공용 결과 Overlay 계층을 사용한다.
- Dim: 검정 alpha 0.68, raycastTarget false.
- InputCatcher: 전체 화면 투명 Image 하나이며 raycastTarget true. 확인 Button이 아니다.
- Clipboard Root: Img_questui 비율을 유지하며 폭 1040으로 표시한다. INGAME 1080 기준 약 96.3%로 가시성을 확장했다.
- 내부 Section, 아이콘, 텍스트, 폰트는 기존 760 기준 배치를 `1040/760` 배율로 함께 확대해 패널과 내용 비율을 유지한다.
- 표시 순서: `CLEAR! → 획득 점수 → 남은 시간 → 총 점수 → 별도 골드 패널`.
- CLEAR 제목: 노란색 굵은 글씨, 짙은 외곽선으로 흰 클립보드 배경에서도 식별되도록 구성했다.
- 점수와 시간 숫자: 굵은 진청색으로 강조한다.
- Quest Panel, 아이콘, 텍스트: 모두 raycastTarget false다.
- 성공용 확인 Button: 0개
- 패배 UI와 성공 UI는 각각 상대 Result 활성 상태를 검사하므로 동시 표시되지 않는다.

## 등장 연출

- 시작 위치: `(0, -1600)`
- Overshoot 위치: `(0, 45)`
- 최종 위치: `(0, -20)`
- 재생 시간: 총 0.52초(상승 0.42초 + 정착 0.10초)
- Overshoot: 짧은 65 unit 정착 구간만 사용한다.
- 회전/Scale 변화: 없음
- Time Scale 영향: `Time.unscaledDeltaTime` 사용으로 timeScale 0에서도 진행한다.
- 중복 연출 방어: 성공 Result 중복 표시 거부 및 단일 `successEntrance` Coroutine을 사용한다.
- 등장 중 입력 차단: InputCatcher는 뒤 게임 입력을 흡수하지만 `successInputEnabled=false`라 확인 동작을 수행하지 않는다.
- 마지막 Snap 관통 방지: 연출 완료 후 `Pointer`와 모든 Touch가 Release될 때까지 기다리고 1프레임 뒤 입력을 연다.
- 재표시 초기화: 표시할 때마다 Clipboard를 시작 위치로 되돌리고 입력/Scene Load Guard를 초기화한다.

## 점수·골드

- 점수 계산 코드 변경: 0개
- 골드 계산 코드 변경: 0개
- 남은 시간 출처: `GameTimerController.DisplayedSeconds`
- 획득 점수 출처: 기존 `Phase3ScoreRules.PhaseClearScore` (`1000`), 표시 전용 참조
- 총점 출처: Score lock 직전의 `GameScoreController.CurrentScore`
- 골드 출처: 사용자 지정 UI 계약에 따라 이번 성공 화면은 `0 골드`로 고정 표시한다.
- 골드 계산식은 만들지 않았고 총점 비례 또는 임시 보상 저장을 하지 않았다.
- Phase별 점수, Snap, 자동 부착, Clear, 총점 계산식은 수정하지 않았다.

## 전체 화면 입력

- 확인 Button 수: 0
- InputCatcher: 성공 Result 전체 화면을 덮는 투명 Image이며 기존 Overlay의 `IPointerClickHandler`로 이벤트가 전달된다.
- 입력 활성화 조건: 등장 완료 + 모든 Pointer Release + 1프레임 경과 + 성공 Result 활성 + Scene Load 미시작.
- 포인터 관통 방지: 등장 중에도 InputCatcher가 Raycast를 받아 뒤 게임 오브젝트 입력을 차단한다.
- 마우스/터치: 첫 Left pointer click만 처리한다.
- 멀티터치/연타 방지: `resultNavigationStarted`, `sceneLoadRequested`, `successInputEnabled` 삼중 Guard를 사용한다.
- Scene Load 시작 시 InputCatcher raycastTarget을 즉시 false로 바꾸고 추가 확인 처리를 잠근다.
- 패배용 Retry/Dim Button 흐름은 수정하지 않았다.

## 로비 이동

- 대상 Scene: `OUTGAME_LOBBY`
- Load 방식: 기존 `GameSessionController.TryLoadResultScene()`의 `SceneManager.LoadSceneAsync(..., Single)` 사용
- 성공 후 다른 분기: 0개. Retry, INGAME 재시작, 별도 Result Scene 분기가 없다.
- 중복 Load 방어: Overlay Guard와 GameSession `_resultSceneLoadRequested` Guard를 모두 통과한 최초 요청만 `ResultSceneLoadRequestCount`를 증가시킨다.
- Runtime Context 정리: Load 시작 성공 후 `OutgameRequestSelectionStore.Clear()`를 호출한다. Retry snapshot은 로비 후보 생성에 남지 않는다.
- Load 실패 시 성공 Result는 입력이 잠긴 상태로 유지되며 다른 Scene이나 Retry로 분기하지 않는다.

## Scene 무결성

- INGAME 작업 전 SHA256: `B04AF72D7311DAC40B136D5A625B684B9030B4D0E805704A452225BA25916F5D`
- INGAME 작업 후 SHA256: `6A64FFA2222DBFA15B4857E24450261157F70D90AA44B15DAC7D099E4CB39A19`
- OUTGAME_LOBBY 작업 전/후 SHA256: `4DEC792FC35C24E070732D6C86FA8448E292E366DB3036E811039B1B8B6A4B3C`
- 변경 GameObject: 기존 `PhaseTransitionOverlay` GameObject만 직렬화 필드가 변경됨
- 변경 Component: 기존 `HATAGONG.GameFlow.PhaseTransitionOverlay` MonoBehaviour
- 추가 직렬화 참조: 성공 UI Sprite 5개
- 신규 직렬화 GameObject/Component: 0개(Runtime lazy build)
- Scene YAML 문서: 289개 / 고유 ID 289개
- 누락 내부 fileID 참조: 0개
- 관련 없는 YAML 변경: 이번 작업에서 0개

## Unity 검증

- Runtime Compile: PASS, warning 0 / error 0
- Editor Compile: PASS, warning 0 / error 0
- Missing Script: Scene의 null `m_Script` 신규 발생 없음; 신규 Script 컴포넌트 직렬화 없음
- Missing Reference: 다섯 Sprite GUID 모두 대응 `.meta`가 있고 Scene 필드에서 해석됨
- Console Error/Warning: Unity 미실행으로 미검증
- Play Mode: 미실행
- 실제 Shine 타이밍, UI 비율, 등장 애니메이션, 마우스/터치/멀티터치, Scene Load 횟수는 Play Mode 검증 필요

## 최종 결론

- 정확한 5개 리소스 검색 및 사용: 정적 확인 완료
- Shine 종료 후 성공 UI 표시: 코드 경로 확인 완료
- Clipboard 아래→위 등장: 정적 구현 완료
- 확인 버튼 없음: 확인 완료
- 화면 전체 클릭 처리: 정적 구현 완료
- 클릭 후 OUTGAME_LOBBY 단일 분기: 확인 완료
- Scene Load 최대 1회 Guard: 확인 완료
- 기존 점수 계산식 변경 없음: 확인 완료
- 기존 골드 계산식 변경 없음: 확인 완료(기존 계산식 자체가 없음)
- 성공/패배 동시 표시 방지: 코드 Guard 확인 완료
- 관련 없는 파일 변경: 이번 작업에서 0개
- 주의: 성공 UI 신규 리소스 3종(Img_questui, Img_goldshowui, Img_icon_gold)의 PNG/meta는 현재 untracked다. 사용자가 커밋할 때 누락하면 Scene Sprite가 Missing이 되므로 PNG와 meta를 반드시 쌍으로 검토해야 한다.
- 최종 판정: `STATIC READY / SUCCESS UI ANIMATION AND LOBBY TRANSITION PLAY MODE VALIDATION REQUIRED`
