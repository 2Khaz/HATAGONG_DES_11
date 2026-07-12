# Phase 1 개발 중 후순위 작업

이 문서는 Phase 1 구현과 안정화 과정에서 핵심 철거 메커니즘 완성을 위해 의도적으로 미룬 작업을 관리한다.

현재 Phase 1 생성·HP·Damage State·입력·점수·클리어 로직은 검증 완료 상태이며, 아래 항목은 해당 핵심 로직의 실패 사항이 아니다.

## Phase 1 Production 시각 리소스

- 상태: WAITING_FOR_RESOURCE
- 분류: Phase 전용 / 리소스
- 보류 이유: Tile 등급, Damage State, 타격·파괴의 최종 비주얼 기준이 확정되지 않았다.
- 현재 임시 처리: 현재 Sprite 슬롯과 Fallback Color 기반 프로토타입 표현을 유지한다.
- 재개 시점: Phase 1~3 전체 비주얼 방향과 제작 기준 확정 후.
- 선행 조건: 등급별·손상별 Sprite 목록과 아트 가이드.
- 영향 범위: Phase1TileView, Grade visual set, Damage State 표현.
- 구현 시 주의사항: HP·Grade·Damage State 논리를 리소스에 종속시키지 않는다.

## Phase 1 타격·파괴 사운드

- 상태: WAITING_FOR_RESOURCE
- 분류: Phase 전용 / 연출
- 보류 이유: 타격음, 파괴음, 보드 클리어 사운드의 공통 스타일이 없다.
- 현재 임시 처리: AudioClip null-safe 상태에서 핵심 입력·점수·클리어를 검증한다.
- 재개 시점: 전체 게임 사운드 스타일 확정 후.
- 선행 조건: AudioClip과 우선순위·볼륨 정책.
- 영향 범위: Phase1FeedbackController, 공용 SFX 정책.
- 구현 시 주의사항: 사운드 누락이 게임 처리나 Clear를 막지 않도록 한다.

## Phase 1 타격·파괴 추가 피드백

- 상태: DEFERRED
- 분류: Phase 전용 / 연출
- 보류 이유: 파편, 화면 흔들림, 추가 진동과 파괴 강조는 핵심 로직 검증에 필요하지 않다.
- 현재 임시 처리: Damage State, DestroyVisual과 최소 punch 표현을 사용한다.
- 재개 시점: Production 비주얼과 모바일 피드백 기준 확정 후.
- 선행 조건: 효과 강도, 성능 예산, 접근성 정책.
- 영향 범위: 타격·파괴·Clear 피드백.
- 구현 시 주의사항: HP mutation과 이펙트 실행을 분리하고 중복 Clear를 만들지 않는다.

## 점수 팝업 및 점수 피드백

- 상태: DEFERRED
- 분류: 공용 시스템 / UI
- 보류 이유: Phase 1~3의 점수 발생 패턴이 모두 확정되지 않았다.
- 현재 임시 처리: GameScoreController가 Score_Value 숫자만 갱신한다.
- 재개 시점: Phase 1~3 점수 이벤트와 표시 빈도 확정 후.
- 선행 조건: 공용 Score event payload와 popup pooling 정책.
- 영향 범위: 타격·파괴·Phase Clear 점수 표시.
- 구현 시 주의사항: 팝업은 점수의 소유자나 계산자가 아니며 공용 점수 결과만 표현한다.

## Item의 Phase 1 적용

- 상태: WAITING_FOR_DESIGN
- 분류: 공용 시스템 / Item
- 보류 이유: Item 종류, 수량, 소비, 효과와 입력 잠금 정책이 없다.
- 현재 임시 처리: Item_Button과 Phase 1 기능을 연결하지 않는다.
- 재개 시점: 전체 Item 시스템을 한 번에 설계할 때.
- 선행 조건: 세션 소유권, 저장, 소비 트랜잭션과 Phase별 효과 계약.
- 영향 범위: 입력, Tile mutation, UI, 점수.
- 구현 시 주의사항: Phase별 임시 Item 소유자를 만들지 않는다.

## Settings의 Phase 1 적용

- 상태: DEFERRED
- 분류: 공용 시스템 / Settings
- 보류 이유: 사운드, 진동, 입력 옵션과 저장 정책이 미정이다.
- 현재 임시 처리: Settings 버튼과 실제 설정 기능을 연결하지 않는다.
- 재개 시점: 공용 Settings 시스템 설계 후.
- 선행 조건: 설정 데이터 모델과 영속화 정책.
- 영향 범위: Audio, vibration, input, UI.
- 구현 시 주의사항: Phase 1 내부에 별도 설정 복사본을 만들지 않는다.

## Request 실제 게임 효과

- 상태: WAITING_FOR_DESIGN
- 분류: 공용 시스템 / Request
- 보류 이유: Normal/Sudden의 점수·시간·Phase 규칙 보정이 미정이다.
- 현재 임시 처리: RequestType, 텍스트와 아이콘만 세션에서 유지한다.
- 재개 시점: Phase 1~3 기본 규칙 구현 후.
- 선행 조건: Request 성공·실패 및 보정 설계.
- 영향 범위: 전체 세션, 모든 Phase, 점수와 Timer.
- 구현 시 주의사항: Request는 특정 Phase 전용이 아니라 전체 게임 세션 특성이다.

## 전체 점수 재밸런싱

- 상태: WAITING_FOR_PHASE3
- 분류: 공용 시스템 / 점수
- 보류 이유: Phase 2·3 점수량과 최종 시간 보너스가 확정되지 않았다.
- 현재 임시 처리: Phase 1의 +10/+50/+300 규칙을 유지한다.
- 재개 시점: Phase 3까지 구현 완료 후.
- 선행 조건: 세 Phase 실제 플레이 시간과 점수 telemetry.
- 영향 범위: Phase별 비중, 최종 점수, 시간·Request 보너스.
- 구현 시 주의사항: Phase별 잔여 시간을 중복 점수화하지 않는다.
