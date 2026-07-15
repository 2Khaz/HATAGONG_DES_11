# 20260715 Phase 3 Drag / Loose / Deck 변경 및 검토 보고서

## 1. 작성 목적

2026-07-15 진행한 Phase 3 조작 및 표시 보완 작업과 후속 검토 대화를 기록한다.

대상 범위:

- Deck Piece를 꺼낼 때의 Grab 기준
- Dragging Piece의 표시 우선순위
- Loose Piece와 Placed Piece의 겹침 허용 범위
- Loose Piece의 Field 밖 Drop 처리
- Phase별 Deck 이미지 표시 방식
- Perspective Camera에서 Drag 표시 깊이 Offset이 조작감에 미치는 영향

Generator, Snap Polygon 계약, Target 점유, Placed Piece 이동, 완료 판정은 변경 대상이 아니다.

## 2. 최초 요청과 적용 방향

### 2.1 Deck Piece 중심 Grab

요청:

- `InDeck → Dragging` 전환 시 Piece 중심을 포인터 위치에 맞춘다.
- `Loose → Dragging`에서는 실제 클릭한 지점의 Press Offset을 유지한다.
- Deck Preview Scale에서 Offset을 계산한 뒤 확대해 발생하는 첫 프레임 위치 튐을 방지한다.

적용 내용:

- 먼저 Piece를 `FieldWorldScale`로 전환한다.
- Drag 시작 전 상태가 `InDeck`이면 Piece의 `transform.position`을 포인터의 Board 평면 좌표에 맞춘다.
- 시작 상태가 `Loose`이면 Board Scale 좌표계에서 `localGrabPoint`를 계산하고 실제 클릭 지점이 유지되도록 이동한다.
- 따라서 Loose Piece를 다시 선택할 때 중심으로 강제 이동시키지 않는다.

### 2.2 Dragging Piece가 Deck에 가려지지 않도록 표시

적용 내용:

- Dragging 상태의 Material Render Queue를 `5000`으로 설정한다.
- Dragging 상태의 Renderer Sorting Order를 `32000`으로 설정한다.
- Drag 중 Piece를 Board 평면에서 카메라 방향으로 소폭 이동시키는 표시용 깊이 Offset을 추가했다.
- Drop 처리 시작 시 Piece를 Board 평면으로 다시 투영한다.

Dragging이 끝나 `Loose`, `InDeck`, `Placed` 상태로 바뀌면 Render Queue와 Sorting Order는 일반 상태 값으로 복구된다.

### 2.3 Loose Piece를 Placed Piece 위에 배치

허용 범위:

- Snap에 실패한 Piece를 최종적으로 `Loose` 상태로 둘 때 이미 `Placed`된 Piece와 Polygon 영역이 겹치는 것을 허용한다.

변경하지 않은 범위:

- Snap 성공 조건
- 이미 점유된 Target에 중복 Placed
- Placed Piece 선택 및 이동
- Placed Piece끼리의 중복 배치
- Assignment 교환 계약
- 완료 판정

기존에는 Loose Drop 위치를 Field 내부로 보정한 뒤 `OverlapsPlaced(piece)`가 참이면 Drop을 취소했다. 이 복귀 검사와 해당 검사만을 위해 사용되던 전용 보조 함수들을 제거했다.

제거된 함수:

- `OverlapsPlaced()`
- `ConvexInteriorsOverlap()`
- `Separated()`
- `Project()`

이 함수들은 Snap, Target 점유, Placed 전환, 완료 판정에서 사용되지 않았다.

현재 Target 점유 보호는 `TryInterchangeableSnap()`에서 `owner.State == TangramPieceState.Placed`인 Target을 Snap 후보에서 제외하는 기존 처리로 유지된다. Placed Piece 선택 금지도 `SelectPieceAt()`에서 유지된다. 실제 `Placed` 전환은 기존과 동일하게 Snap 검증 성공 경로에서만 실행된다.

### 2.4 Loose Piece의 Field 밖 Drop

적용 내용:

- Loose Piece를 Field 밖에 놓으면 `LastStableLoosePosition`으로 복귀시키지 않는다.
- 현재 Polygon Bounds를 기준으로 Field 내부에 완전히 들어오는 가장 가까운 위치까지 최소 X/Y Translation을 적용한다.
- Deck 영역에 직접 Drop한 경우에는 기존과 동일하게 Original Deck Page/Slot으로 복귀한다.
- Deck에서 처음 꺼낸 Piece를 Field 밖에 놓은 경우에는 기존 Deck Return 정책을 유지한다.

### 2.5 Phase별 Deck 이미지

적용 내용:

- Phase 1: Deck1 유지
- Phase 2: Deck1 사용
- Phase 3: 활성화 시 Deck1 → Deck2 → Deck3 순서로 표시
- 각 Phase 3 전환 프레임은 실시간 기준 `0.12초` 유지
- 애니메이션 종료 후 Deck3 유지
- Phase 3 `Prepare` 단계에서는 공용 Deck 이미지를 변경하지 않는다.

Phase 3가 아직 활성화되지 않은 Phase 2 진행 중 Deck3가 미리 노출되는 문제를 막기 위해 실제 Deck 애니메이션은 `Activate`에서만 시작한다.

## 3. Loose / Placed 계약 검토 결과

### 3.1 OverlapsPlaced()의 기존 역할

`OverlapsPlaced()`는 Snap 실패 후 Loose 상태로 둘 Piece와 기존 Placed Piece의 내부 Polygon 겹침을 검사했다. 겹치면 Drop을 취소하고 기존 Loose 위치 또는 Original Deck Slot으로 복귀시키는 역할만 담당했다.

### 3.2 다른 계약에 대한 사용 여부

다음 기능에서는 사용되지 않았다.

- 전체 Polygon Vertex 기반 Snap 검사
- Target 점유 검사
- Assignment 교환
- Rotation 및 Scale 유지 검사
- Placed 상태 전환
- Placed Piece 선택 차단
- 모든 Piece의 `IsPlaced` 기반 완료 판정
- Generator 및 Piece Polygon 생성

따라서 해당 검사 제거의 기능적 영향은 Loose Piece와 Placed Piece의 최종 겹침을 허용하는 것으로 한정된다.

## 4. Grab 및 깊이 복구 검토 결과

### 4.1 InDeck → Dragging

- Board Scale 전환 후 Piece 중심을 포인터의 Board 평면 위치에 맞춘다.
- 평면상의 Grab Offset은 0이다.
- 이후 별도의 표시용 깊이 Offset이 추가되므로 실제 `Vector3 DragOffset`은 완전한 `Vector3.zero`가 아니다.

### 4.2 Loose → Dragging

- Piece 중심으로 이동시키지 않는다.
- Board Scale 좌표계에서 실제 클릭 지점의 Press Offset을 유지한다.
- 기존 Loose 위치와 Rotation은 유지된다.

### 4.3 Drag 종료

`EndSelectedDrag()` 진입 시 표시용 깊이 Offset을 제거하고 Piece를 Board 평면으로 복구한다.

- 일반 Loose Drop: Board 평면 복구 후 Field Clamp
- Deck Return: Board 평면 복구 후 Original Deck Slot 좌표 적용
- Snap 성공: Board 평면 복구 후 Target 중심과 전체 Polygon 검사
- Snap 실패: Board 평면상의 원래 검사 위치로 복구 후 Loose 처리

따라서 최종 배치, Deck Return, Snap 및 완료 판정에는 Drag 표시 깊이가 남지 않는다.

## 5. Perspective Camera 검토

현재 Main Camera 설정:

- Projection: Perspective
- Field of View: 60
- Position: `(0, 10, -10)`
- Rotation: X 90도
- Phase 3 Board 평면 거리: 카메라 전방 10 단위

현재 표시용 깊이 처리:

```text
position -= camera.forward * DragDisplayOffset
```

Perspective Camera에서는 같은 카메라 X/Y 좌표라도 카메라에 가까워지면 화면 중심에서 더 멀리 투영된다. 이 때문에 다음 현상이 생길 가능성이 있다.

- 화면 중앙에서는 거의 차이가 없음
- 화면 가장자리에서 Piece 중심 또는 실제 Grab 지점이 포인터 바깥 방향으로 미세하게 이동
- 드래그 시작 시 Piece가 미세하게 확대되거나 미끄러지는 느낌
- 작은 Piece를 정밀하게 조작할 때 손에 완전히 붙지 않는 느낌

예상 오차 비율은 대략 다음과 같다.

```text
DragDisplayOffset / (10 - DragDisplayOffset)
```

수치상 일반적으로 약 1픽셀 안팎의 작은 차이일 가능성이 높다. 게임 판정 오류보다는 조작감 문제에 해당한다.

## 6. Orthographic Camera 검토

Orthographic Camera에서는 깊이가 바뀌어도 화면상의 X/Y 위치와 크기가 바뀌지 않는다. 따라서 현재 표시용 깊이 Offset을 유지해도 포인터 위치 오차는 사라진다.

다만 Main Camera를 전역 Orthographic으로 변경하는 것은 권장하지 않는다.

별도 영향 가능성:

- Phase 1·2 및 공용 World 오브젝트의 원근감 변경
- 배경과 카메라 기반 효과의 크기 변경
- 기존 Perspective FOV 60과 Orthographic Size 5 사이의 화면 범위 차이
- Canvas와 투명 오브젝트의 Depth Sorting 변화
- Phase 3 종료 시 Projection 복구 처리 추가 필요

거리 10에서 FOV 60과 유사한 수직 화면 범위는 Orthographic Size 약 `5.77`이다. 현재 직렬화된 Size 5를 그대로 사용하면 World 표현이 기존보다 확대될 수 있다.

## 7. 향후 권장 수정안 — 현재 미적용

조작감 개선이 실제로 필요할 경우 가장 영향이 적은 권장 순서는 다음과 같다.

1. Perspective Main Camera는 유지한다.
2. Dragging Piece의 카메라 정방향 Transform 깊이 Offset을 제거한다.
3. InDeck Piece는 Board Scale 전환 후 `DragOffset = Vector3.zero`로 중심을 포인터에 고정한다.
4. Loose Piece는 기존 Press Offset을 유지한다.
5. Deck보다 위에 표시하는 문제는 Render Queue와 Sorting Order로만 해결한다.
6. 해당 방식으로도 특정 Canvas에서 가려질 때만 Pointer Ray 방향의 깊이 이동 또는 전용 렌더 계층을 검토한다.

카메라 정방향이 아니라 Pointer Ray 방향으로 깊이를 이동하면 Perspective에서도 Grab 지점의 화면 위치는 유지할 수 있다. 그러나 Piece가 미세하게 커질 수 있고 `DragOffset`이 완전한 0이 아니므로 차선책이다.

이 권장 수정안은 분석 결과일 뿐 이번 보고서 작성 시점에는 코드에 적용하지 않았다.

## 8. 정적 검증 결과

구현 직후 실행한 정적 검증:

- `Assembly-CSharp.csproj`: warning 0 / error 0
- `Assembly-CSharp-Editor.csproj`: warning 0 / error 0
- 변경 코드 및 문서 대상 `git diff --check`: 정상
- Unity 및 Play Mode: 실행하지 않음
- Git stage / commit / push: 실행하지 않음

## 9. 실제 구현 당시 변경 대상

- `Assets/Scripts/Phase3Tangram/Phase3TangramManager.cs`
- `Assets/Scripts/Phase3Tangram/Phase3TangramPiece.cs`
- `Assets/Scenes/INGAME.unity`의 Phase 2 Deck Sprite 연결
- `Docs/Phase3_Dev_List/07_P3_6_SESSION_SOURCE_RUNTIME_INTEGRATION_REPORT.md`

이후 질문 응답 단계에서는 코드를 수정하지 않고 정적 분석과 설명만 수행했다.

## 10. 현재 판정

- Loose Piece와 Placed Piece의 겹침 허용 범위는 Snap 및 Target 점유 계약과 분리되어 있다.
- Placed Target 중복 점유, Placed Piece 이동 및 완료 판정은 변경되지 않았다.
- Drop, Deck Return 및 Snap 후 Piece 깊이는 Board 평면으로 복구된다.
- Perspective Camera의 표시용 깊이 Offset은 최종 게임 판정에는 영향을 주지 않지만 미세한 드래그 조작감 오차를 만들 수 있다.
- 해당 오차를 해결해야 할 경우 공용 Camera Projection 변경보다 Drag Transform 깊이 Offset 제거가 최소 영향 수정안이다.

최종 상태: **PHASE 3 DRAG / LOOSE / DECK CHANGE DOCUMENTED — PERSPECTIVE DRAG OFFSET REVIEW PENDING PLAY MODE CONFIRMATION**
