# Phase 2 검증 근거와 회귀 위험

## 1. 최종 완료 판정

Phase 2 Stage 5B는 자동검증과 사용자 실제 Play Mode 검수를 모두 통과했다.

### Phase 2
- Stage 5B Scene Integration: `157/157`
- Stage 5A Integrated Flow: `118/118`
- Logic Core: `36/36`
- Visual Tech: `34/34`
- Runtime Orchestration: `69/69`

### 기존 공용
- Pre-Phase2 Models: `11/11`
- Timer: `31/31`
- Request Icons: `12/12`
- Risk Boundaries: `12/12`
- Final Safety: `8/8`

### Phase 1
- Generation Matrix: `120/120`
- Generation Stress: `1200/1200`
- Minimum HP violations: 0
- HP mismatches: 0

---

## 2. 사용자 실제 Play Mode 통과 항목

- Phase 1 정상 시작
- GO 이후 Timer 진행
- Phase 1 Clear와 Score Finalize
- Transition 중 Timer Pause
- Overlay midpoint Root 교체
- Phase 2 별도 READY 없음
- 초기 검은 Cover 정상
- 좌클릭 Drag 도포 정상
- Board 밖 이동 후 재진입 Stroke 유지
- 99% Clear
- Phase 2 Score 1450
- 0.4초 자동 마감
- 마감 후 검은 흔적 완전 제거
- 자동 마감 추가 점수 없음
- Phase 3 미등록 안전 잠금

---

## 3. 현재 예상 오류

Phase 3가 등록되지 않은 현재 다음 오류는 예상 동작이다.

```text
[GameFlow][Transition] Next phase is not registered: Phase3.
Session remains Transitioning, timer paused, and all phase input disabled.
```

다음 조건을 충족하면 회귀가 아니다.

- Phase 2 Score와 자동 마감 완료 후 발생
- Timer paused
- 모든 input disabled
- Phase 2 재활성화 없음
- Session Transitioning 유지

Phase 3 등록 후에는 이 오류가 사라져야 한다.

---

## 4. 해결된 결함과 재발 방지

### 4.1 자동 마감 누락
과거:
- 99% 직후 ExitReady
- 검은 빈틈 잔존

현재:
- Score 선반영
- CompletionFill 0→1
- 0.4초 후 ExitReady

회귀 검사:
- Fill 중간값
- Logic·Score 불변
- ExitReady 1회

### 4.2 PointerExit Stroke 손실
과거:
- pointerPress 등 불안정한 값으로 취소

현재:
- PointerExit는 취소하지 않음
- PointerUp/EndDrag/Focus loss 등만 종료

회귀 검사:
- Exit
- Board 밖 Drag
- 재진입 Drag
- Coverage 증가

### 4.3 경계 밖 거부가 Stroke를 종료
과거:
- Geometry rejected 결과를 입력 종료로 오해

현재:
- 비도색 결과와 Pointer 생명주기를 분리

회귀 검사:
- Board 밖 non-paint segment
- 활성 Pointer 유지
- 재진입 성공

### 4.4 Mask identity 오판
과거:
- Prepare 직후 firstMask와 현재 Mask 동일 요구

원인:
- Ping-Pong Composite가 `_mask`와 `_scratch`를 교환

현재:
- Deactivate 직전 현재 Mask를 저장
- 같은 참조와 `IsCreated` 보존 검사

### 4.5 Validation Fixture 생명주기 오판
과거:
- Edit Mode의 Destroy/OnDisable 자동 호출을 실제 Runtime과 동일하게 가정

현재:
- 소유권과 private lifecycle을 명시적으로 검사
- 실제 Play Mode 스모크로 보완

---

## 5. 주요 회귀 위험

### 높은 위험
- PointerExit에서 다시 Stroke 취소 조건 추가
- Presenter가 외부 Mask Release
- Deactivate에서 Orchestrator Dispose
- Auto Completion 전에 ExitReady
- Item이 별도 Grid/Score 생성
- Phase 3 연결 시 Timer가 잘못 재개
- Setup 재실행으로 Scene 중복 생성

### 중간 위험
- Production Texture 교체 시 Shader Alpha 의미 역전
- Progress UI가 Raycast 차단
- Mobile R8 미지원
- Safe Area에서 Board 일부 접근 불가
- Trowel Radius와 Preview 불일치
- Cement Basket이 이미 도포된 곳을 선택

### 낮은 위험
- Runtime Material identity 변경
- Deactivate에서 Presenter Material 재생성
- Ping-Pong으로 현재 Mask object가 교대

---

## 6. 변경 시 최소 재검증 Matrix

### Pointer·Presenter 변경
- Stage 5B 157/157
- 실제 PointerExit Play smoke
- Visual 34/34

### Orchestrator·Mask 변경
- Logic 36/36
- Visual 34/34
- Orchestration 69/69
- Stage 5B 157/157

### GameFlow·Phase 배열 변경
- Stage 5A 118/118
- Stage 5B 157/157
- 실제 1→2 또는 1→2→3 smoke
- Timer 31/31
- Final Safety 8/8

### Phase 1 변경
- Matrix 120/120
- Stress 1200/1200
- Stage 5A
- Full Game Integration

### Shader·Material·Texture 변경
- Visual 34/34
- Stage 5B
- 초기 Cover
- Stamp 표시
- Auto Completion
- 모바일 플랫폼 검증

---

## 7. Commit 생성 직전 역사적 최종 감사 Snapshot

다음 내용은 Commit `8d658ed1f2719d3716b8372610ec52cd1ba765b4` 생성 직전의 역사적 Snapshot이며, 현재 Working Tree 상태를 뜻하지 않는다.

당시 작성 시점:

- Branch: `Sub_Phase2LogicCore`
- 이전 기준 HEAD: `18ceb1bd4ef84b9274f48fe1a3f280e932c4e648`
- Stage된 파일: 0
- Tracked modified: 2
- Untracked: 16
- 총 변경: 18
- 삭제·Rename: 0
- 예상 밖 변경: 0
- Scene diff: 308 additions / 0 deletions
- `git diff --check`: 오류 0
- Assembly-CSharp: Warning 0 / Error 0
- Assembly-CSharp-Editor: Warning 0 / Error 0

이 문서 패키지는 위 18개 파일 감사 후 외부에서 생성됐으며, 이후 기능 파일과 함께 저장소에 반영됐다.

### 실제 적용 결과 및 인수 시점

- Phase 2 기능 파일 18개와 인수인계 Markdown 10개가 동일 Commit에 포함됨
- 적용 Commit: `8d658ed1f2719d3716b8372610ec52cd1ba765b4`
- 실제 Commit 제목: `[ADD]260713 7번째 커밋_이정현`
- 현재 Branch: `Sub_Phase2LogicCore`
- 인수 시점 Working Tree: clean
