# Phase 2 구현 방향 및 전환 통합 원칙

## 1. 목적

이 문서는 Phase 2 실제 메커니즘을 추측해 구현하지 않고, 현재 검증된 공용 프레임워크에 Phase 2를 안전하게 통합하기 위한 필수 계약과 순서를 기록한다.

## 2. 현재 공용 프레임워크 상태

- Session 11/11 PASS
- Timer 31/31 PASS
- Request 12/12 PASS
- Risk Review 12/12 PASS
- Final Safety 8/8 PASS
- Phase 1 1320/1320 보드, 9900타일
- HP 위반/불일치 0/0
- Damage State 30/30
- 입력·점수 Smoke 3/3
- Console Error/Warning 0/0
- Missing Script/Broken Prefab 0/0

Phase 2는 다음 공용 시스템을 재사용한다.

- GameSessionController
- GameTimerController
- GameScoreController
- PhaseTransitionController
- PhaseHUDPresenter
- GameRequestContext
- IGamePhase 또는 동등한 공통 계약

## 3. Phase 2 공통 계약

Phase 2 Controller 또는 Adapter는 최소한 다음 생명주기를 제공해야 한다.

- StartPhase
- StopPhase
- SetInputEnabled
- IsRunning
- IsCleared
- PhaseCleared

Phase 2는 Phase 1 Controller 내부에 결합하지 않는다. 필요하면 Phase 2 전용 Adapter가 공통 계약을 구현한다.

Phase 2 Clear 이벤트는 마지막 게임 처리와 모든 점수 반영 이후 정확히 1회 발생해야 한다.

## 4. Phase 1→2 전환 순서

1. Phase 1 마지막 유효 행동 처리
2. 마지막 타격 점수 반영
3. 파괴 점수 반영
4. Phase 1 클리어 점수 반영
5. Phase1Cleared 발생
6. Checked Transition 시작
7. Session Transitioning
8. Phase 1 입력 차단
9. Timer Pause
10. PHASE 1 CLEAR!! 배너 진입
11. midpoint에서 Phase 2 준비
12. Phase 2 실제 준비 성공 검증
13. Phase HUD를 Phase 2로 변경
14. Phase 2 Root 표시 및 StartPhase
15. 성공 반환
16. 배너 퇴장
17. Timer Resume
18. Phase 2 입력 활성화
19. Session Playing

Phase 2 준비가 검증되기 전에 Phase 1 Root를 영구 제거하지 않는다.

## 5. Checked Transition 사용 원칙

실제 Phase 통합의 주 경로는 PlayPhaseClearTransitionChecked 또는 동등한 성공 반환 API다. 단순 Action 기반 API는 실제 Phase 2 준비의 주 경로로 사용하지 않는다.

성공 결과는 다음을 모두 의미해야 한다.

- midpoint 핵심 작업 성공
- 다음 Phase 준비 완료
- 배너 정상 완료
- Timer Resume 가능
- 다음 Phase 입력 활성화 가능
- Session Playing 가능

completed Callback은 로그, 사운드 요청, 통계 등 성공 이후 후처리에만 사용한다. Phase 생성이나 필수 초기화를 completed에 넣지 않는다.

## 6. Phase 2 IsReady 원칙

초기화 요청 전, 초기화 진행 중, 플레이 시작 가능을 구분해야 한다. 실제 이름은 IsReady, InitializationSucceeded, CanStartPhase 등 Phase 2 구조에 맞춰 결정한다.

midpoint 성공 전 최소 확인 조건:

- Phase 2 Controller 존재
- Phase 2 Adapter 존재
- 필수 Serialized Reference 연결
- Phase 2 데이터 생성 완료
- 플레이 영역 준비 완료
- 입력 대상 생성 완료
- HUD 설정 가능
- StartPhase 호출 가능
- IsReady 또는 동등 조건 true
- 필수 초기화 오류 없음

초기화 함수가 반환됐다는 사실만으로 성공하지 않는다. 현재는 Phase 2 구조가 없으므로 IsReady 구현을 미리 만들지 않는다.

## 7. Root와 HUD 교체 시점

권장 순서:

1. Phase 1 입력 차단
2. Phase 2 준비 시작
3. Phase 2 필수 객체 생성
4. Phase 2 준비 상태 검증
5. Phase HUD를 Phase 2로 변경
6. Phase 2 Root 표시
7. Phase 1 Root 정리 또는 비활성화
8. Phase 2 StartPhase
9. midpoint 성공 반환

HUD는 준비 검증 이후 배너가 중앙을 가리는 시점에 PHASE 2와 하양/파랑/하양 Dot 상태로 변경한다. Phase 2 설명은 기존 Serialized String을 사용하며 최종 문구를 코드에 하드코딩하지 않는다.

초기화 실패 시 HUD가 PHASE 2로 남지 않도록 준비 검증 후 HUD를 적용하거나 명시적인 복원 절차를 둔다. Phase 1 Root의 정확한 비활성화 시점은 Phase 2 생성 구조 확정 후 결정한다.

## 8. Timer·Score·Input 규칙

### Timer

- 전환 중 실제 내부 RemainingSeconds를 보존하고 Pause
- Phase 2 시작 시 90초 Reset 금지
- 표시 정수로 내부 시간 보정 금지
- 전환 시간 동안 감소 금지
- midpoint 성공 및 다음 Phase 준비 완료 후에만 Resume
- midpoint 실패 시 Resume 금지

### Score

- GameScoreController만 사용
- Phase 1 누적 점수 유지
- Phase 전환 시 Reset 금지
- Transitioning 중 신규 점수 금지
- Phase 2 점수를 기존 총점 위에 누적
- Phase별 시간 보너스 금지
- 남은 시간 보너스는 전체 게임 완료 후 한 번만 검토

### Input

Phase 2의 실제 Production 상태 변경 진입점도 다음을 모두 검사한다.

- GameSessionState == Playing
- Phase 2 InputEnabled == true

Ready, Transitioning, Expired, Completed, Playing+Input Disabled에서는 게임 상태, 진행도, 점수, Clear 판정, Phase 이벤트가 변경되면 안 된다. 입력 Controller만 차단하는 구조로 끝내지 않으며 public 검증 우회 API를 만들지 않는다.

## 9. Request 유지 규칙

Phase 2는 Request 상태를 새로 만들지 않는다. GameRequestContext의 현재 Normal 또는 Sudden 상태, Request_Text, Request_Icon과 향후 보정 데이터를 세션 전체에서 유지한다.

Phase 2가 Request 영향을 받을 경우에도 공용 Context를 읽으며 Phase 전용 Request 복사본을 소유하지 않는다.

## 10. 전환 실패 정책

다음은 Failed 결과다.

- 필수 참조 누락
- Phase 2 생성·초기화·데이터 생성 실패
- IsReady false
- midpoint 예외
- midpoint 명시적 실패

Failed 상태에서는 다음을 유지한다.

- Session Transitioning
- Timer Paused
- 입력 비활성
- completed 성공 Callback 0회
- LastError에 원인 보존

Phase 준비 실패와 Session Playing, Timer Running, 입력 활성의 조합은 금지한다. 잘못된 자동 진행보다 안전 잠금을 우선한다.

completed 후처리 예외는 핵심 Transition 성공과 분리한다. Phase 준비가 이미 성공했다면 게임 상태를 영구 잠그지 않고 LastError로 기록한다.

## 11. 실패 복구 결정 항목

Phase 2 실제 구조가 확정되면 다음 중 하나를 작업 지시서에서 선택해야 한다.

- Phase 2 초기화 재시도
- 부분 생성 객체 정리 후 재시도
- 이전 Phase로 안전 롤백
- 안전한 게임 종료
- Scene 재시작
- 오류 안내 후 메인 화면 이동

현재 단계에서는 복구 정책을 구현하지 않는다. Phase 1 Root 비활성화 시점, 부분 생성 객체의 소유권, 재시도 가능성을 먼저 분석한다.

## 12. Phase 2 지시서 필수 포함 항목

- Phase 2 핵심 게임 규칙과 데이터 구조
- Phase 2 Controller와 Adapter
- 입력 경로와 Production mutation gate
- 점수 규칙과 클리어 조건
- IsReady의 구체적 조건
- Phase 1→2 midpoint 처리
- Phase 1 Root 비활성화 시점
- Phase 2 Root 활성화 시점
- HUD 변경 시점
- Timer Resume와 입력 활성화 시점
- 초기화 실패 객체 정리
- 전환 실패 복구 정책
- Phase 2 자동 검증
- Phase 1 전체 회귀 검증

메커니즘이 확정되기 전에 세부 구현을 추측하지 않는다.

## 13. 현재 구현하지 않을 항목

- Phase 2·3 실제 메커니즘
- Item
- Settings
- 점수·타격 팝업
- 추가 파티클과 피드백
- 결과 화면
- Request 실제 효과
- 최종 시간 보너스

이 기능들은 Phase 2 핵심 메커니즘과 분리된 후순위 작업으로 유지한다.
