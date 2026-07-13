# P3-3 확인 완료·미검증·후속 확인 체크리스트

- 기준일: 2026-07-14 KST
- 목적: 확인 상태를 과장하지 않고 다음 작업의 시작점을 고정한다.

## 1. 사용자 확인 완료

- [x] Phase 1 진입 시 `Img_deck1`
- [x] Phase 2 진입 시 `Img_deck2`
- [x] Phase 3 진입 시 `Img_deck3`
- [x] Deck Piece 좌클릭 Drag 시작
- [x] Drag Piece가 Deck 위에서 보임
- [x] Pointer를 따라 이동
- [x] RMB +45도 회전
- [x] RMB Release 후 Drag 유지

## 2. 코드 계약 확인

- [x] PointerDown만으로 상태 변경 없음
- [x] Drag Threshold 이후 BeginDrag
- [x] 같은 PointerId만 후보를 이어받음
- [x] Drag 중 원래 Deck Relay GameObject 생존
- [x] 원래 Deck Graphic과 Overlay Drag View 중복 표시 없음
- [x] Press Anchor 보존
- [x] 좌클릭 Release에서만 Drop
- [x] Empty Field → Loose
- [x] Invalid/Occupied Target → Loose
- [x] Valid Snap Target → Placed
- [x] Deck → 원래 Slot 복귀
- [x] Non-Field UI → Cancel
- [x] Loose 위치 보존 및 재드래그
- [x] Wheel ±45도
- [x] `R` +45도
- [x] Space 무반응
- [x] Phase3DragOverlay 생성 지점 1개

## 3. 명시적 미검증

- [ ] 모바일 Primary Touch Drag
- [ ] 모바일 Primary Touch Release Drop
- [ ] 모바일 Secondary Touch +45도 회전
- [ ] Secondary Touch 시간·이동 제한
- [ ] 다른 TouchId/Device 거부
- [ ] 모바일 Rotate Button
- [ ] 모바일 Pause/Focus loss 복구

이 항목들은 P3-4에서 실제 기기 조건과 함께 확인한다.

## 4. 실제 퍼즐 후속 확인

- [ ] 실제 Puzzle Data 형식 확정
- [ ] 실제 Puzzle Generator 규칙 확정
- [ ] Seed/재현성 정책 확정
- [ ] 난이도별 데이터 생성 확인
- [ ] 실제 `IPhase3SessionSource` 구현
- [ ] Adapter에 실제 Source 등록
- [ ] Safe Template fallback 정책 확정
- [ ] 개발 배지 Production 처리 확정

이 항목들은 P3-5/P3-6 범위이며 P3-3 완료로 표시하지 않는다.

## 5. 최종 게임 흐름 후속 확인

- [ ] Phase1 → Phase2 → Phase3 전체 전환
- [ ] 실제 Puzzle 기반 Phase 3 Clear
- [ ] 최종 Timer 정지
- [ ] 최종 Score 확정
- [ ] `GameSessionState.Completed`
- [ ] 결과 UI
- [ ] Retry/Lobby
- [ ] Clear/Timeout 경합
- [ ] 반복 실행 시 Listener/View 중복 없음

## 6. 커밋 전 점검

- [x] Roslyn warning 0
- [x] Roslyn error 0
- [x] `git diff --check` whitespace 오류 0
- [x] 기존 51개 Manifest 불일치 0
- [x] 삭제된 P3-3 대형 Editor 검증기 복구 0
- [x] 새로운 대형 Editor 검증기 추가 0
- [x] Prefab/Packages/ProjectSettings 변경 0
- [ ] 사용자 문서 검토
- [ ] 사용자 Git 작업 승인

