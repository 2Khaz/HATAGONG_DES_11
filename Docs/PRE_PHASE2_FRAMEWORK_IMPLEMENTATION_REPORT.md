# Phase 2 착수 전 공용 게임 골격 통합 보고서

## 1. 작업 목표

Phase 2·3 실제 콘텐츠 없이 공용 세션 상태, 누적 점수, Request, Phase HUD, READY/GO, Phase Clear 전환, 타이머·입력·점수 게이트를 INGAME Scene에 통합했다.

## 2. 작업 전 Git 상태

- Branch: main
- HEAD: 98a72c0
- 작업 시작 시 tracked/untracked 변경 없음
- commit, push, branch 변경, package 변경 없음

## 3. 기존 기준선

- Phase 1: 1320/1320 보드, 9900 tiles
- Timer: 정수 90→0, 단위 검증 31/31
- INGAME build index 0

## 4. 구현한 공용 구조

- GameSessionModel / GameSessionController
- GameScoreController
- GameRequestContext
- IGamePhase / Phase1PhaseAdapter
- PhaseHUDPresenter / RequestPresenter
- GameStartOverlay
- PhaseTransitionOverlay / PhaseTransitionController

## 5. GameSession 상태

순수 모델 검증 11/11 PASS. Preparing→Ready→Playing→Transitioning→Playing, Playing→Expired, Playing→Completed를 검증했다. 동일 상태 이벤트 중복, 잘못된 전이, terminal 상태 복귀를 차단한다.

실제 시작 검증: Ready 상태에서 Timer Running=false, Remaining=90, Time_Value=90, 입력=false, Score=0, Overlay=true. Overlay 완료 뒤 Playing, Timer Running=true, 입력=true를 확인했다.

## 6. Phase 공통 계약

IGamePhase가 StartPhase, StopPhase, SetInputEnabled, IsRunning, IsCleared, PhaseCleared를 정의한다. Phase1PhaseAdapter가 기존 Phase1BoardController와 Phase1InputController를 최소 연결하며 실제 Phase 2·3 구현은 없다.

## 7. 공용 Score 구조

GameScoreController만 Score_Value를 소유한다. 점수량, Phase, ScoreReason을 받아 누적하고 Playing 이외 상태 또는 Lock 상태에서 거부한다.

## 8. Phase 1 점수 이전

Phase1ScoreController는 UI를 직접 소유하지 않고 GameScoreController로 전달한다. 보드 Commit/Regenerate의 Score Reset을 제거했다. 타격→파괴→클리어 점수 후 Phase1Cleared 순서를 유지했다.

- Easy 실제 Smoke: 1010/1010
- Normal 실제 Smoke: 1200/1200
- Hard 실제 Smoke: 1420/1420
- Clear event: 각 1회

랜덤 Bag/등급에 따라 이번 실행의 hit 수는 Easy 41, Normal 55, Hard 62였다. 규칙 +10/+50/+300과 계산 결과가 일치했다.

## 9. Request 분류와 UI

- 기본: Normal / NORMAL REQUEST
- 변경: Sudden / SUDDEN REQUEST
- Phase HUD를 1→2→3으로 바꿔도 Request 유지
- 동일 Request 재설정 이벤트 중복 없음
- Normal/Sudden Sprite 슬롯은 각각 Img_icon_normal / Img_icon_sudden에 명시적으로 연결
- 실제 Request 효과는 구현하지 않음

## 10. Phase HUD

- Phase 1: PHASE 1, 파랑/하양/하양
- Phase 2: PHASE 2, 하양/파랑/하양
- Phase 3: PHASE 3, 하양/하양/파랑
- 모든 Dot active/enabled/alpha 1
- active color RGBA(0.1, 0.45, 1, 1), inactive white
- 기존 Phase 1 설명 Cheol-Geo를 보존
- Phase 2·3 설명은 최종 문구 미확정이므로 빈 Serialized String

## 11. READY / GO 시작 연출

기본 시간은 READY 0.85초, READY→GO 빈 간격 0.15초, GO 0.45초, Fade 0.2초다. 전체 화면 50% black dim과 raycast 차단을 사용하며 unscaledDeltaTime 기반이다. 실제 Ready 구간에서 Timer 90/정지, 입력 차단, Score 0을 확인했고 완료 뒤 Timer와 입력이 함께 시작됐다. 중복 Coroutine은 차단한다.

## 12. PHASE N CLEAR 슬라이드 연출

동적 TMP PHASE N CLEAR!! 메시지를 사용한다. 화면 Rect 폭과 Banner 폭으로 우측/좌측 외부 위치를 계산하며 1440을 하드코딩하지 않는다. EaseOutCubic 진입, 중앙 유지, EaseInCubic 퇴장을 사용한다.

실제 검증:

- 시작 state=Transitioning, Timer Paused, 입력 false
- 전환 직전/중 Remaining=71.556361450348로 정확히 동일
- midpoint 1회, completed 1회
- 중복 전환 호출 차단
- 완료 후 Playing, Timer Running, 입력 true, HUD Phase2
- 첫 종료 후 재호출 시 inactive Overlay Coroutine 오류를 발견해 Play 전 활성화하도록 수정

## 13. Timer Pause / Resume

전환 시작 즉시 Pause하고 표시 정수가 아닌 내부 double Remaining을 보존한다. 완료 Callback 뒤 기존 내부 값에서 Resume한다. Reset은 호출하지 않는다.

## 14. 입력 차단

Phase1InputController에 외부 inputEnabled 게이트를 추가했다. Preparing/Ready/Transitioning/Expired에서 false, Playing에서 true다. 비활성화 시 active pointer도 해제한다.

## 15. TimerExpired 처리

실제 격리 Tick 결과:

- Session=Expired
- Remaining=0
- 입력=false
- Score locked=true
- 추가 점수 거부
- GameExpired=1회

Scene reload, Phase 변경, 결과 화면, 보너스 지급은 없다.

## 16. 자동 테스트 목록

- Timer 단위: 31/31 PASS
- Session 모델: 11/11 PASS
- Ready 시작 상태 통합
- Request Normal/Sudden
- Phase HUD 1/2/3
- Transition pause/callback/중복/복구
- Expired 입력·점수 잠금
- Controller/Presenter 중복 수 검사

## 17. Phase 1 전체 회귀 결과

- Matrix: 120/120, 900 tiles
- Stress: 1200/1200, 9000 tiles
- 합계: 1320/1320, 9900 tiles
- 최소 HP 위반 / HP 불일치: 0 / 0
- Damage State: 30/30
- Easy/Normal/Hard 입력·점수 Smoke: 3/3
- 클리어 이벤트: 각 1회
- Fixed Seed Layout/Variant 재현 로직 보존

## 18. Scene·Inspector 연결

GameSession, GameScore, Timer, Request, 각 Presenter, 두 Overlay, Transition Controller, Phase1 Adapter를 Serialized Reference로 연결했다. 각 공용 Controller/Presenter/Overlay/Adapter 수는 정확히 1개다. 런타임 GameObject.Find는 사용하지 않는다.

Scene validate: total issue 0, Missing Script 0, Broken Prefab 0. INGAME build index 0, isDirty=false, Play Mode 종료.

## 19. UI 및 리소스 무변경 확인

기존 HUD/Field RectTransform diff는 0이다. 추가 RectTransform은 신규 Game_UI_Transition 계층뿐이다. Texture, PNG, Sprite, AudioClip, Font asset, Material asset, fallback Color를 생성·교체하지 않았다. 신규 TMP는 기존 Phase_Text font를 참조한다. .vscode 변경 없음.

## 20. 변경 파일 목록

- Assets/Scripts/GameFlow/Runtime 신규 파일: Session, Phase 계약, Score, Request, Adapter, Transition 제어
- Assets/Scripts/GameFlow/UI 신규 파일: Request/Phase HUD, Start/Transition Overlay
- Assets/Scripts/GameFlow/Editor/PrePhase2Setup.cs: Scene 계층 및 Serialized Reference 설정
- Assets/Scripts/GameFlow/Editor/PrePhase2Validation.cs: Session 상태 자동 검증
- Phase1BoardController.cs: 공용 ScoreReason 전달과 Regenerate Score Reset 제거
- Phase1InputController.cs: 외부 입력 게이트
- Phase1ScoreController.cs: 공용 누적 점수 위임
- Assets/Scenes/INGAME.unity: 공용 컴포넌트와 신규 Overlay 저장

## 21. 아직 구현하지 않은 항목

Phase 2·3 실제 게임/Root/점수 규칙, Item, Settings, 팝업, 파티클, 최종 결과 화면, Request 실제 효과, 시간 보너스를 구현하지 않았다. Phase 1 Clear를 존재하지 않는 Phase 2로 자동 연결하지 않았다.

## 22. 남은 위험

- Phase 2 구현 시 실제 Flow가 Phase1PhaseAdapter.PhaseCleared와 Transition Controller를 연결해야 한다.
- Phase 2·3 설명과 Normal/Sudden 전용 아이콘 리소스는 확정 후 Inspector 슬롯에 연결해야 한다.
- 최종 기기 해상도에서 신규 Overlay의 시각 품질을 수동 확인할 필요가 있다.

## 23. 최종 판정

**PASS**

지시 범위의 자동화 가능한 상태·UI·전환·게이트·회귀 조건을 모두 통과했다.
