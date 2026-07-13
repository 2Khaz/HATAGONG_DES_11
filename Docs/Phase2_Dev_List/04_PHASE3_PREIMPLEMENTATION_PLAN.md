# Phase 3 착수 전 확정 및 구현 계획

## 1. 현재 확정된 사실

- 게임은 총 3개 Phase로 구성된다.
- 전체 제한 시간은 90초다.
- 제한 시간 안에 Phase 1, 2, 3을 모두 Clear하면 승리한다.
- Phase 2는 최종 자동 마감 후 `PhaseExitReady`를 발생시킨다.
- 현재 Phase 3는 등록되지 않았다.
- 현재 Phase 2 종료 후:
  - Session은 Transitioning 유지
  - Timer Pause
  - 모든 Phase Input OFF
  - Phase 2 재활성화 없음
- 이는 Phase 3가 없는 동안의 의도된 안전 잠금이다.

---

## 2. 아직 확정되지 않은 핵심

다음은 자료가 없으므로 Codex가 임의로 만들면 안 된다.

- Phase 3의 작업 주제
- 핵심 조작
- Board 또는 오브젝트 구조
- 난이도 차이
- Clear 조건
- 실패 조건
- 목표 소요 시간
- 점수 구조
- Item 효과
- Request 효과
- 시각 연출
- 사운드·진동
- Phase 3 완료 후 결과 화면
- Retry·중단 정책

---

## 3. 코딩 전에 사용자가 확정할 설계 질문

### 플레이
1. Phase 3에서 플레이어가 무엇을 하는가?
2. 탭, 드래그, 배치, 선택 중 어떤 입력인가?
3. 한 손 세로 모바일에서 가능한가?
4. Phase 3 목표 시간은 몇 초인가?
5. 난이도별 차이는 무엇인가?

### 판정
1. Clear 수치 또는 조건은 무엇인가?
2. 중복 입력은 어떻게 처리하는가?
3. Board 밖 입력은 허용하는가?
4. 완벽하지 않아도 Clear되는 보정이 있는가?
5. Timeout 시 중간 결과를 저장하는가?

### 점수
1. 기본 행동 점수는 얼마인가?
2. Milestone이 있는가?
3. Clear Bonus는 얼마인가?
4. 시간 보너스가 있는가?
5. Phase 2 최대 1450과 어떤 비중을 갖는가?

### Item·Request
1. Phase 3 전용 Item은 무엇인가?
2. 기존 보유 Item을 공유하는가?
3. Request Normal/Sudden이 무엇을 바꾸는가?
4. Item 사용이 점수에 영향을 주는가?

### 결과
1. Phase 3 Clear 즉시 승리인가?
2. 완료 연출 시간은?
3. Timer는 언제 멈추는가?
4. 최종 점수와 별·등급은 어디서 확정하는가?
5. 결과 화면으로 넘어갈 때 Overlay를 재사용하는가?

---

## 4. Phase 3 착수 Gate 0 — 자동검수 시스템

Phase 3 코드를 만들기 전에 다음을 구축한다.

- FullGameIntegrationRunner
- 실제 Play Mode Driver
- EventSystem 기반 Pointer 시뮬레이터
- Run ID 기반 JSON 결과 기록
- 최신 실행 구간만 분리하는 Log Collector
- Codex 반복 수정·재검수 규칙
- 최대 반복 횟수와 중단 안전장치

상세는 `05_FULL_GAME_AUTOMATED_VALIDATION_PLAN.md` 참조.

Gate 0 완료 조건:

- Phase 1→2 자동 통합 실행
- Phase 2 PointerExit 재진입 자동 재현
- Phase 2 99% 자동 마감 검증
- Phase 3 부재 안전 잠금 자동 확인
- 결과 JSON 생성
- 과거 Editor.log와 현재 Run 구분
- 사용자 수동 조작 없이 성공·실패 판정

---

## 5. Stage P3-1 — 읽기 전용 조사

목표: 기존 Scene과 공용 시스템에 Phase 3를 넣을 정확한 위치를 찾는다.

조사:

- INGAME Scene hierarchy
- Middle_GamePanel sibling 구조
- Phase1Root·Phase2Root 크기·위치
- GameSessionController phase 배열
- Transition Overlay
- Timer Pause/Resume
- Score Controller
- Request·Item 연결 지점
- Result 화면 또는 미구현 상태
- 사용 가능한 리소스
- Input System
- 모바일 Safe Area

산출물:

- `PHASE3_STAGE1_PROJECT_INVESTIGATION_REPORT.md`
- `PHASE3_DEFERRED_WORK.md`
- 변경 없음

---

## 6. Stage P3-2 — 순수 Logic Core

Phase 3의 실제 규칙이 확정된 뒤 Scene·UI 없이 구현한다.

필수 원칙:

- 순수 C# Model
- Unity Object 의존 최소화
- Difficulty 외부 주입
- 명시적 Mutation Gate
- 중복 Clear 방지
- ScoreDelta 결과 반환
- deterministic validation 가능
- Runtime 경로 LINQ·불필요 allocation 회피
- Phase 1·2 Runtime 수정 없음

완료 조건:

- Logic Validation 전부 통과
- 경계·중복·실패·Clear·Score 검증
- 공용 회귀 통과

---

## 7. Stage P3-3 — 시각 기술 코어

Phase 3가 별도 시각 기술을 요구할 때만 분리 구현한다.

예:

- Object Pool
- Board Renderer
- Animation State
- Shader
- Particle
- Layout
- GPU Resource

원칙:

- Logic 결과가 진실
- 시각 시스템은 판정을 소유하지 않음
- 복구·Dispose·OnDisable 계약 명시
- 모바일 fallback
- Scene 연결 전 독립 Validation

---

## 8. Stage P3-4 — Runtime Orchestration

Logic, Visual, Input 요청을 하나의 Production 경로로 묶는다.

필수:

- 동기적 결과 또는 명시적 async 계약
- 실패 사유
- Input Gate
- ScoreDelta
- Clear 1회
- Resource ownership
- Deactivate/Reactivate
- Recovery
- public mutable Core 노출 금지

---

## 9. Stage P3-5 — Adapter와 Scene 통합

필요 요소:

- `Phase3Root`
- `Phase3PhaseAdapter`
- 실제 Input Controller
- Presenter
- Setup
- Validation
- Material·Shader·Asset
- INGAME Scene 등록

GameSessionController 배열:

1. Phase1
2. Phase2
3. Phase3

전환:

```text
Phase2 ExitReady
→ Phase3 Prepare 확인
→ Transition Overlay
→ midpoint Root 교체
→ Phase3 Activate
→ Input ON
```

금지:

- Phase2 Adapter에서 Phase3 GameObject 직접 찾기
- Phase2 코어가 Phase3 규칙 참조
- 가짜 Phase3 Ready
- Scene의 기존 UI 무관 변경

---

## 10. Stage P3-6 — Phase 3 완료와 게임 결과

Phase 3가 최종 Phase이므로 PhaseExitReady 이후 “다음 Phase 검색”이 아니라 Game Complete 정책이 필요하다.

확정해야 할 Controller 책임:

- 최종 Timer 정지
- 모든 Input OFF
- 최종 Score 확정
- Victory State
- 결과 Overlay/Scene
- 최고 점수·보상 저장
- Retry
- Lobby 이동
- Timeout과 동시 Clear race
- 이벤트 중복 방지

이 단계에서 `GameSessionController`의 마지막 Phase 처리 계약을 명시적으로 확장한다.

---

## 11. 검증 계획

### Logic
- Phase 3 규칙 전 항목
- Difficulty
- Score
- Clear/Fail
- 중복 요청
- invalid input

### Lifecycle
- Prepare
- Activate
- Deactivate
- Reactivate
- OnDisable
- OnDestroy
- Resource cleanup

### Integration
- Phase 2→3
- Timer Pause/Resume
- Overlay midpoint
- Root 교체
- Score visibility
- Phase 3 Clear→Victory
- Timeout race
- Retry

### Full Game
- Phase1→2→3→Result
- 전체 90초
- 총점
- Request
- Item
- 모바일 입력 시뮬레이션
- JSON 결과

---

## 12. Phase 3 착수 시 금지

- 메커니즘 확정 전 Production 코드 생성
- Phase 3가 없다는 오류를 숨기기 위한 빈 Adapter 등록
- 기존 Phase 2 99%·1450·0.4초 계약 변경
- Phase 1·2 코어에 Phase 3 임시 분기 추가
- 결과 화면이 없는데 성공 상태를 임시 로그로만 종료
- 검증 없이 Scene Setup 반복
- 사용자가 승인하지 않은 commit/push
