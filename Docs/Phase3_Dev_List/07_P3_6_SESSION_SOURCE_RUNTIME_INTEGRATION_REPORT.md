# Phase 3 P3-6 Antigravity Tangram Runtime 통합 보고서

- 작업일: 2026-07-14 KST
- 직접 기반: `PuzzlePiece.cs`, `TangramGenerator.cs`, `TangramManager.cs`
- 상태: 신규 Tangram Manager Scene 연결 완료, 사용자 Play Mode 검토 대기

## 실제 반영 구조

데이터 흐름은 `Phase3TangramGenerator → Phase3TangramManager → Phase3TangramPiece`다.

- Generator: 절대좌표 Polygon 목록과 결정적 초기 Rotation 생성
- Manager: 면적 중심, originalShape, Target Assignment, Piece GameObject, Deck/Guide/Snap/Placed 관리
- Piece: originalShape 하나로 Mesh와 PolygonCollider2D 생성, Transform 하나로 이동/회전
- 실제 Polygon: 모든 originalShape Vertex에 `transform.TransformPoint` 적용

Piece당 실제 GameObject는 하나다. 필수 Component는 MeshFilter, MeshRenderer, PolygonCollider2D, Phase3TangramPiece이며 InDeck/Dragging/Loose/Placed 전환 시 복제하지 않는다.

## Mesh, Collider, 색상

Mesh Vertex와 Collider Path는 동일한 `OriginalShape` 배열에서 함께 생성한다. Polygon은 5~8색 팔레트를 순환하는 단색 Material로 표시한다. Piece 색상, ID, GameObject, Original Deck Slot은 Target 교환 때 바뀌지 않는다.

게임 중 Piece별 이미지, UV, Texture, 픽셀 복사, Bounds 재정규화는 모두 제거했다. `testbase.png`는 모든 Piece가 Placed된 뒤 Field 전체에 한 번 표시하는 Completion Overlay에서만 사용한다.

## Target 및 Guide

각 Piece는 물리 Shape와 별도로 현재 `TangramTargetAssignment` 하나를 소유한다. Assignment는 Target ID, 절대 Polygon, 면적 중심 Target Position을 묶는다.

Guide는 Assignment가 가리키는 절대 Polygon의 Vertex를 순서대로 그리고 LineRenderer `loop=true`로 마지막 Vertex와 첫 Vertex를 폐합한다. 전역 Edge Graph, T-junction 보정, 별도 Guide Polygon 생성은 없다.

## 교환형 Snap

드래그 Piece의 실제 Polygon은 `TransformPoint(originalShape)`로 구한다. 아직 Placed되지 않은 모든 Piece의 Target Assignment를 순회하며, 현재 Transform Rotation/Scale을 유지한 상태에서 Target 중심까지 동일 Translation을 적용했을 때 전체 Vertex가 일치하는지 검사한다.

다른 동일 형태 Piece의 Target에 맞으면 두 미배치 Piece의 `TangramTargetAssignment`만 교환한다.

교환 범위:

- Target ID
- Target absolute Polygon
- Target Position

교환하지 않는 항목:

- Piece originalShape/Mesh/Collider
- Piece 색상과 GameObject/ID
- Original Deck Page/Slot
- Rotation Step과 Transform Rotation
- State 및 Placed Piece 데이터

Snap은 Rotation, RotationStep, Scale, Mesh, Collider, Vertex를 변경하지 않는다. Target 중심까지 한 번의 동일 평행이동 후 TransformPoint 전체 Vertex를 다시 비교하고 Field 내부를 확인한 뒤에만 Placed/잠금/점수를 적용한다.

## Deck 및 P3-3 입력

- 페이지당 4칸: Easy 4/2, Normal 4/3, Hard 4/4
- Piece가 빠진 Original Slot은 빈칸 유지
- Return은 Original Page/Slot
- Page 변경은 InDeck visibility에만 영향
- Dragging/Loose/Placed GameObject는 Page 변경 영향 없음
- 좌클릭 Drag. Deck에서 꺼낼 때는 Piece 중심을 포인터에 맞추고, Loose Piece 재드래그는 Press Offset 유지
- Drag 중 RMB +45도
- Wheel ±45도
- R +45도
- Space 무반응
- 빈 Field Drop → Loose
- Deck Drop → Original Slot
- Deck Drop → Original Slot, Loose Piece의 Field 밖 Drop → 가장 가까운 Field 내부 위치로 Clamp

입력 Adapter는 Geometry를 계산하지 않고 Manager의 Begin/Drag/Rotate/End 메서드만 호출한다. Piece 선택은 Collider와 동일한 originalShape를 `TransformPoint`한 뒤 기존 카메라 시선 평면에 투영한 Polygon으로 판정한다.

## Loose 및 완료

Dragging은 최상단 render queue/sorting order와 카메라 방향 표시 오프셋을 사용해 Deck Panel에 가려지지 않는다. 이동 중 Field 밖과 Piece 겹침을 허용한다. Field Drop 시 실제 TransformPoint Polygon 전체가 Field 안에 오도록 최소 Translation만 적용하며, Loose Piece는 Placed Piece 위에도 둘 수 있다. Loose Piece를 Field 밖에 놓으면 이전 위치로 복귀하지 않고 현재 위치에서 가장 가까운 Field 내부 위치로 Clamp한다. Deck에 직접 Drop한 경우에만 Original Slot로 복귀한다.

모든 Piece가 Placed되면 Piece/Guide를 숨기고 `Assets/Resources/Phase3/testbase.png` 전체를 Field 크기의 RawImage 하나로 표시한다. Completion 이미지는 Geometry와 Snap에 관여하지 않는다.

## Scene 및 정적 결과

- INGAME `Phase3Root`: `Phase3TangramManager` 1개
- 기존 v4/ZIP Adapter, SessionSource, Presenter, Guide, Piece Container Scene 참조 0
- 기존 Camera와 EventSystem 사용, 신규 Camera/EventSystem 0
- 기존 Main Camera의 위치·회전·Projection은 변경하지 않음. Piece/Guide 좌표를 기존 카메라 시선 평면으로 투영하며 Canvas render mode만 Phase 3 활성 중 임시 전환 후 복원
- Runtime/Editor Roslyn warning 0 / error 0
- 6/7/8 기준 3,000 Seed Validation 코드 반영, 사용자 Unity 실행 결과 대기
- P3-1 154/154, P3-2 362/362
- 사용자 1차 Play Mode 실패 로그 확인 및 코드 교정 완료, 교정본 재실행 대기
- Git 쓰기 미실행

### 1차 Play Mode 오류 교정

- Session 시작의 입력 비활성화가 Runtime UI 생성보다 먼저 호출되어도 `RefreshVisibility`가 생성 전 참조를 접근하지 않도록 방어
- Play Mode 종료 중 Camera가 먼저 파괴된 경우 `RestoreCanvas`가 Camera Transform을 접근하던 `MissingReferenceException` 제거
- 기존 Main Camera를 XY orthographic으로 강제 변경하던 처리를 폐기하고, 원래 perspective/X 90도 카메라의 시선 평면에서 Piece/Guide/입력/스냅 좌표를 일관되게 계산

### 2차 Play Mode 표시·재시작 교정

- 직렬화된 `260714` Seed 상시 사용을 폐기하고 Prepare마다 신규 64-bit Seed 생성. 고정 Seed는 `useFixedSeedForDebug`가 켜진 경우에만 사용
- Manager가 난이도별 생성 수 Easy 6 / Normal 7 / Hard 8을 Prepare 시 강제 검증하고 Bind 로그에 expected/actual을 함께 기록
- 4칸 Deck에 이전/다음 화살표와 `현재 페이지 / 전체 페이지 · 총 Piece 수` 표시 추가
- 초기 Rotation이 적용된 Bounds로 Deck Preview uniform scale을 계산하여 회전 Piece의 잘림과 크기 오판 제거
- Deck Preview scale에서 Field scale로 전환할 때 클릭한 local grab point가 포인터 아래에 유지되도록 Press Offset 재계산

### 3차 Board/Puzzle 외곽 일치 교정

- 실제 Field Rect의 좌하단·우하단·좌상단에서 단일 Board Frame을 생성
- Guide Vertex, Target Position, Piece Field scale, Clamp 경계가 모두 같은 Board Frame의 origin/X/Y 단위 벡터만 사용
- Board Frame의 X/Y scale 불일치 또는 직교 불일치는 Prepare/Activate 단계에서 오류로 차단
- 난이도별 Piece 수를 Easy 6 / Normal 7 / Hard 8로 변경

### 4차 Guide/Snap/완료 로그 교정

- 사용자 Play 로그에서 Hard 8개 전부 Snap 및 완료 점수까지 적용된 사실 확인
- 완료 후 존재하지 않는 Phase 4 전환을 요청하던 빨간 로그를 제거하고 Phase 3 exit-ready에서 정상 `CompleteGame` 처리
- Guide를 보드 평면보다 카메라 쪽으로 소폭 이동하고 선 굵기·불투명도·sorting order를 높여 Piece가 없는 영역에서 확실히 표시
- Snap 중심 탐색 반경을 Board 15%에서 20%, Vertex 허용 오차를 Board 0.1%에서 0.8%로 조정하되 Vertex 수·Rotation·Scale·전체 Polygon 일치 검사는 유지

### 5차 Drag/Loose/Deck 표시 교정

- InDeck → Dragging 전환 시 Piece 중심을 포인터 위치에 맞추고 즉시 Field scale로 전환
- Loose → Dragging은 기존 local grab offset을 유지
- Dragging 동안 전용 최상단 render queue/sorting order와 카메라 방향 표시 오프셋을 적용하고 Drop 시 Board 평면으로 복원
- Loose Piece의 Placed Piece overlap 거부를 제거하되 기존 Snap Polygon 검증은 유지
- Loose Piece를 Field 밖에 Drop하면 이전 안정 위치 대신 현재 위치에서 가장 가까운 Field 내부 위치로 Clamp
- Phase 1·2는 Deck1을 사용하고, Phase 3 `Activate` 시에만 Deck1 → Deck2 → Deck3을 순차 표시한 뒤 Deck3 유지
- Phase 3 `Prepare`는 공용 Deck 이미지를 변경하지 않아 Phase 2 진행 중 Deck3가 조기 노출되지 않음

## 사용자 Play Mode 체크리스트

- Easy/Normal/Hard Piece 수와 4칸 페이지
- 모든 Piece가 알록달록 단색 Mesh로 표시
- Mesh와 Collider 선택 영역 일치
- 모든 Guide Polygon 폐합 및 Piece 형상 대응
- Deck 빈칸, Page 변경 시 Field Piece 유지
- Deck 중심 Grab 및 Loose Press Offset, RMB/Wheel/R, Space 무반응
- Loose/Deck Return/Field 밖 최근접 내부 Clamp
- 같은 형태 Piece를 다른 동일 Target에 놓았을 때 Assignment 교환
- Snap 중 회전/Scale 변경 0
- 틀린 실제 Polygon은 Placed 금지
- 완성 후 Field 전체 testbase 이미지 한 장 표시
- Error/Exception/Missing Script/Missing Reference 0

최종 상태: **ANTIGRAVITY TANGRAM CORE + PHASE 3 DECK/INPUT INTEGRATION APPLIED — READY FOR USER PLAY MODE REVIEW**
