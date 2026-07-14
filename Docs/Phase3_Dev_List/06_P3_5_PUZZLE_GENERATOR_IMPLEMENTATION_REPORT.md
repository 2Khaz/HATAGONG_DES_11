# Phase 3 P3-5 Antigravity Tangram Generator 구현 보고서

- 작업일: 2026-07-14 KST
- Branch: `Sub_Phase3LogicCore`
- 기준 HEAD: `92389149b112cedd39165b13f4faadd8b114dcb3`
- 직접 기반: `PuzzlePiece.cs`, `TangramGenerator.cs`, `TangramManager.cs`

## 폐기 및 교체

기존 v4와 직전 ZIP/UV 구현은 Production 경로에서 제거했다. 신규 구현은 `HATAGONG.Phase3Tangram` 아래에서 안티그래비티 세 파일의 구조를 직접 따른다. 기존 Generator, Session Geometry, Target/Required Rotation, Presenter, RuntimeOrchestrator, Piece UV/Image 분할은 호출하지 않는다.

## Generator 흐름

`Phase3TangramGenerator`는 다음 순서로 동작한다.

1. `(0,0) (16,0) (16,16) (0,16)` 정사각형 하나로 시작
2. 현재 Polygon을 면적 비례로 선택
3. `x=1..15`, `y=1..15`, `x-y=-16..32`, `x+y=-16..32` 후보 생성
4. Seed RNG로 후보 순서를 결정적으로 셔플
5. 첫 유효 Cut을 적용하고 두 자식 Polygon으로 교체
6. Easy 6 / Normal 7 / Hard 8개까지 반복

교차점은 Edge와 직선 방정식으로 계산하며 양쪽 Polygon에 같은 점을 넣는다. 연속 중복 Vertex를 제거하고 양쪽 3개 이상, 면적 16 이상, 최대 4 Vertex, 45도 계열 각도, Bounds 비율 2 이하, Convex를 검사한다. ShapeKind, Shape Signature, Tile Bag, 최근 Shape History는 없다.

한 생성 시도는 최대 1,500번 Polygon 선택/Cut 탐색으로 제한한다. terminal partition을 만날 수 있으므로 전체 정사각형부터 다시 시작하는 결정적 restart를 최대 12회 허용한다. 모든 예산이 소진되면 부분 Polygon 목록을 반환하지 않고 명시적 Failure를 반환한다.

## 결정성

같은 Difficulty와 Requested Seed는 다음이 같다.

- 면적 비례 Polygon 선택
- Cut 후보 셔플과 첫 유효 Cut
- 최종 절대 Polygon 목록
- Piece ID와 Deck Page/Slot
- 초기 45도 Rotation Step

`UnityEngine.Random`, `Random.value`, `Math.random`, 무제한 반복은 사용하지 않는다.

## 3,000 Seed 정적 표본

기존 8/10/12 기준 측정값은 6/7/8 수량 변경으로 무효화했다. 신규 수량 기준 3,000 Seed 검사는 `Phase3TangramValidation`에 반영했으며 사용자 Unity 실행 전이므로 실행 결과는 보류한다.

전 표본에서 Piece Count 오류 0, 총면적 오류 0, interior Overlap 0, Field 이탈 0, 동일 Seed 불일치 0이었다.

## 중심 및 Local Shape

각 절대 Polygon의 면적 중심을 한 번 계산한다.

`originalShape[i] = absolutePolygon[i] - center`

`targetPosition = center`

따라서 Rotation 0에서 `originalShape[i] + targetPosition == absolutePolygon[i]`이다. Target Rotation이나 Rotation Correction은 없다.

## 검증 상태

- P3-1: 154/154
- P3-2: 362/362
- Runtime/Editor C# 9 netstandard2.1: warning 0 / error 0
- Unity Editor/Batch/Play Mode: 미실행
- Git stage/commit/push: 미실행

최종 상태: **ANTIGRAVITY TANGRAM CORE + PHASE 3 DECK/INPUT INTEGRATION APPLIED — READY FOR USER PLAY MODE REVIEW**
