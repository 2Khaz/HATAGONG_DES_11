# Phase 2 Stage 1 프로젝트 조사 보고서

## 1. 작업 목적

Phase 2 시멘트 도포 구현 전 저장소, INGAME Scene, 공용 흐름, 입력, 그래픽 설정과 리소스를 읽기 전용으로 조사했다.

## 2. 작업 전 Git 상태

- Branch: main
- HEAD: ec3288c51b29b5ffb269b8457b49420477236db7
- git status/diff: clean
- 활성 Scene: Assets/Scenes/INGAME.unity, build index 0
- isDirty: false
- Console Error/Warning: 0/0

## 3. 조사 범위와 금지 사항

코드, Scene, Meta, Importer, Project Settings와 Unity Editor 상태만 읽었다. 코드, Shader, Material, Scene, Prefab, Importer, Package와 Project Settings를 변경하거나 저장하지 않았다.

## 4. INGAME Scene 계층

확인된 계층:

    Canvas
    ├─ Game_UI_General
    │  └─ Middle_GamePanel
    │     ├─ Game_Panel
    │     └─ Phase1_FieldRoot
    │        ├─ Phase1_TileContainer
    │        ├─ Phase1_EffectRoot
    │        └─ Phase1_ScorePopupRoot
    ├─ Game_UI_Settings
    └─ Game_UI_Transition

Game_Panel은 sibling 0, stretch Image, raycastTarget=false다. Phase1_FieldRoot는 sibling 1이다. Tile 입력은 동적 Phase1TileView의 pointer handler가 Phase1InputController로 전달한다. Game_UI_Transition은 Canvas sibling 2라 일반 UI보다 위다. 조사한 작업 영역과 조상에 Mask/RectMask2D는 없다.

## 5. 1250×1250 보드 검증

Phase1_FieldRoot 실제 값:

| 항목 | 값 |
|---|---|
| Parent / Sibling | Middle_GamePanel / 1 |
| Anchor Min/Max | (0.5,0.5) / (0.5,0.5) |
| Pivot | (0.5,0.5) |
| Anchored Position | (-7,-1) |
| Size Delta | (1250,1250) |
| Scale / Rotation | (1,1,1) / (0,0,0) |
| Active | true |

Middle_GamePanel은 1360×1378이므로 보드는 기하상 내부에 들어가고 Mask에 잘리지 않는다. 모바일 종횡비별 화면 잘림은 실기기/Game View 검증이 필요하다.

권장 위치:

    Middle_GamePanel
    ├─ Game_Panel
    ├─ Phase1_FieldRoot
    └─ Phase2Root

Phase2Root는 Phase1과 동일 RectTransform, 초기 inactive, sibling 2가 적합하다. 내부 순서는 CementFilledLayer, UnpaintedTopLayer, BrushPreview, CompletionSheen, ProgressUI다. Progress는 최상단이며 모든 Graphic은 raycastTarget=false로 둔다.

## 6. Canvas·해상도 구조

- Screen Space Overlay, camera null, Pixel Perfect false
- CanvasScaler Scale With Screen Size
- Reference Resolution 1440×2560
- Match Width Or Height, match 0.5
- Safe Area component 0

RectTransformUtility.ScreenPointToLocalPointInRectangle camera는 null을 사용한다. Phase2Root local point를 rect.xMin/yMin과 width/height로 0~1 UV 변환하고 교차 여부를 먼저 검사한다. Canvas 배율과 무관하게 논리 UV는 1250 local-unit rect 기준이다. Safe Area와 Progress 충돌은 실기기 검증 대상이다.

## 7. Phase 1 Clear 호출 흐름

실제 순서:

1. Phase1TileView.OnPointerDown
2. Phase1InputController.TryBegin
3. Phase1BoardController.TryHitDetailed
4. Session Playing + InputEnabled gate
5. Phase1TileView.ApplyDamage
6. Hit +10
7. DestroyVisual 및 Destroy +50
8. remaining 0이면 Clear
9. Clear +300
10. Phase1BoardController.Phase1Cleared
11. Phase1PhaseAdapter.OnCleared
12. Adapter.PhaseCleared

cleared bool이 이벤트를 1회로 제한하며 점수는 Clear 이벤트 전에 반영된다. Adapter.PhaseCleared를 Transition에 연결하는 Flow subscriber와 Phase1Root 비활성화 Runtime 경로는 아직 없다. 별도 GamePhaseFlowController가 Adapter 이벤트를 구독하는 것이 안전하다.

## 8. Transition API

확인 파일은 PhaseTransitionController.cs, PhaseTransitionOverlay.cs, PhaseTransitionResult.cs, GameSessionController.cs, Phase1PhaseAdapter.cs다.

실제 Checked API는 PlayPhaseClearTransitionChecked(GamePhaseId, GamePhaseId, Func<bool>, Action)이다. LastResult와 LastError를 제공하고 None, Succeeded, Interrupted, Failed, Rejected를 구분한다.

- 중복 또는 상태 불충족: Rejected
- midpoint false/예외: Failed, Session Transitioning, Timer Paused, input disabled
- Interrupted: FinishTransition으로 복구
- Succeeded: FinishTransition에서 Timer Resume와 input enable
- completed 예외: 핵심 성공과 분리해 LastError 기록

중요한 현재 한계: GameSessionController는 Phase1PhaseAdapter를 직접 소유하며 FinishTransition이 Phase1 입력을 다시 연다. Phase 2 통합 전 current IGamePhase를 소유하도록 확장해야 한다.

적용 원칙:

- 배너 전: RT, Material, Grid 준비 완료, Phase2Root inactive
- midpoint: IsReady 재확인, Root/HUD 교체, Phase2 Start 가능 확인 후 true
- midpoint에서 RT 최초 생성, Asset 로드, 무거운 데이터 생성 금지
- 실패: Failed 안전 잠금
- Overlay Disable: Interrupted로 준비 실패와 구분

## 9. 아웃게임 Difficulty 전달 경로

확인 결과:

1. 프로젝트 Runtime Scene은 INGAME과 TEST뿐이며 의뢰 선택 Runtime UI가 없다.
2. Runtime SceneManager.LoadScene 호출이 없다.
3. Difficulty 저장용 PlayerPrefs, static singleton, DontDestroyOnLoad, ScriptableObject run context가 없다.
4. Phase1BoardController.difficulty만 Scene 직렬화 값을 소유한다.
5. INGAME 실제 값은 2, 즉 Hard다.
6. 이 값이 Bag, HP, Grade, Layout과 HUD를 함께 결정하므로 Scene 내부 일치는 보장된다.
7. Scene 재시작과 Editor 직접 실행은 Hard로 돌아간다.
8. RequestType은 별도 데이터다.
9. Phase 2·3 공용 Difficulty Context는 없다.

따라서 아웃게임 선택 Difficulty 전달 경로는 부재로 확정한다.

## 10. 공용 Difficulty 권장 구조

GameRunContext를 cross-scene 단일 소유자로 권장한다.

- 공용 GameDifficulty Easy/Normal/Hard
- Difficulty, RequestType, initialized flag 소유
- 아웃게임 선택 시 1회 설정 후 게임 중 불변
- 명시적 bootstrap/DontDestroyOnLoad로 Scene 간 유지
- Phase1 Adapter가 Phase1Difficulty로 명시 변환
- Phase2/3은 Context를 읽고 Phase1BoardController를 참조하지 않음
- Editor 직접 실행은 명시적 Debug Override
- Production 초기화 누락은 조용한 기본값 대신 시작 차단과 1회 Error

## 11. Session·Timer·Score·Request

Session은 GameSessionController 내부 GameSessionModel이 Preparing, Ready, Playing, Transitioning, Expired, Completed를 소유한다. Phase 2는 CurrentState와 CanAcceptGameplayInput을 검사한다.

Timer 단일 소유자는 GameTimerController이고 순수 모델은 GameCountdownTimer다. Transition Pause/Resume는 GameSessionController.BeginTransition/FinishTransition에 있으며 Reset은 호출되지 않는다. Phase 2 Clear 점수 확정 후 Checked Transition을 시작하면 BeginTransition에서 Pause된다.

Score_Value 단일 소유자는 GameScoreController다. AddScore(amount, phase, reason)는 Playing 이외와 Lock 상태를 거부한다. Phase 2는 GamePhaseId.Phase2로 기존 총점에 누적하며 별도 총점 UI가 필요 없다.

Request는 Scene 단일 GameRequestContext와 RequestPresenter가 소유한다. Normal/Sudden은 Phase 전환으로 바뀌지 않지만 cross-scene 아웃게임 소유권은 아직 없다.

## 12. Production Mutation Gate 연결점

Phase 1은 Input gate를 Phase1InputController.TryBegin, 실제 mutation gate를 Phase1BoardController.TryHitDetailed에 둔다.

Phase 2 권장 순서:

1. Phase2InputController에서 pointer, cancel/focus, bounds 처리
2. Phase2BoardController.RequestStamp에서 Session Playing, InputEnabled, internal Running 확인
3. 통과 후에만 128×128 Grid 변경
4. 동일 성공 Stamp만 visual queue와 history 등록
5. 논리 delta로 점수와 99% Clear 판정

Public bypass API는 만들지 않는다.

## 13. 시멘트·바닥·UI 리소스

전용 Production 리소스는 없다.

| 경로 | 확인 사실 | 판정 |
|---|---|---|
| Assets/TextMesh Pro/Examples & Extras/Textures/Floor Cement.jpg | 520×390 RGB, alpha 없음, Default texture, mip on, read off, GUID 283f897e4925411ebbaa758b4cb13fc2, TMP 예제 Material에서만 사용 | Production 후보 아님 |
| Assets/TextMesh Pro/Examples & Extras/Textures/Floor Tiles 1 - diffuse.jpg | 600×600 RGB, alpha 없음, Default texture, GUID 85ac55597b97403c82fc6601a93cf241, 예제 Ground Material 사용 | 미도포 바닥 후보 아님 |
| Assets/resource/Img_UI_GamePanel.png | 1360×1378 Sprite/alpha, GUID b8a35353d95a4844db97c3573570fc98, INGAME Game_Panel 사용 | Frame 배경 |
| Assets/resource/Img_UI_Context.png | 1070×258 Sprite/alpha, GUID 6d05903f7281e7d49955d2267d00add7, HUD 사용 | Progress 장식 참고만 가능 |

CementFilledLayer, UnpaintedTopLayer, Soft Brush, Progress Track/Fill, Completion Sheen 전용 후보는 모두 없음이다. Progress 프로토타입은 단색 Image가 가능하다. 기존 LiberationSans SDF를 재사용할 수 있다.

## 14. Shader·Material

Production custom Shader/Material은 없다. 프로젝트 Shader는 TMP 렌더링/마스킹용이며 paint mask 용도가 아니다. BlendOp Max, RenderTexture, Blit, CommandBuffer 구현도 없다.

Source Alpha × (1 - Paint Mask) 표현에는 후속 단계의 신규 UI Shader/Material이 필요하다.

권장 표시 컴포넌트는 RawImage다. RenderTexture 직접 연결, 0~1 UV, custom material 제어가 단순하고 Sprite atlas UV 복잡성과 별도 MeshRenderer Camera 혼합을 피한다. raycastTarget=false로 둔다.

## 15. RenderTexture 포맷과 생성 정책

- Unity 6000.3.19f1, URP 17.3.0, Linear
- Editor: Windows/D3D12/NVIDIA RTX 2060 SUPER
- Active Build Target: StandaloneWindows64
- Current RP: Assets/Settings/PC_RPAsset.asset
- Mobile RP: Assets/Settings/Mobile_RPAsset.asset, renderScale 0.8
- Android: Vulkan + OpenGLES3, Automatic false
- iOS: Metal, Automatic true
- Project MTRendering enabled, Editor graphicsMultiThreaded false
- Graphics Jobs target override 없음

Windows Editor에서 R8_UNorm Sample/Render 지원은 true였다. 모바일 지원 증거는 아니다.

후속 정책:

- 1024×1024
- R8_UNorm Render/Sample 지원 검사
- 미지원 시 R8G8B8A8_UNorm
- Bilinear, Clamp, mip off, clear 0
- Preparing에서 생성/clear
- Reset 시 Grid/History와 clear
- teardown/OnDestroy에서 Release와 Destroy
- IsCreated 검사, focus 복귀 시 재생성과 History replay

## 16. Stamp Batch 후보 비교

| 후보 | 평가 |
|---|---|
| Stamp마다 Blit | stamp 수만큼 full-screen pass와 같은 RT read/write 위험, 비권장 |
| Stamp마다 CommandBuffer Draw | 누적 가능하나 draw call 다수 |
| 한 프레임 Mesh Batch | 소수 draw, GC 통제, Soft brush와 모바일에 적합 |
| Instanced Quad | 효율적이나 플랫폼 검증과 복잡도 증가 |
| 별도 Camera | Camera/Layer/RT 생명주기 비용 |
| Frame Stamp RT + Composite | persistent read/write 회피, 추가 RT/pass |

주 권장안은 한 프레임 stamp quad mesh batch를 CommandBuffer로 persistent RT에 그리는 방식이다. BlendOp Max 지원을 전제로 한다.

Fallback은 Frame Stamp RT batch 후 persistent mask와 프레임당 1회 composite다. spacing=radius×0.4, segment 최대 96은 CPU queue에서 제한하고 List/mesh buffer를 재사용한다.

## 17. RenderTexture 복구와 Stamp History

History는 Center UV, Logic Radius, Visual Feather, Stamp Source를 기록한다. 초기 List capacity 2048을 권장하고 실제 telemetry 후 조정한다.

RT 유실 시 pointer/queue 해제, RT recreate/clear, History visual replay 순서로 복구한다. replay는 Grid, Score, milestone, Clear를 변경하지 않는다. Cleared 상태는 완전 mask로 즉시 복구할 수 있다. Phase Reset은 History와 Grid를 함께 비운다.

현재 focus 처리는 Phase1InputController가 active pointer만 해제한다. Pause handler와 RT 복구는 Phase 2 책임으로 추가해야 한다.

## 18. PC·모바일 입력 시스템

- activeInputHandler 1: New Input System
- EventSystem + InputSystemUIInputModule + BaseInput
- Phase 1은 PointerDown/Up 기반, nullable activePointer 하나
- 다른 pointerId 거부, focus 상실 시 해제
- pointer cancel, drag/move 전용 처리, 직접 Touch polling 없음

Phase 2는 pointerId와 단일 pointer 패턴, Overlay camera=null 좌표 변환을 재사용한다. IPointerDown/Drag/Up, cancel, board 밖 clipping, focus/pause segment 종료가 새로 필요하다. Progress Graphic은 raycastTarget=false로 둔다.

Phase 1의 0.04초 debounce는 연속 도포에 사용하지 않는다. Phase 2는 거리 spacing을 사용한다.

## 19. Progress UI 배치

Phase2Root 내부 상단 Stretch 배치가 가능하다.

- Anchor Min (0,1), Max (1,1), Pivot (0.5,1)
- left/right 32, top -24, height 56
- Percent width 96, gap 16, Gauge height 30
- Image/TMP raycastTarget=false

Middle_GamePanel은 HUD 아래에서 시작해 구조상 분리된다. 실제 Safe Area는 별도 검증한다. 입력은 Progress가 아니라 Phase2Root hit surface가 받아야 한다.

## 20. Phase 2 Prepare 시점

권장 시점은 Session Preparing의 별도 GamePhaseFlowController 초기화, READY Overlay 시작 전이다.

Prepare 항목:

- GameRunContext Difficulty
- 필수 참조
- 128 Grid
- 1024 RT 선택/생성/clear
- Material instance
- queue/history
- Progress 0%
- Input disabled
- Phase2Root inactive
- IsReady

현재 GameSessionController.Start는 즉시 Ready와 StartOverlay를 실행해 비동기/실패 가능한 Prepare hook이 없다. 시각 통합 전에 bootstrap 확장이 필요하다. midpoint 최초 생성은 frame hitch와 실패 잠금 때문에 비권장이다.

## 21. 자동 검증 확장안

순수 논리: 128 Grid, 원형/중복/경계/outside stamp, 99% Clear, milestone/score, radius 0.085/0.075/0.065, mutation gate, spacing/96 cap.

시각 기술: R8/RGBA8 선택, clear/release/recreate, UV 대응, Soft brush/Max, batch draw/GC, history replay 불변식.

Play Mode: Phase1→2 Checked Transition, Timer 보존, Root/HUD, Progress 0%, PC drag, multi-pointer, 모바일 pointer simulation, 99% 1회, Phase3 부재 안전 처리.

기존 Timer 31, Session 11, Request 12, RiskReview 12, FinalSafety 8, Phase1 Matrix/Stress/Smoke를 유지한다.

## 22. 확정 사실

- Phase1_FieldRoot 1250×1250, position (-7,-1)
- Middle_GamePanel 아래 Phase2Root sibling 삽입 가능
- 작업 영역 Mask 없음
- Screen Space Overlay, 1440×2560, match 0.5
- Phase1 점수 후 Clear 이벤트 순서
- Checked Transition API와 실패 정책
- 실제 Phase1→2 Flow subscriber 없음
- 아웃게임 Difficulty 전달과 공용 Context 없음
- Scene 직접 실행 Difficulty Hard
- Phase 2 전용 리소스와 paint Shader 없음
- New Input System
- Windows Editor R8 Render/Sample 지원

## 23. 실행 검증 필요 항목

- Game View 해상도별 전체 보드 노출
- RawImage custom mask Shader
- batch stamp/composite
- RT 유실/History replay
- 실제 Root/HUD 교체
- Progress raycast pass-through
- Prepare 비용과 hitch

## 24. 실기기 검증 필요 항목

- Android Vulkan/OpenGLES3와 iOS Metal R8 RT
- BlendOp Max 정확도
- 1024 RT 메모리/발열
- touch move/cancel/focus/pause
- Safe Area
- 저사양 stamp batch 성능

## 25. 기존 설계와의 충돌 여부

1250 Root, 128 Grid, 1024 RT, 99%, Soft brush, 난이도 radius와 정수 Progress는 Scene 구조와 충돌하지 않는다.

선행 충돌:

1. 공용 Difficulty 전달 부재
2. GameSessionController가 Phase1 Adapter만 직접 소유
3. Preparing Prepare hook 부재
4. paint Shader/Material과 Production surface 리소스 부재

## 26. 2차 작업 선행 조건

순수 논리 코어는 UI/Shader/Scene 및 공용 GameRunContext와 분리해 착수 가능하다. board resolution 128, clear ratio 0.99, difficulty radius ratio를 외부 입력으로 주입받도록 한다.

GameRunContext와 공용 Difficulty는 논리 코어의 즉시 선행 조건이 아니다. 인게임 시작 시 난이도를 1회 확정해 Phase 1·2·3에 동일하게 주입하는 최종 방향을 유지하며, Phase 1→2 실제 Scene·Flow 통합 전까지 완료해야 하는 필수 후순위 작업이다.

시각/Scene 통합 전에는 GameRunContext, current IGamePhase Flow, Preparing contract, mask 기술 스파이크와 시각 리소스 결정이 필요하다. 상세 추적은 Docs/PHASE2_DEFERRED_WORK.md를 따른다.

## 27. 변경 파일

- Docs/PHASE2_STAGE1_PROJECT_INVESTIGATION_REPORT.md

Runtime 코드 변경: 0  
Editor 코드 변경: 0  
Shader 변경: 0  
Scene 변경: 0  
Prefab 변경: 0  
리소스 변경: 0  
Project Settings 변경: 0

## 28. 남은 위험

- 공용 Difficulty 부재로 Phase 간 난이도 불일치 가능
- FinishTransition이 현재 Phase1 입력을 다시 활성화
- Prepare hook 없이 midpoint RT 생성 시 hitch/실패 잠금
- 모바일 R8/BlendOp Max 미검증
- TMP Example Floor Cement의 Production 채택은 예제 의존성 문제

## 29. 최종 판정

**PARTIAL**

저장소·Scene·흐름·리소스·기술 조건 조사는 완료됐고 순수 논리 코어는 착수 가능하다. 공용 Difficulty/Phase Flow/Prepare contract 부재와 모바일 RT·Blend 실기기 미검증 때문에 전체 통합 준비를 PASS로 확정하지 않는다.
