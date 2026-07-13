# Phase 2 시멘트 도포 시스템 상세 기술 명세서 — GPT·Codex용

## 1. 문서 목적

이 문서는 새 GPT·Codex 세션이 과거 대화를 전혀 보지 못해도 Phase 2의 다음 내용을 복구하도록 한다.

- 게임 기획 의도
- 확정 수치
- Logic·Rendering·Orchestration·GameFlow의 책임 경계
- 입력과 생명주기 계약
- Scene 연결
- 점수와 Clear 순서
- 자원 소유권
- 자동 마감
- 검증 기준
- 변경 금지 계약
- 후속 작업 경계

---

## 2. Phase 2의 게임 내 역할

Phase 2는 Phase 1 철거 후 빈 바닥에 시멘트를 바르는 짧은 감각형 작업이다.

- 전체 게임 제한 시간: **90초 고정**
- Phase 2 목표 체류 시간: **15~18초**
- 최대 목표: **20초 이내**
- PC: 좌클릭 드래그
- 모바일 목표: 단일 유효 터치 드래그
- 내부 판정 도포율 99% 이상에서 Clear
- 시각적으로는 자동 마감 후 100% 완성처럼 종료
- 다음 단계는 Phase 3

Phase 2는 별도의 전략 판단보다 빠르고 시원한 도포감을 담당한다.

---

## 3. 현재 완료 범위와 미완료 범위

### 완료
- Logic Core
- GPU Paint Mask 기술 코어
- Runtime Orchestration
- 공용 Phase Flow 기반
- INGAME Scene 통합
- 실제 Pointer 입력
- 점수 연동
- 99% Clear
- 0.4초 시각 자동 마감
- Phase 3 부재 안전 잠금
- 자동·수동 검증

### 미완료
- Production 시멘트·미도포 텍스처
- Brush Preview
- Progress Gauge/Text
- Item
- Request의 Phase 2 실제 효과
- 사운드·파티클·진동
- 실기기 모바일 검증
- Phase 2→3 실제 성공 전환
- 전체 Phase 점수 재밸런스
- 최종 결과 화면과 Game Win 연결

---

## 4. 확정 상수

| 항목 | 값 |
|---|---:|
| Logic Grid | 128×128 |
| 총 논리 셀 | 16,384 |
| Clear Ratio | 0.99 |
| Clear 필요 셀 | 16,221 |
| Visual Mask | 1024×1024 |
| Board Rect | 1250×1250 |
| Board Position | (-7, -1) |
| Easy Radius Ratio | 0.085 |
| Normal Radius Ratio | 0.075 |
| Hard Radius Ratio | 0.065 |
| Interpolation Spacing | radius × 0.4 |
| Brush Solid 영역 | 반경의 90% |
| Brush Feather 영역 | 바깥 10% |
| Coverage Score 최대 | 500 |
| 25% Milestone | +100 |
| 50% Milestone | +150 |
| 75% Milestone | +200 |
| Clear Bonus | +500 |
| Phase 2 최대 점수 | 1,450 |
| Visual Auto Completion | 0.4초 |
| GPU Batch 안전 상한 | 65,536 |
| Recovery Replay Chunk | 4,096 |

---

## 5. 계층형 아키텍처

```text
GameSessionController / GameScoreController
                 │
                 ▼
        Phase2PhaseAdapter
                 │
                 ▼
      Phase2PaintOrchestrator
        ┌────────┼─────────┐
        ▼        ▼         ▼
 Logic Session  Mask RT   Interpolator/History
        │        │
        ▼        ▼
 128×128 Grid   1024×1024 GPU Mask
                 │
                 ▼
       Phase2MaskPresenter
                 │
                 ▼
       BlackCoverLayer Shader
```

입력 경로:

```text
InputSystemUIInputModule
        │
        ▼
Phase2PointerInputController
        │
        ▼
Phase2PhaseAdapter
        │
        ▼
Phase2PaintOrchestrator
```

---

## 6. Logic Core 계약

### 6.1 Grid
- 고정 크기 Storage로 128×128 도포 상태를 관리한다.
- 전체 이미지를 매 Stamp마다 스캔하지 않는다.
- 원형 Stamp의 bounding box 범위만 검사한다.
- 이미 도포된 셀은 다시 점수·진행도에 반영하지 않는다.
- PaintedCellCount는 mutation 시점에 누적한다.

### 6.2 좌표와 원 판정
- 입력은 Board UV 또는 정규화 좌표로 처리한다.
- 브러시 중심이 Board 밖이어도 원이 Board와 겹치면 처리한다.
- Board와 완전히 겹치지 않는 Stamp만 거부한다.
- 좌표를 Board 안으로 Clamp하지 않는다.
- 원-Board 교차 판정은 Logic과 Orchestration이 공유하는 기하 계약을 사용한다.

### 6.3 Clear
- `ceil(16384 × 0.99) = 16221`
- 최초로 16,221셀에 도달했을 때만 Clear 전환한다.
- 99% 이후 남은 셀을 Logic에서 100%로 조작하지 않는다.
- 자동 마감은 시각 전용이다.

### 6.4 난이도
난이도는 칠할 대상이 아니라 기본 브러시 반경을 바꾼다.

- Easy: 0.085
- Normal: 0.075
- Hard: 0.065

초기 기획의 별도 edge compensation 수치는 현재 Production 계약이 아니다. 현재 구현은 전체 Grid + 99% threshold + 겹침 Stamp 처리로 가장자리 난도를 완화한다.

---

## 7. 점수 계약

### 7.1 Coverage
- 새로 도포된 셀에 대해서만 지급한다.
- 전체 Coverage Budget은 최대 500이다.
- 같은 영역 재도포는 0점이다.

### 7.2 Milestone
- 25%: +100
- 50%: +150
- 75%: +200
- 각 1회만 지급한다.

### 7.3 Clear
- 99% 최초 도달: +500
- Phase 2 총점 최대: 1,450

### 7.4 공용 점수 반영
- Phase2PhaseAdapter가 Core의 `ScoreDelta`만 `GameScoreController.AddScore`로 전달한다.
- Phase 2가 공용 총점을 직접 재계산하지 않는다.
- 자동 마감은 추가 점수를 만들지 않는다.
- 이벤트 순서는 최종 ScoreDelta 반영 후 Clear/Exit 관찰자가 최종 총점을 볼 수 있어야 한다.

---

## 8. 드래그 보간

- PointerDown에서 첫 Stamp를 적용한다.
- Drag는 이전 좌표와 현재 좌표 사이에 Stamp를 생성한다.
- 간격은 `radius × 0.4`
- 빠른 마우스·터치 이동에도 빈틈이 과도하게 생기지 않아야 한다.
- Stroke가 실제로 끝난 뒤 새 PointerDown이 오면 이전 Stroke 좌표와 연결하지 않는다.
- 경계 밖 좌표가 Core에서 “도포 없음”으로 거부되어도 활성 Stroke 자체는 유지한다.

---

## 9. GPU Rendering 계약

### 9.1 Mask 의미
- Mask 0: 미도포
- Mask 1: 도포
- Scene의 검은 Cover Alpha는 반대로 계산한다.

```text
effectiveMask = max(mask.r, CompletionFill)
coverAlpha = (1 - effectiveMask) × GraphicAlpha
```

### 9.2 RenderTexture
- 기본 1024×1024
- 우선 포맷: `R8_UNorm`
- 필요 시 `R8G8B8A8_UNorm` fallback
- Depth 0
- MSAA 1
- Linear
- Bilinear
- Clamp
- Mipmap 없음

### 9.3 Ping-Pong
Persistent Mask A/B가 Composite마다 교환된다.

```csharp
previousMask = _mask;
_mask = _scratch;
_scratch = previousMask;
```

따라서 Prepare 직후의 첫 Mask 참조가 영원히 “현재 Mask”라는 보장은 없다. 생명주기 검증은 Deactivate 직전 현재 Mask를 저장해 비교해야 한다.

### 9.4 Brush
- 반경 90%까지 완전 도포
- 바깥 10% feather
- Frame Stamp에는 Max Blend
- Persistent Mask 합성은 `max(existing, frameStamp)`
- Mask는 단조 증가해야 한다.

### 9.5 Runtime 금지
- Runtime ReadPixels 금지
- Runtime 전체 픽셀 CPU 스캔 금지
- Stamp마다 Material 생성 금지
- Source와 Destination을 같은 RT로 사용 금지

---

## 10. Orchestration 계약

Phase2PaintOrchestrator는 다음을 소유한다.

- Logic Session
- Paint Mask Renderer
- Stamp Interpolator
- Stamp History
- Recovery
- 요청 결과 통합

외부에는 가변 Session·Renderer를 직접 노출하지 않는다.

### 10.1 결과
모든 입력 요청은 동기적인 `Phase2OrchestrationResult` 또는 동등 결과로 반환한다.

결과에는 최소한 다음 의미가 있어야 한다.

- Accepted / Rejected
- Failure reason
- 새 Painted count
- ScoreDelta
- Milestone
- Clear 전환
- Visual submission 결과

### 10.2 Visual 예외
GPU·Shader 경로에서 예외가 발생해도 Logic 상태와 실패 사유를 명시적으로 유지한다. 조용한 누락을 허용하지 않는다.

### 10.3 Recovery
- RT 유실 시 새 RT 생성
- Stamp History를 시각적으로만 replay
- replay는 Grid·Score·Milestone·Clear를 재발생시키지 않는다.
- Replay chunk: 4,096
- Clear 상태 복구 정책은 완전 Mask 또는 History replay 중 구현 계약을 따른다.

### 10.4 Batch
- 안전 상한: 65,536
- 입력을 조용히 버리지 않는다.
- 초과·실패 시 명시적 결과를 반환한다.

---

## 11. GameFlow 및 Lifecycle 계약

### 11.1 Prepare
- 새 Orchestrator는 Prepare에서만 생성한다.
- Difficulty를 공용 GameRunContext에서 받는다.
- Grid·Mask·History·CompletionFill을 초기화한다.
- Input OFF
- Phase2Root inactive 또는 전환 전 비활성 상태
- Ready 여부를 명시한다.

### 11.2 Activate
- 기존 Orchestrator를 재사용한다.
- Phase2Root active
- 기존 현재 Mask를 Presenter에 Bind
- 명시적으로 입력을 켜기 전까지 Input OFF
- Deactivate 후 Reactivate에서도 Progress·Score·Mask를 보존한다.

### 11.3 Deactivate
- Input OFF
- Pointer 취소
- Running false
- Presenter binding과 Runtime Material 정리
- Phase2Root inactive
- Orchestrator·Mask·Logic·Score는 Dispose하지 않는다.

### 11.4 OnDisable
- 중복 안전한 입력·Presenter 정리만 한다.
- Runtime Dispose 금지
- Mask Release 금지
- Progress Reset 금지

### 11.5 OnDestroy
- 소유 Orchestrator와 GPU Resource를 Dispose한다.
- 중복 호출에 안전해야 한다.

---

## 12. Pointer 입력 계약

Phase2PointerInputController는 다음 Event를 지원한다.

- PointerDown
- BeginDrag
- Drag
- PointerExit
- PointerUp
- EndDrag
- Focus loss
- Application pause
- Input disable
- OnDisable

### 12.1 단일 Pointer
- 첫 번째 활성 Pointer ID만 사용한다.
- 다른 Pointer ID는 무시한다.
- 아이템 UI 입력은 별도 경로다.

### 12.2 Stroke 시작
- PointerDown에서 첫 Stamp
- 같은 Pointer의 Drag만 이어 받는다.

### 12.3 PointerExit
PointerExit는 Stroke를 취소하지 않는다.

다음 상태를 보고 취소 여부를 추론하지 않는다.

- pointerPress
- rawPointerPress
- dragging
- eligibleForClick
- pointerEnter
- pointerCurrentRaycast

실제 InputSystemUIInputModule에서는 Exit 시점 값이 구현·상황에 따라 달라질 수 있다.

### 12.4 Stroke 종료
공식 종료 경로:

- 같은 Pointer의 PointerUp
- 같은 Pointer의 EndDrag
- Input OFF
- Deactivate
- OnDisable
- Application focus loss
- Application pause

중복 종료는 안전하게 무시한다.

---

## 13. Presenter와 자동 마감

### 13.1 Presenter 소유권
Presenter가 소유하는 것:

- Runtime Material instance
- Graphic binding
- Completion animation state

Presenter가 소유하지 않는 것:

- Orchestrator의 Mask RenderTexture

따라서 Presenter는 외부 Mask를 Release·Destroy하지 않는다.

### 13.2 미바인딩 표시
- `_MaskBound=0`
- 초기 Cover는 완전 불투명 검정
- Mask가 Bind된 뒤 Shader가 실제 Mask를 읽는다.

### 13.3 Clear 순서
정확한 순서:

1. 99% threshold 도달
2. `IsCleared=true`
3. Input OFF
4. `PhaseCleared` 1회
5. Core 최종 `ScoreDelta` 반영
6. `CompletionFill` 0→1 시작
7. 0.4초 후 Cover 완전 투명
8. 완료 로그
9. `IsExitReady=true`
10. `PhaseExitReady` 1회

자동 마감은 `Time.unscaledDeltaTime`을 사용한다.

### 13.4 불변
자동 마감 동안 다음 값은 바뀌지 않는다.

- Logic Grid
- PaintedCellCount
- Phase Score
- Total Score
- Milestone
- Clear 발생 횟수

---

## 14. Scene 계약

현재 계층:

```text
Canvas
└─ Game_UI_General
   └─ Middle_GamePanel
      ├─ Game_Panel
      ├─ Phase1_FieldRoot
      └─ Phase2Root
         ├─ CementBaseLayer_Gray
         ├─ BlackCoverLayer
         └─ Phase2InputSurface
```

### Phase2Root
- 정확히 1개
- Parent: Middle_GamePanel
- sibling index 2
- initial inactive
- anchorMin/Max: (0.5, 0.5)
- pivot: (0.5, 0.5)
- anchoredPosition: (-7, -1)
- sizeDelta: (1250, 1250)
- scale: (1, 1, 1)

### GameSessionController
Phase 배열:

1. Phase1PhaseAdapter
2. Phase2PhaseAdapter

기타 보존 값:

- initialPhase: Phase1
- difficulty: Hard — Scene 직접 실행 기준
- Phase1Board generateOnStart: false
- Transition Overlay: 0.75초

### Scene 금지
- Phase1_FieldRoot RectTransform 변경 금지
- Game_Panel 변경 금지
- 기존 HUD 재배치 금지
- Phase2Root 중복 생성 금지
- Setup을 이미 정상 실행한 Scene에 불필요하게 재실행하지 않는다.

---

## 15. 핵심 코드 파일

### Stage 2 Logic
- `Phase2PaintConfig`
- `Phase2PaintPresets`
- `Phase2PaintGrid`
- `Phase2PaintProgressRules`
- `Phase2PaintSessionModel`
- `Phase2StampInterpolator`
- `Phase2LogicCoreValidation`

### Stage 3 Visual
- `Phase2PaintStamp`
- `Phase2PaintMaskRenderer`
- Paint Mask Brush Shader
- Paint Mask Composite Shader
- Visual Tech Validation

### Stage 4 Orchestration
- `Phase2PaintOrchestrator`
- `Phase2OrchestrationResult`
- Stamp History·Recovery 관련 Runtime
- Runtime Orchestration Validation

### Stage 5A Flow
- 공용 Difficulty·RunContext·Phase 계약
- `GameSessionController`
- `Phase1PhaseAdapter`
- `Phase2Stage5AFlowFoundationValidation`

### Stage 5B Scene Integration
- `Assets/Scripts/GameFlow/Runtime/Phase2PhaseAdapter.cs`
- `Assets/Scripts/Phase2/Runtime/Phase2MaskPresenter.cs`
- `Assets/Scripts/Phase2/Runtime/Phase2PointerInputController.cs`
- `Assets/Scripts/Phase2/Editor/Phase2Stage5BSceneSetup.cs`
- `Assets/Scripts/Phase2/Editor/Phase2Stage5BSceneIntegrationValidation.cs`
- `Assets/Shaders/Phase2/UI_Phase2BlackCoverMask.shader`
- `Assets/Materials/Phase2/Phase2BlackCoverMask.mat`
- `Assets/Scenes/INGAME.unity`

---

## 16. 현재 플레이 흐름

```text
Session Preparing
→ Phase 1 Prepare
→ READY/GO
→ Timer 시작
→ Phase 1 Clear/Score Finalize
→ Transitioning, Timer Pause
→ Overlay midpoint
→ Phase1Root OFF
→ Phase2Root ON
→ Phase 2 Input ON
→ 도포
→ 99% Clear
→ Input OFF
→ Score 1450 확정
→ 0.4초 Visual Completion
→ PhaseExitReady
→ Phase 3 검색
```

현재 Phase 3가 등록되지 않았으므로 마지막은 다음 안전 잠금으로 끝난다.

```text
Session remains Transitioning
Timer paused
All phase input disabled
Phase 2 reactivation 없음
```

이 빨간 오류는 현재 완료 범위에서는 예상 동작이다.

---

## 17. 검증 메뉴와 최종 기준선

- Stage 5B Scene Integration: `157/157`
- Stage 5A Integrated Flow: `118/118`
- Logic Core: `36/36`
- Visual Tech Core: `34/34`
- Runtime Orchestration: `69/69`

기존 회귀:

- Pre-Phase2 Models: `11/11`
- Timer: `31/31`
- Request Icons: `12/12`
- Risk Boundaries: `12/12`
- Final Safety: `8/8`
- Phase1 Matrix: `120/120`
- Phase1 Stress: `1200/1200`

실제 Play Mode:

- Phase1→Phase2 정상
- PointerExit 후 재진입 Stroke 유지
- 99% 후 자동 마감
- Phase2 점수 1450
- Phase3 미등록 안전 잠금

---

## 18. 이미 해결된 주요 결함

### 자동 마감 누락
과거에는 Clear 후 PhaseExitReady가 즉시 발생해 검은 빈틈이 남았다. 현재는 0.4초 CompletionFill 후 ExitReady가 발생한다.

### PointerExit 취소
과거에는 PointerEventData의 press 상태를 추론해 Exit에서 Stroke를 취소했다. 현재 Exit는 취소하지 않는다.

### 경계 밖 Drag 거부 시 Stroke 취소
과거에는 Core가 비도색 구간을 거부하면 Controller가 Stroke를 끝낼 수 있었다. 현재 활성 Pointer는 유지된다.

### 잘못된 Mask identity Validation
초기 Mask와 현재 Mask를 동일해야 한다고 검사했으나 Ping-Pong 구조상 잘못이었다. 현재는 Deactivate 직전 현재 Mask를 저장해 보존을 검사한다.

---

## 19. 후속 개발 변경 금지 계약

- 128×128 Logic과 1024×1024 Visual을 합치지 않는다.
- Progress를 GPU Readback으로 계산하지 않는다.
- 99% threshold를 바꾸지 않는다.
- 자동 마감을 Logic 100% 조작으로 구현하지 않는다.
- Core Score를 UI나 Feedback에서 재계산하지 않는다.
- Presenter가 Mask RT를 Release하지 않는다.
- Deactivate에서 Runtime을 Dispose하지 않는다.
- PointerExit에서 Stroke를 끊지 않는다.
- 좌표를 Clamp하지 않는다.
- Phase 1 UI·RectTransform을 임의 변경하지 않는다.
- Shader Shared Material을 Runtime에서 직접 변경하지 않는다.
- 검증 통과를 위해 Assertion을 삭제·무조건 true로 바꾸지 않는다.
- 사용자가 승인하기 전 commit/push하지 않는다.

---

## 20. 다음 작업 시작 전 체크

1. 현재 Branch와 HEAD 확인
2. Working tree clean 확인
3. Unity 6000.3.19f1 확인
4. 이 문서의 Phase 2 완료 Commit ID 확인
5. Stage 5B 157/157 기준선 확인
6. Setup을 재실행하지 않음
7. Phase 3 전에 Full Game 자동검수 시스템을 먼저 구축
8. Phase 3 메커니즘은 설계 확정 전 생성하지 않음
