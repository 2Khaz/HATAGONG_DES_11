# Phase 3 Stage P3-3 당일 작업 보고서

- 작성일: 2026-07-14 KST
- Branch: `Sub_Phase3LogicCore`
- 기준 HEAD: `6f7d2cc426ddf78d6d66aac18ff241f19c1c8dc9`
- 상태: 사용자 Play Mode 확인 완료 항목과 미검증 항목을 분리한 커밋 전 기록

## 1. 당일 작업 목표

P3-1/P3-2 Pure Core 위에 Phase 3 Production 표시·입력·세션 연결을 구성하고, 실제 INGAME Scene에서 확인된 P3-3 문제를 표적 수정했다.

이번 단계에는 실제 Puzzle Generator, 실제 Puzzle Data, 실제 `IPhase3SessionSource` 구현을 포함하지 않는다. 해당 요소가 없을 때는 개발용 Safe Template이 사용된다.

## 2. 구현된 Production 구조

- `Phase3PhaseAdapter`
  - Prepare/Activate/Deactivate와 Input Gate
  - `GameScoreController` 연동
  - `PhaseCleared`와 `PhaseExitReady` 전달
  - 실제 세션 소스가 없을 때 Safe Template 사용 및 화면 배지 표시
- `Phase3RuntimeOrchestrator`
  - Session, Presenter, Pointer Drag, 회전, Drop 결과 연결
  - Operation 결과 단일 발행
  - Focus/Pause/Input OFF 시 활성 Drag 정리
- `Phase3BoardPresenter` / `Phase3DeckPresenter`
  - Field, Deck, Loose, Placed, Dragging 표현 분리
  - Deck 페이지와 Piece View 수명 관리
- `Phase3PieceInputRelay`
  - 좌클릭 PointerDown 후보 기록
  - Drag Threshold 이후 BeginDrag
  - PointerId 일치 확인
  - Drag/Release/Disable 후보 정리
- `Phase3DragOverlay`
  - 공용 Canvas 안에서 Deck보다 앞에 Drag Piece 표시
  - Phase 재진입 시 기존 인스턴스 재사용
- Phase별 Deck Sprite
  - Phase 1: `Img_deck1`
  - Phase 2: `Img_deck2`
  - Phase 3: `Img_deck3`

## 3. 당일 표적 수정

### 3.1 Drag 중 우클릭 Release

- RMB는 +45도 회전만 요청한다.
- RMB Release로 좌클릭 Drag를 종료하지 않는다.
- 실제 좌클릭이 눌린 동안 Mouse Drop 처리를 거부한다.

### 3.2 Deck 배경과 Phase별 이미지

- Scene의 공용 Deck 기본 Sprite를 `Img_deck1`로 복구했다.
- 각 Phase Adapter가 Prepare/Activate 시 자기 Sprite를 다시 적용한다.
- Deck의 크기, 비율, 위치는 공용 RectTransform을 유지한다.

### 3.3 Deck Piece Drag 표시 순서

- Drag View를 `Phase3DragOverlay` 아래에 표시한다.
- Overlay는 `Middle_GameDeck`보다 뒤가 아닌 다음 sibling에 위치한다.
- 원래 Deck View와 Overlay Drag View가 동시에 보이지 않도록 한다.

### 3.4 EventSystem Drag 연결

확정 원인은 BeginDrag 직후 Presenter Refresh가 EventSystem의 원래 `pointerDrag` GameObject를 비활성화한 것이었다.

수정 후에는:

- 원래 Deck Piece GameObject가 Drag 동안 활성 상태를 유지한다.
- 원래 Piece Graphic은 숨기고 Raycast를 끈다.
- EventSystem Relay는 계속 활성 상태를 유지한다.
- 별도 Field Piece View만 Overlay에서 보인다.
- PointerUp/EndDrag/Disable에서 입력 후보를 정리한다.

## 4. Drop 계약

| Release 영역 | 결과 |
|---|---|
| Deck | 원래 Deck Slot으로 복귀, 현재 Rotation 유지 |
| Empty Field | `Loose`, Release 위치 유지 |
| Invalid/Occupied Target | `Loose` |
| Valid Snap Target | 목표 위치·회전으로 Snap, `Placed` |
| Non-Field UI | Cancel 및 이전 세션 상태 복원 |

Loose Piece는 Presenter Refresh 후에도 위치를 유지하고 다시 Drag할 수 있다.

## 5. 입력 계약

- RMB: Drag 중 +45도
- Wheel: ±45도
- `R`: +45도
- Space: 무반응
- Drop: 좌클릭 Release에서만 처리
- PointerDown만으로 Session 상태 변경 없음
- Drag Threshold 통과 후에만 `InDeck/Loose → Dragging`

## 6. 사용자 확인 완료

- Phase별 Deck Sprite
- Deck Piece Drag
- RMB 회전 후 Drag 유지

## 7. 명시적 미검증

- 모바일 Primary Touch Drag
- 모바일 Secondary Touch 회전
- 모바일 Rotate Button

미검증 항목은 완료로 간주하지 않는다.

## 8. 후속 단계로 분리된 범위

- 실제 Puzzle Generator
- 실제 Puzzle Data
- 실제 `IPhase3SessionSource` 통합

## 9. 커밋 전 기준선

- 기존 P3-1/P3-2 51개 Manifest fingerprint:
  `9169091e5b7dfe0c7df6ad360346bc467997e1fb977aa97f5300223450571437`
- 삭제된 P3-3 대형 Editor 검증기는 복구하지 않는다.
- 사용자 승인 전 Git add/commit/push를 수행하지 않는다.

## 10. 문서 작성 후 정적 점검 결과

- Runtime/Editor Roslyn: warning 0, error 0
- `git diff --check`: whitespace 오류 0
- 기존 P3-1/P3-2 51개 Manifest 불일치: 0
- Phase 1·2 게임 로직 비의도 변경: 0
- 삭제된 P3-3 대형 Editor 검증기 복구: 0
- Prefab/Packages/ProjectSettings 변경: 0
- staged: 0
- Phase 3 untracked: 90
- 본 문서 패키지 신규 Markdown: 6
- tracked 수정: `INGAME.unity`, `Phase1PhaseAdapter.cs`, `Phase2PhaseAdapter.cs`

현재 요청에서 Git add/commit/push는 실행하지 않았다.
