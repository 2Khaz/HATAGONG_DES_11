# Phase 2 착수 전 위험 검토 및 READY/GO 템포 보고서

## 1. 작업 목표

공용 프레임워크의 상태·이벤트·Overlay 생명주기를 재검토하고 READY/GO 템포를 조정한 뒤 전체 회귀를 수행했다.

## 2. 작업 전 Git 상태

- Branch: main
- HEAD: 98a72c0
- 이전 공용 프레임워크 및 Request 아이콘 변경이 미커밋 상태
- 기존 변경 폐기, commit, push, package 변경 없음

## 3. 현재 기준선

Session 11/11, Timer 31/31, Request 12/12, Phase 1 1320/1320 보드·9900 tiles를 기준으로 검토했다.

## 4. READY/GO 시간 변경

- READY: 0.85초
- READY→GO 빈 간격: 0.15초
- GO: 0.45초
- Fade: 0.20초
- 합계: 약 1.65초

네 값은 Serialized Field이며 Setup과 Scene 재오픈 뒤 0.85/0.15/0.45/0.2로 유지됐다. 빈 간격에는 메시지만 빈 문자열이고 Dim/raycast/Ready 상태가 유지된다. Timer와 입력은 Fade 완료 Callback 뒤 시작한다.

## 5. 시작 연출 검토

첫 상태는 READY..., Session Ready, Timer Running=false, Remaining=90, Input=false, Score=0, raycast=true였다. 진행 중 두 번째 Play 요청은 차단됐다. 비활성화 시 sequence가 정리되고 재활성화 후 재호출 true, 즉시 중복 호출 false를 확인했다.

## 6. 이벤트 구독 검토

- RequestPresenter와 Phase1PhaseAdapter: OnEnable 구독, OnDisable 해제
- GameTimerPresenter: 구독 대상 identity 검사로 Start/OnEnable 중복 방지
- GameSessionController: TimerExpired를 Start에서 1회 구독하고 OnDestroy 해제
- static gameplay event 없음
- 모델, Shuffle, history는 인스턴스 소유

Domain Reload 비활성에서도 Scene 인스턴스 파괴 시 구독이 해제되며 static event 잔존 위험은 확인되지 않았다.

## 7. Session 상태 경계 검토

Session 모델 11/11과 위험 경계 검증으로 Ready→Transitioning, Expired→Transitioning/Playing, Completed→Playing을 차단했다. Transition 중 두 번째 요청도 차단된다. Timer가 먼저 Expired로 확정되면 BeginTransition은 실패한다. 정상 전환은 Timer를 먼저 Pause하므로 전환 중 자연 만료가 발생하지 않는다.

## 8. Timer 검토

시작 연출 전체에서 90을 유지하고 Fade 완료 후 Start한다. Transition은 내부 double 값을 보존한다. Pause/Resume 중복과 Expired terminal 정책은 Timer 31/31에서 유지됐다. Time_Value 소유자와 Timer Controller는 각각 1개다.

## 9. 입력 게이트 검토

Ready/Transitioning/Expired에서 Phase1InputController.inputEnabled=false이며 active pointer도 해제된다. Playing에서만 true다. Overlay도 raycast를 차단한다.

직접 Phase1BoardController.TryHit 호출 위험은 후속 안전 보강에서 해결했다. Board mutation 진입점 자체가 Session Playing과 Phase 입력 Enabled를 모두 확인한다.

## 10. Score 검토

Score_Value는 GameScoreController만 소유한다. Ready/Transitioning/Expired/Completed에서 AddScore가 거부된다. Board Regenerate는 Reset하지 않으며 새 Session 시작만 Reset한다. 마지막 타격·파괴·클리어 후 PhaseCleared 순서를 유지했다.

- Easy: hits 38, score 980/980
- Normal: hits 55, score 1200/1200
- Hard: hits 66, score 1410/1410
- clear event 각 1회

## 11. Request 검토

Scene 재오픈 뒤 Request_Icon 기본 Sprite와 normalIcon은 Img_icon_normal, suddenIcon은 Img_icon_sudden으로 유지됐다. null 슬롯은 현재 Sprite를 덮어쓰지 않고 최초 1회만 Error를 기록하며 Setup/Editor 검증에서는 실패한다. INGAME에는 누락이 없어 Warning/Error가 없다.

## 12. Phase HUD 검토

Phase 1/2/3은 각각 현재 Dot 하나만 파랑이고 나머지는 흰색이다. 모든 Dot active/enabled/alpha 1이다. Phase 2·3 빈 설명은 이전 문구를 남기지 않는다. 기존 HUD RectTransform 변경은 없다.

## 13. Transition 검토

위치는 Canvas와 Banner 폭 기반이며 하드코딩 해상도가 없다. midpoint/completed는 1회 wrapper로 보호된다. Callback 예외는 Error로 기록하고 Coroutine을 계속 진행한다. 외부 Disable 시 completion recovery가 실행되어 실제 검증에서 Session Playing, Timer Running, Input true로 복구됐다.

## 14. Setup 멱등성 검토

Setup을 연속 2회 실행하고 INGAME Scene을 다시 로드했다. Start/Transition Overlay, Session, Timer, RequestPresenter는 각각 1개였다. 시간값과 Request Sprite 참조가 유지됐고 Scene isDirty=false였다. 기존 RectTransform 누적 변경은 없다.

## 15. 발견한 실제 결함

1. Start Overlay가 외부 Disable되면 Coroutine reference가 남아 재호출이 영구 차단될 수 있었다.
2. Transition Overlay가 중간 Disable되거나 Callback이 예외를 던지면 Session/Timer/Input이 잠길 수 있었다.
3. READY와 GO 사이 별도 빈 간격이 없었다.

## 16. 수정한 내용

- Start Overlay OnDisable 정리와 bool Play 중복 거부 결과 추가
- Transition Overlay 중단 recovery, callback 예외 격리, 완료 중복 방지
- READY 0.85 / gap 0.15 / GO 0.45 / Fade 0.20 적용
- Request null 누락 최초 1회 명시 오류

## 17. 추가 자동 검증

RiskReview 12/12 PASS: 단일 컴포넌트, 4개 시간값, Request 참조, Ready/Expired 전이 경계를 검사한다. Play Mode에서 Start 중복·Disable/Enable 재호출과 Transition 중단 복구를 별도 확인했다.

## 18. 전체 회귀 결과

- Session: 11/11
- Timer: 31/31
- Request: 12/12
- RiskReview: 12/12
- Matrix: 120/120, 900 tiles
- Stress: 1200/1200, 9000 tiles
- 합계: 1320/1320, 9900 tiles
- HP 위반/불일치: 0/0
- Damage State: 30/30
- Smoke: 3/3
- Console Error/Warning: 0/0
- Missing Script/Broken Prefab: 0/0

## 19. 변경 파일

- GameStartOverlay.cs: 시간 gap, 중복 결과, Disable 복구
- PhaseTransitionOverlay.cs: 중단/예외 안전 복구
- RequestPresenter.cs: 누락 Sprite 최초 1회 오류
- PrePhase2Setup.cs: 신규 시간값 직렬화
- PrePhase2Validation.cs: 위험 경계 12개 검사
- INGAME.unity: 0.85/0.15/0.45/0.20 저장
- PRE_PHASE2_FRAMEWORK_IMPLEMENTATION_REPORT.md: 템포 기록 갱신
- 본 보고서

## 20. 남은 위험

- TryHit 직접 호출 우회는 Board 자체 게이트로 해결됐다.
- 실제 기기 프레임과 다양한 화면 비율에서 연출 체감·배너 위치를 최종 수동 확인해야 한다.

## 21. 최종 판정

**PASS**
