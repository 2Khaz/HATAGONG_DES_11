# Full Game 실제 Play Mode 자동검수 시스템 구축 명세

## 1. 결정 사항

이 시스템은 **Phase 3 작업에 들어가기 전에 구축한다.**

목표:

```text
Codex가 수정
→ 열린 Unity Editor에서 실제 Play Mode 자동검수
→ Run별 JSON 읽기
→ 실패 원인 수정
→ 다시 검수
→ 전부 통과할 때까지 제한 반복
→ 사용자에게 최종 체감 검수만 요청
```

사용자가 매번 기능 오류를 직접 찾는 구조를 없애고, 사용자는 최종 연출·손맛만 확인한다.

---

## 2. Batchmode 정책

이 프로젝트는 과거 Unity LicenseClient·AssetImportWorker 문제를 반복 경험했다.

따라서 1차 구조는:

- Unity Editor를 사람이 한 번 실행
- Editor를 테스트 서버처럼 유지
- MCP 또는 로컬 명령 채널로 메뉴 실행
- Play Mode 진입·종료
- JSON 결과 수집

Batchmode를 기본 실행 경로로 만들지 않는다. 안정성이 증명된 경우에만 보조 경로로 추가한다.

---

## 3. 필수 구성요소

### 3.1 FullGameIntegrationRunner
책임:

- 단일 메뉴 진입점
- 고유 Run ID 생성
- Test 단계 순서 관리
- Play Mode 진입·종료
- Timeout
- 실패 시 안전 종료
- 최종 결과 파일 기록

권장 메뉴:

```text
Tools > HATAGONG > Validation > Run Full Game Integration
```

### 3.2 PlayModeScenarioDriver
책임:

- READY/GO 대기
- Phase 1 자동 진행
- Transition 상태 관찰
- Phase 2 실제 입력
- Phase 3 자동 진행 — 구현 후 확장
- Result 확인

Production private 메서드를 직접 호출해 성공시키는 것이 아니라 가능한 한 실제 공개 입력·이벤트 경로를 사용한다.

### 3.3 EventSystemPointerSimulator
실제 `EventSystem`과 `InputSystemUIInputModule`에 가까운 순서로 이벤트를 보낸다.

Phase 2 필수 순서:

```text
PointerDown
→ BeginDrag
→ Drag
→ PointerExit
→ Board 밖 Drag
→ PointerEnter
→ 재진입 Drag
→ PointerUp
→ EndDrag
```

별도 Scenario:

- 다른 Pointer ID
- Focus loss
- Application pause
- Input disable
- Root deactivate
- 빠른 Drag
- 경계·모서리
- Click without Drag

직접 `OnDrag(fakeEventData)`만 호출하는 검증은 보조 단위검증으로만 사용한다.

### 3.4 JsonResultWriter
누적 Editor.log가 아니라 Run별 구조화 결과를 만든다.

권장 경로:

```text
Logs/HATAGONGValidation/<runId>/result.json
Logs/HATAGONGValidation/<runId>/summary.txt
```

Git 추적 대상이 아니어야 한다.

### 3.5 LatestRunLocator
- 최신 Run ID를 명시적으로 선택
- 시작·종료 Timestamp
- Suite Version
- Git Commit
- Branch
- Unity Version
- Scene
- 결과 파일 경로
- 과거 로그와 혼동 금지

---

## 4. JSON Schema 초안

```json
{
  "schemaVersion": 1,
  "runId": "2026-07-13T16-57-33.123+09-00",
  "suite": "FullGameIntegration",
  "suiteVersion": "1.0.0",
  "branch": "Sub_Phase2LogicCore",
  "commit": "COMMIT_SHA",
  "unityVersion": "6000.3.19f1",
  "scene": "Assets/Scenes/INGAME.unity",
  "startedAt": "ISO-8601",
  "finishedAt": "ISO-8601",
  "status": "Passed",
  "passed": 0,
  "total": 0,
  "failures": [
    {
      "id": "Phase2.PointerExitReentry",
      "phase": "Phase2",
      "frame": 1832,
      "message": "Coverage did not increase after re-entry",
      "expected": "active stroke continues",
      "actual": "stroke inactive"
    }
  ],
  "metrics": {
    "phase1Score": 0,
    "phase2Score": 1450,
    "phase2PaintedCellsAtClear": 16221,
    "phase2CompletionSeconds": 0.4,
    "totalScore": 0
  }
}
```

---

## 5. Phase 1 자동 Scenario

검사:

1. Scene 시작
2. Session Preparing
3. READY/GO
4. Timer가 GO 이후에만 감소
5. Phase1Root active
6. Phase2Root inactive
7. 실제 Phase 1 Production 입력 경로로 완료
8. 최종 Score 반영
9. PhaseCleared 1회
10. PhaseExitReady가 최종 Score를 관찰
11. 입력 차단
12. Transition 시작
13. Timer Pause

Phase 1 자동 완료 방식은 테스트 전용 Production bypass가 아니라 실제 Tile 입력·Damage 경로를 사용한다.

---

## 6. Phase 1→2 Transition Scenario

검사:

- Overlay 표시
- midpoint 전 Phase1 유지
- midpoint에서 Phase1Root OFF
- Phase2Root ON
- Phase 2 별도 READY 없음
- Phase 2 Prepared
- 초기 Cover 불투명
- Timer 정책 정상
- Phase 1 입력 재활성화 없음
- 중복 전환 없음

---

## 7. Phase 2 자동 Scenario

### 입력
- PointerDown 첫 Stamp
- Drag Coverage 증가
- 중복 도포 Score 0
- PointerExit 후 Pointer ID 유지
- Board 밖 구간에서 Stroke 유지
- 재진입 후 Coverage 증가
- PointerUp 후 Drag 무시
- EndDrag 중복 안전
- 다른 Pointer 무시
- Focus loss 취소

### Clear
- 16,220셀: 미Clear
- 16,221셀: Clear
- Input OFF
- PhaseCleared 1회
- Score 1450
- PhaseExitReady 즉시 발생하지 않음
- CompletionFill 중간값
- 0.4초 범위
- 완료 후 Fill 1
- Logic PaintedCellCount 불변
- Score 불변
- PhaseExitReady 1회

### 현재 Phase 3 부재
- 예상 안전 잠금
- Session Transitioning
- Timer Pause
- 모든 Input OFF
- Phase2 재활성화 없음

---

## 8. Phase 3 구현 후 확장

- Phase 2→3 전환
- Phase3Root 활성
- Phase 3 Input
- 난이도
- Score
- Clear
- Phase 3 완료 연출
- 최종 Victory
- Timer 정지
- Result 화면
- Retry
- Lobby 이동
- Timeout race

---

## 9. Codex 반복 루프

권장 최대 반복:

- 최대 10회
- 같은 Failure ID 2회 연속이면 중단
- 총 Assertion 수 감소 시 중단
- Scene diff 허용 범위 초과 시 중단
- Production 수치 변경 시 중단
- 새 Compile Error가 늘어나면 중단
- Test가 Assertion을 삭제하면 중단
- Working tree에 예상 밖 파일이 생기면 중단
- Commit·Push는 사용자 승인 전 금지

루프:

1. Git 상태·Commit 기록
2. 실패 JSON 읽기
3. Production/Validation 결함 분리
4. 최소 수정
5. 정적 컴파일
6. Full Game Integration 실행
7. 새 Run ID 결과만 읽기
8. Passed면 종료
9. 실패면 안전장치 평가 후 반복

---

## 10. 자동검수와 수동검수 경계

### 자동검수 대상
- Phase 순서
- Timer
- Root 활성
- Score
- Clear
- 중복 Event
- Pointer 연속성
- 자원 해제
- Transition
- 1→2→3→Result
- Timeout
- JSON 결과

### 사용자 최종 확인
- 브러시 감각
- 자동 마감이 보기 좋은가
- Overlay 속도
- 시멘트 비주얼
- 사운드 체감
- 모바일 손맛
- 난이도 체감

기능 버그 탐색은 자동화하고, 취향과 품질 승인만 사람이 한다.

---

## 11. 완료 조건

- 메뉴 한 번으로 실행
- 사용자 입력 없음
- Play Mode 자동 종료
- Run별 JSON
- 실패 위치·Frame·Expected·Actual 기록
- 누적 Editor.log 의존 없음
- Phase 2 실제 PointerExit 문제를 재현·검출 가능
- Phase 2 자동 마감 검출 가능
- Phase 3 부재 안전 잠금 검출 가능
- Stage 5A/5B 기존 검증을 삭제하지 않음
- 사용자 승인 전 commit/push 없음
