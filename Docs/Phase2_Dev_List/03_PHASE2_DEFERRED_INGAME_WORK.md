# Phase 2 차후 인게임 총괄 작업 목록

## 1. 목적

Phase 2 기본 기능은 완료됐다. 이 문서는 이후 “인게임 총괄 작업”에서 추가할 요소만 관리한다.

상태 분류:

- `IMPLEMENTED`: 현재 완료
- `DESIGN_CONFIRMED`: 설계는 확정됐지만 코드 미구현
- `WAITING_FOR_RESOURCE`: 최종 리소스 필요
- `WAITING_FOR_PHASE3`: Phase 3가 선행
- `WAITING_FOR_GLOBAL_SYSTEM`: Item·Request·Result 등 공용 시스템 선행
- `NEEDS_DEVICE_QA`: 실기기 검증 필요
- `BALANCE_PENDING`: 전체 플레이 데이터 필요

---

## 2. Production 시각 리소스

### 2.1 시멘트 완료 바닥
- 상태: `WAITING_FOR_RESOURCE`
- 현재: 단색 회색 Base
- 목표: 카툰풍의 바른 시멘트 바닥
- 요구:
  - 1250×1250 영역에서 확대 시 이질감 없음
  - 과도하게 반복되는 타일링 패턴 금지
  - Mask 좌표나 Logic Grid에 종속되지 않음
  - 모바일 압축 후 뭉개짐 확인
- 금지:
  - TMP Example 리소스 채택
  - 인터넷 임의 리소스 무단 추가
  - Texture 교체를 위해 Logic·Mask 구조 수정

### 2.2 미도포 바닥
- 상태: `WAITING_FOR_RESOURCE`
- 현재: 검은 Cover
- 목표: 거친 콘크리트·먼지·빈 바닥 표현
- 요구:
  - Shader는 기존 Mask 의미 0=미도포, 1=도포 유지
  - Source Alpha와 Graphic Alpha 보존
  - 초기 전체 미도포가 명확히 보임

### 2.3 완료 광택
- 상태: `WAITING_FOR_DESIGN`
- 후보: 낮은 Alpha의 젖은 시멘트 Sheen 1회
- 별 Sparkle 남발 금지
- Progress UI 아래, 입력 차단 없음
- 자동 마감 0.4초와 충돌하지 않도록 Timeline 정의

---

## 3. Brush Preview

- 상태: `DESIGN_CONFIRMED`
- 코드: 미구현
- Logic·Score와 완전히 분리

### PC
- Board 밖: 숨김 또는 흐림
- Board 안 Hover: 원형 Preview 표시
- 좌클릭 중: Preview가 커서를 추적하며 실제 Stamp
- Release: 도포 종료, Hover Preview 유지

### 모바일
- 사전 Hover 없음
- Touch 시작과 동시에 Preview 및 Stamp
- Drag 중 추적
- Touch 종료 후 0.1~0.2초 Fade
- 첫 유효 Touch만 도포

### 금지
- Preview 이동만으로 Mask·Grid 변경
- Preview Graphic이 raycast를 가로챔
- Preview 반경과 실제 Logic 반경 불일치

---

## 4. Progress UI

- 상태: `WAITING_FOR_RESOURCE`
- 현재 Scene에는 미구현

권장 역할:

- ProgressGauge
- ProgressText
- 99% Clear 후 UI는 100% 또는 COMPLETE 표시

권장 배치:

- Phase2Root 상단 Stretch
- left/right 32
- top -24
- height 56
- Percent width 96
- gap 16
- Gauge height 30

계약:

- 모든 Graphic/TMP `raycastTarget=false`
- 실제 도포 반응은 즉시
- UI 갱신은 필요 시 0.2~0.3초 throttle 가능
- UI 표시를 Logic 진실값으로 사용하지 않음
- 자동 마감 동안 내부 99%를 시각 100%로 보여줄 수 있음

---

## 5. Item 공통

- 상태: `DESIGN_CONFIRMED`
- 선행: 공용 Inventory·Item 소비 시스템
- Phase 2 전용 아이템:
  1. 흙손
  2. 시멘트 바스킷

공통 규칙:

- 보유 수량만큼 사용 가능
- Stage당 1회 제한 금지
- 버튼 클릭마다 1개 소비
- 수량 0이면 발동 금지
- 연출 충돌 방지용 0.2~0.4초 입력 잠금 허용
- 99% 도달 이후 소비 금지
- Phase 2 종료 시 지속 효과 종료
- 아이템도 기존 Production Stamp 경로를 사용
- 별도 Progress·Score 시스템 생성 금지

---

## 6. 흙손

- 상태: `DESIGN_CONFIRMED`
- 효과:
  - 6초 동안 Brush Radius 1.4배
  - 배율 중첩 없음
  - 효과 중 재사용하면 1개 소비 후 남은 시간 +6초
- 예:
  - Easy 0.085 → 0.119
  - Normal 0.075 → 0.105
  - Hard 0.065 → 0.091
- UI:
  - 아이콘 지속시간 게이지
  - Preview 원 확대
- Feedback:
  - 활성화 사운드
  - 더 두꺼운 도포음 후보
- 금지:
  - 1.4×1.4 식 배율 중첩
  - 다음 Phase 이월
  - Item 전용 별도 Logic Grid

---

## 7. 시멘트 바스킷

- 상태: `DESIGN_CONFIRMED`
- 버튼 클릭 즉시 자동 발동
- 플레이어가 위치를 지정하지 않음
- 미도포 셀 밀집 후보 중 랜덤 선택
- 신규 도포량 목표: 8~12%
- 1회 신규 도포 상한: 12%
- 연출: 0.25~0.4초
- 형태: 큰 원형 또는 불규칙 물방울형
- 사운드: 꿀럭·철퍽 계열

후보 우선순위:

1. 미도포 셀이 큰 덩어리로 모인 곳
2. 중앙 미도포 영역
3. 가장자리 미도포 영역
4. 남은 영역이 작으면 가장 큰 미도포 덩어리

구현 순서:

1. 현재 Grid에서 미도포 후보 수집
2. 밀집도·연결 구역 분석
3. 후보 선택
4. 기존 Orchestrator에 Item Stamp 요청
5. Logic과 Visual 동시 적용
6. 새 셀만 진행도 반영

점수 정책:

- Item 사용 자체 보너스는 없음
- Item으로 새로 도포된 셀을 현재 Coverage Score에 포함할지는 전체 밸런스 확정 시 결정
- 결정을 내리기 전 Core 수치를 임의 수정하지 않음

---

## 8. Request의 Phase 2 효과

- 상태: `WAITING_FOR_GLOBAL_SYSTEM`
- 현재: RequestType·텍스트·아이콘만 세션 유지
- 미정:
  - Normal/Sudden이 Brush, 시간, 점수, 누락 표시 중 무엇을 바꾸는지
  - Phase 2 전용 Modifier 유무
- 원칙:
  - Phase 2가 별도 Request 상태를 소유하지 않음
  - GameRunContext 또는 공용 Request 상태를 읽음
  - Logic Core 기본 수치를 직접 덮어쓰지 말고 명시적 Modifier로 주입
  - Request가 Clear 99% 계약을 임의 변경하지 않음

---

## 9. 사운드·파티클·진동

- 상태: `WAITING_FOR_RESOURCE`

필요 Event:

- 일반 Stamp
- 이미 도포된 영역 재문지름
- 25/50/75% Milestone
- 흙손 활성화·지속 종료
- 시멘트 바스킷 낙하
- 99% Clear
- 0.4초 자동 마감 완료

원칙:

- Feedback이 Logic mutation을 소유하지 않음
- AudioClip null에서 오류 없음
- Stamp마다 Particle·Audio Object 신규 생성 금지
- Pool 또는 제한된 재사용
- 모바일 진동 빈도 제한
- 저사양 기기 GC·발열 측정

---

## 10. Phase 2→3 실제 전환

- 상태: `WAITING_FOR_PHASE3`
- 현재: Phase 3 미등록 안전 잠금
- 선행:
  - Phase3Root
  - Phase3 Adapter
  - Prepare·IsReady
  - 실패 정책
  - 실제 Phase 3 게임 규칙

연결 계약:

1. Phase 2 Score Finalize
2. 0.4초 자동 마감
3. PhaseExitReady
4. GameSessionController가 Phase 3 Prepare 확인
5. Transition Overlay
6. midpoint에서 Phase2Root OFF, Phase3Root ON
7. Phase 3 Input ON
8. Timer 정책 유지

금지:

- 빈 Phase3Root를 가짜로 등록해 오류만 숨김
- Phase 2 완료 전에 Phase 3 Prepare 실패를 무시
- Phase 3 미등록 상태에서 Phase 2 재활성화

---

## 11. 모바일·플랫폼 QA

- 상태: `NEEDS_DEVICE_QA`

필수 대상:

- Android Vulkan
- Android OpenGLES3
- 가능 시 iOS Metal

검사:

- R8 Render/Sample/Blend
- RGBA8 fallback
- BlendOp Max
- 1024 RT 메모리
- 발열
- 긴 Drag
- Touch cancel
- Focus/Pause
- 첫 Touch 단일 입력
- Item UI와 Board Touch 충돌
- Safe Area
- 1440×2560 외 종횡비
- 저사양 Stamp batch

Windows Editor PASS를 모바일 PASS로 간주하지 않는다.

---

## 12. 밸런스와 Telemetry

- 상태: `BALANCE_PENDING`

수집 권장:

- 난이도별 Phase 2 완료 시간
- 평균 Stroke 길이
- Stamp 수
- 90%→99% 체류 시간
- 가장자리 잔여 셀 분포
- Item 사용 수
- Item 신규 도포율
- Phase 2 획득 점수
- 전체 90초 중 Phase별 체류 시간
- 자동 마감 시작 시 PaintedCellCount

확인 목표:

- Easy 12~15초
- Normal 15~18초
- Hard 18~20초
- 99%에서 탐색 스트레스가 과도하지 않음
- Item이 자동 승리 버튼이 아님
- Phase 2 점수 1450의 전체 비중 적정

---

## 13. 작업 우선순위

### Phase 3 전
1. Full Game 자동검수 시스템
2. Phase 2 완료 Commit과 문서 정리
3. 다른 PC 재현성 확인

### Phase 3 구현 직후
1. 실제 Phase 2→3 전환
2. Phase 3→결과 연결
3. 전체 1→2→3 Timer·Score 통합
4. 전체 점수 재밸런스

### 인게임 총괄 Polish
1. Production Texture
2. Progress UI
3. Brush Preview
4. Item
5. Request 효과
6. Audio·Particle·Haptic
7. 모바일·Safe Area·성능
8. 최종 밸런스
