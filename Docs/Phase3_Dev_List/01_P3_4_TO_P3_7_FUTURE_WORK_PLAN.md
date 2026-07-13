# Phase 3 P3-4~P3-7 차후 작업계획서

- 기준일: 2026-07-14 KST
- 기준: P3-3 Production 구현 및 데스크톱 핵심 동작 사용자 확인 이후
- 중요: 아래 단계는 계획이며 사용자 승인 전 착수하지 않는다.

## 1. 단계 재정의 이유

초기 사전계획의 Runtime Orchestration과 Scene 통합 범위 상당 부분이 실제 P3-3 작업에 포함됐다. 따라서 현재 Production 기준으로 남은 작업을 P3-4~P3-7에 다시 배치한다.

각 Stage는 독립적으로 시작 조건, 완료 조건, 변경 범위를 가진다. 다음 Stage를 미리 섞어 구현하지 않는다.

## 2. P3-4 — 모바일 입력 확인 및 표적 보완

### 목표

현재 존재하는 모바일 입력 구조를 실제 Touch 환경에서 확인하고, 확인된 결함만 수정한다.

### 범위

- Primary Touch Piece Drag
- Secondary Touch +45도 회전
- Secondary Touch 이동·시간 제한
- 다른 Device/TouchId 거부
- 모바일 Rotate Button
- Touch Release Drop
- Pause/Focus loss/Input OFF 정리

### 비범위

- 모바일 UI 전면 재설계
- 새로운 입력 Framework
- 실제 Puzzle Data/Generator

### 완료 조건

- 지원 모바일 환경에서 사용자 또는 지정 기기로 실제 확인
- Desktop RMB/Wheel/R/Space 계약 회귀 없음
- Console Error/Exception 0
- 결과와 기기 조건을 문서에 기록

## 3. P3-5 — 실제 Puzzle Data와 Generator

### 시작 조건

- Puzzle 생성 규칙과 데이터 출처를 사용자가 확정
- 난이도별 Piece 수, 형태, 허용 Target, 초기 Rotation 정책 확정
- 재현 가능한 Seed 정책 확정

### 범위

- 실제 Puzzle Data 형식
- 난이도별 데이터 검증 규칙
- Generator 입력/출력 계약
- Seed와 재현성
- 생성 실패 사유
- Partition, 중복 Slot/Piece, 허용 Target 무결성

### 원칙

- 기존 P3-1/P3-2 Core 모델의 public 계약을 우선 재사용
- Safe Template을 실제 데이터처럼 위장하지 않음
- Scene 또는 Presenter가 퍼즐을 직접 생성하지 않음
- Generator 실패를 빈 퍼즐이나 임의 데이터로 숨기지 않음

### 완료 조건

- Easy/Normal/Hard 실제 데이터 생성 가능
- 동일 Seed의 결과 재현
- 생성 실패가 명시적인 결과로 반환
- Core 무결성 규칙 통과

## 4. P3-6 — 실제 IPhase3SessionSource 통합

### 목표

확정된 Puzzle Data/Generator를 `Phase3PhaseAdapter`의 실제 세션 생성 경로에 연결한다.

### 범위

- `IPhase3SessionSource.TryCreateSession`
- `GameRunContext` 난이도 전달
- 생성 실패 사유 전달
- 실제 Session 수명과 재진입 정책
- Safe Template fallback 정책 확정
- 개발 배지의 Production 처리

### 원칙

- Adapter는 Source를 호출하고 결과를 검증하지만 퍼즐 규칙을 소유하지 않음
- Source 실패 시 이전 Session이나 Safe Template로 조용히 대체하지 않음
- fallback 허용 여부는 Build/환경 정책으로 명시
- Score와 Clear 이벤트를 중복 발행하지 않음

### 완료 조건

- INGAME Phase 3가 실제 Source의 Session을 사용
- 실제 Source 사용 시 fallback 배지 비활성
- 실패 시 Prepare가 명확한 이유와 함께 안전하게 실패
- 재진입과 Deactivate에서 Session/View 누수 없음

## 5. P3-7 — 최종 게임 흐름 및 전체 확인

### 범위

- Phase1 → Phase2 → Phase3 전환
- Phase 3 Clear 후 `GameSessionState.Completed`
- Timer 정지
- 모든 입력 OFF
- 최종 Score 확정
- 결과 UI, Retry, Lobby 정책
- Clear/Timeout 동시 경합
- Phase 재진입 및 반복 실행
- Desktop과 모바일 핵심 경로

### 시작 조건

- P3-4 모바일 결과 확정
- P3-5 실제 Data/Generator 완료
- P3-6 실제 Source 통합 완료
- 결과 화면과 Retry/Lobby UX 명세 확정

### 완료 조건

- 전체 게임 실제 Play Mode 경로 확인
- Scene/Timer/Score/Input 수명 계약 확인
- Console Error/Exception 0
- 최종 사용자 승인 후에만 Git 마감 작업 수행

## 6. 공통 Gate

각 단계마다 다음 순서를 지킨다.

1. 사용자 명세와 시작 조건 확인
2. 해당 Stage의 Production 범위만 구현
3. Roslyn과 소형 정적 점검
4. 필요한 최소 Play Mode 확인
5. 결과 문서 갱신
6. 사용자 승인 전 다음 Stage 및 Git 쓰기 금지

