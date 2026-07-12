# Phase 2 착수 전 최종 안전 보강 보고서

## 1. 작업 목표

Production Board mutation 우회와 Phase 전환 midpoint 실패의 잘못된 성공 복구를 차단했다.

## 2. 작업 전 Git 상태

- Branch: main
- HEAD: 98a72c0
- 공용 프레임워크, Request 아이콘, 위험 검토 변경이 미커밋 상태
- 기존 변경 폐기, commit, push, package/resource 변경 없음

## 3. 기존 위험

1. Phase1BoardController.TryHit 직접 호출은 Phase1InputController를 우회해 비Playing 상태에서도 HP를 변경할 수 있었다.
2. midpoint 예외를 Overlay가 기록만 하고 계속 진행해 준비되지 않은 다음 Phase에서 Timer와 입력을 재개할 수 있었다.

## 4. TryHit 직접 호출 분석

기존 Board TryHit에는 Session 또는 inputEnabled 검사가 없었다. GameScoreController만 점수를 거부했으므로 HP, DamageState, 파괴, Clear가 점수와 불일치할 수 있었다.

## 5. Production 타격 게이트 수정

TryHit와 TryHitDetailed는 실제 ApplyDamage 전에 다음을 모두 요구한다.

- GameSessionController 존재
- Session CanAcceptGameplayInput, 즉 Playing
- Phase1InputController 존재
- InputEnabled true

하나라도 거짓이면 StateBlocked를 반환하며 Tile과 Board를 변경하지 않는다.

## 6. 검증 전용 타격 경로

Production public ForceHit/bypass API는 추가하지 않았다. 기존 Smoke는 Session을 Playing으로 설정하고 Phase 입력을 활성화한 뒤 동일 Production TryHit를 사용한다. DebugAllowImmediateHit는 debounce 시간만 조절하며 Session/Input gate는 우회하지 않는다.

## 7. 상태별 HP 변경 차단 결과

- Ready: StateBlocked, HP/Damage/Score 동일, Clear 0
- Playing + Input Disabled: StateBlocked, HP 동일
- Playing + Input Enabled: Succeeded, HP -1, Score +10
- Transitioning: StateBlocked, HP/Score 동일, Clear 0
- Expired: StateBlocked, HP/Damage/Score 동일, Clear 0, Input false
- Completed: StateBlocked, HP/Damage/Score 동일, Clear 0, Input false

Phase1HitResult는 Succeeded, StateBlocked, InvalidTarget, AlreadyDestroyed, DamageRejected를 구분한다.

## 8. Transition Callback 실패 분석

기존 Overlay는 midpoint Action 예외를 로깅하고 배너 퇴장과 successful completion을 계속했다. 핵심 Phase 준비 실패와 단순 애니메이션 중단을 구분하지 못했다.

## 9. 전환 성공/실패 정책

PhaseTransitionResult는 None, Succeeded, Interrupted, Failed, Rejected를 구분한다. midpoint는 Func<bool> 기반 Checked API를 제공하며 기존 Action API는 성공 반환 wrapper로 호환된다.

- Succeeded: Session FinishTransition, Timer Resume, Input true, completed 성공 callback
- Failed: Session Transitioning 유지, Timer Paused, Input false, completed 성공 callback 금지
- Interrupted: midpoint 전/애니메이션 중단 복구로 Session Playing, Timer/Input 복구, completed 성공 callback 금지
- Rejected: Expired/Completed/중복/참조·상태 불충족, callback 0

## 10. midpoint 실패 결과

명시 false와 예외 모두 TryExecuteMidpoint에서 false로 판정된다. 예외 정보는 LastError에 보존한다.

실제 Controller 실패 결과:

- LastResult Failed
- completed 0
- Session Transitioning
- Timer Paused
- Input false
- Overlay busy false
- Error 1회

성공 Playing으로 복귀하지 않는다.

## 11. Overlay Disable 정책

midpoint 실패와 외부 Disable은 별도 결과다. 외부 Disable은 Interrupted이며 Coroutine reference를 정리한다. 검증 결과 completed 성공 callback 0, Session Playing, Timer Running, Input true, busy false였다.

## 12. completed 예외 처리

midpoint와 핵심 전환이 성공한 뒤 Session/Timer/Input을 먼저 정상 복구한다. completed 후처리 예외는 LastError와 Error 로그로 분리한다.

검증 결과 LastResult Succeeded, completed 호출 1회, Session Playing, Timer Running, Input true, postError true였다. 중복 호출은 없다.

## 13. 상태 불변식 검증

- Transition Failed + Playing/Timer Running/Input true 조합 없음
- Transitioning + Timer Running 없음
- Ready/Transitioning/Expired/Completed의 HP 변경 없음
- Expired/Completed 입력 false
- Expired Transition 요청 Rejected, midpoint/completed 0
- 성공 전환만 Playing/Timer Running/Input true

## 14. 자동 검증 목록

- Session: 11/11
- Timer: 31/31
- Request: 12/12
- RiskReview: 12/12
- FinalSafety contract: 8/8
- 실제 Hit Gate 상태 6종
- Transition Succeeded/Failed/Interrupted/Rejected
- midpoint false/exception
- completed exception

## 15. 전체 회귀 결과

- Matrix: 120/120, 900 tiles
- Stress: 1200/1200, 9000 tiles
- 합계: 1320/1320 보드, 9900 tiles
- HP 위반/불일치: 0/0
- Damage State: 30/30
- Easy Smoke: 36 hits, 960/960
- Normal Smoke: 54 hits, 1190/1190
- Hard Smoke: 61 hits, 1410/1410
- Smoke 3/3, clear event 각 1회
- Console Error/Warning 최종 0/0
- Missing Script/Broken Prefab 0/0

## 16. Scene·Setup 결과

PrePhase2Setup이 Board.sessionController를 명시적으로 연결한다. Setup 2회 후 중복 0, Scene 재오픈 후 참조 유지, READY/GO 값과 Request Sprite 유지, 기존 RectTransform 변경 0, isDirty=false다.

## 17. 변경 파일

- Phase1HitResult.cs: 상세 타격 결과
- Phase1BoardController.cs: Session/Input 이중 mutation gate
- PhaseTransitionResult.cs: 전환 결과 분류
- PhaseTransitionOverlay.cs: midpoint bool/exception 판정과 중단 분리
- PhaseTransitionController.cs: 실패 잠금, 성공/중단/거부 정책과 진단
- PrePhase2Setup.cs: Board Session 참조
- PrePhase2Validation.cs: FinalSafety 8개 계약 검사
- INGAME.unity: Board Session 직렬화 참조
- PRE_PHASE2_RISK_REVIEW_REPORT.md: TryHit 위험 해결 상태 반영
- 본 보고서

## 18. 남은 위험

Phase 2 실제 Adapter가 추가되면 midpoint의 true 반환 전에 IsReady 등 실제 준비 조건을 검증해야 한다. 현재는 Phase 2가 없어 검증 Callback의 bool/예외로 성공 여부를 판정한다.

## 19. 최종 판정

**PASS**

## 20. Phase 2 착수 인수인계 원칙

Phase 2는 Phase 1 내부 구현에 종속시키지 않고 GameSessionController, GameTimerController, GameScoreController, PhaseTransitionController, PhaseHUDPresenter, GameRequestContext 및 IGamePhase 계약에 연결되는 독립 Controller/Adapter로 구현한다.

Phase 1→2와 Phase 2→3의 실제 통합은 반드시 성공 여부를 반환하는 Checked Transition 경로를 사용한다. midpoint에서 초기화 메서드를 호출했다는 사실만으로 성공하지 않으며, Controller·Adapter·필수 참조·데이터·플레이 영역·입력 대상이 준비되고 IsReady 또는 동등한 실제 준비 조건이 true인 경우에만 성공을 반환한다.

midpoint 실패 시 Session Transitioning, Timer Paused, 입력 비활성의 안전 잠금을 유지하고 completed 성공 Callback을 호출하지 않는다. 실패 복구 방식은 Phase 2 생성 구조가 확정된 작업 지시서에서 재시도, 정리, 롤백, 안전 종료 중 하나로 결정한다.

세부 인수인계는 PHASE2_IMPLEMENTATION_DIRECTION.md를 따른다.
