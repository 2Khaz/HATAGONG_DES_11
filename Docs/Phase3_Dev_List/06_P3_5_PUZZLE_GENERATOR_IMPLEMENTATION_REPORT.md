# Phase 3 P3-5 실제 Puzzle Data / Generator 구현 보고서

- 작업일: 2026-07-14 KST
- Branch: `Sub_Phase3LogicCore`
- 기준 HEAD: `81f39586419637caca2dc948b1532843b2bddc69`
- Generator Version: `phase3-geometric-partition-v3`
- 상태: 사용자 검토 전, Git 미반영

## 1. Piece 오더 정정

P3-5 초안은 과거 오더인 Easy 6, Normal 7, Hard 8로 구현됐다. 사용자 신규 확정 오더에 따라 현재 Generator 사양은 다음과 같다.

- Easy: 8 Piece
- Normal: 10 Piece
- Hard: 12 Piece

기존 `phase3-grid-partition-v1`과 같은 Version으로 다른 결과를 만들지 않기 위해 8/10/12 적용 시점에는 `phase3-geometric-partition-v2`를 사용했다. 구조 선택 알고리즘을 확대한 현재 출력은 같은 Seed에서도 v2와 Polygon이 달라지므로 `phase3-geometric-partition-v3`로 다시 구분했다. 사용되지 않는 v2 생성 분기는 유지하지 않는다.

기존 P3-3 Safe Template의 6/7/8 전역 Core 기준은 수정하지 않았다. Generator는 명시적 P3-5 Config와 `Phase3PartitionValidator`의 명시적 규칙 오버로드를 사용한다.

## 2. 보존한 Generator 기반

- Generator 전용 결정적 XorShift RNG
- 같은 Version, Seed, Difficulty의 결정적 결과
- 기본 최대 16회, 요청 가능 1~64회 제한
- 명시적 실패 결과
- Safe Template 자동 대체 없음
- Piece/Slot 1:1 대응
- 자신의 Slot만 Allowed Target
- SHA-256 Canonical Hash
- Piece ID와 나열 순서 독립 Hash
- Structure Signature
- 최근 동일 Canonical Hash 거부

## 3. 허용 도형과 Partition

기존 `Phase3ShapeDefinition`과 `Phase3Geometry`가 지원하는 Polygon Vertex, 수평·수직·45도 Edge, 볼록성 검사를 그대로 사용한다. 별도 Geometry 시스템은 추가하지 않았다.

현재 Generator 허용 도형:

- 삼각형
- 일반 사각형 및 사다리꼴
- 직사각형
- 정사각형
- 평행사변형

유기형, 톱니형, 자유 곡선, 긴 Cell 뱀, 오목 Polyomino는 생성하지 않는다.

Field는 세 Band로 구성하되 v3에서는 높이와 순서를 고정하지 않는다. Easy는 기하 Band 높이 6/7과 나머지 높이 4 이상, Normal/Hard는 기하 Band 높이 5/6과 나머지 높이 5 이상에서 합계 16을 결정적으로 선택한다. 한 Band는 삼각형·평행사변형·사다리꼴로 타일링하고, 나머지 두 Band는 난이도별 행 Piece 수와 45도 Cut 수를 배정한다. 분할 폭은 최소 면적·실제 두께·최대 면적·Aspect Ratio를 생성 전에 만족하는 정수 범위 안에서 Seed로 분배한다. 전체 Polygon은 16×16 영역 안에 있고 기존 Partition Validator가 내부 겹침 0과 전체 면적 256을 확인한다.

## 4. 난이도별 정책

### Easy

- 8 Piece
- 기하 도형 3개와 큰 사각형 5개, 2/3 Piece 행의 위치·높이·폭 변화
- 평균 면적 32
- 낮은 절단 수와 큰 Piece 유지

### Normal

- 10 Piece
- 삼각형·사각형·평행사변형 혼합
- 두 사각 행 중 한 곳의 Seed 선택 Segment를 45도 절단
- 평균 면적 25.6

### Hard

- 12 Piece
- 삼각형·평행사변형·비대칭 사각형 비중 증가
- 두 사각 행에서 1개/2개 Segment를 Seed에 따라 45도 절단
- 평균 면적 약 21.3
- ShapeKind 다양성은 Easy 이상

## 5. 최소 품질 Config

| 난이도 | 최소 면적 | 최소 내부 각도 | 최소 실제 두께 | 최대 Aspect Ratio |
|---|---:|---:|---:|---:|
| Easy | 18 | 30도 | 4 | 2.4 |
| Normal | 12 | 30도 | 3.5 | 2.5 |
| Hard | 10 | 30도 | 3.5 | 2.6 |

최소 면적은 평균 면적의 약 47~56%를 하한으로 두어 지나치게 작은 Piece를 막는다. 30도 제한은 거의 퇴화한 삼각형과 과도하게 작은 각도를 차단한다. 실제 두께는 볼록 Polygon의 각 변에 수직인 축으로 모든 Vertex를 투영해 얻는 두 평행 지지선 사이 거리 중 최솟값이다. 따라서 AABB가 커도 대각선 방향으로 얇은 조각을 거부한다. Normal/Hard의 5×5 직각이등변삼각형 고도는 `5/√2 = 3.5355...`다. v3 256 Seed에서 관측한 최솟값은 Easy `4`, Normal/Hard `3.535533905932736`이고 생성 성공률은 모두 256/256이다. 기준값은 v2 계약에서 완화하지 않았다.

## 6. Rotation 대칭 처리

- 목표 Rotation과 초기 Rotation을 분리한다.
- 기존 45도 `Phase3RotationStep`을 유지한다.
- Polygon을 Centroid 중심으로 45도 단위 회전하고 원래 Vertex 집합과 비교해 실제 대칭 주기를 구한다.
- 정사각형은 2 Step, 직사각형과 일반 평행사변형은 4 Step, 비대칭 삼각형/사각형은 일반적으로 8 Step이다.
- 초기 Rotation 후보는 실제 대칭 주기로 목표와 동등한지 확인하며, 동등하지 않은 첫 Step만 선택한다.
- 같은 Seed에서는 같은 초기 Rotation을 생성한다.

## 7. Hash와 Signature

Canonical Hash 입력:

- Generator Version
- Grid Size
- Difficulty
- 정규화된 Polygon Vertex
- 목표 Rotation

Piece ID와 Piece 나열 순서는 제외한다. 고정 Field 좌표를 기준으로 하므로 회전·반사된 배치는 별도 구조다.

Polygon 입력은 연속 중복과 닫힘용 마지막 중복 Vertex를 제거한 뒤, 기존 정수 Grid 정밀도를 그대로 사용해 Winding과 순환 시작점을 Canonical Sequence로 통일한다. 시작 Vertex, 역방향 Winding, 닫힘 Vertex 유무는 Hash에 영향을 주지 않는다. 실제 좌표, Generator Version, Difficulty 또는 목표 Rotation 변경은 Hash를 바꾼다.

Structure Signature:

- Difficulty
- Piece Count
- 정렬된 Piece 면적 분포
- 정렬된 Piece 둘레 분포
- 내부 경계 길이
- ShapeKind별 개수

## 8. History 범위

- 정확한 동일 퍼즐 중복 방지: 구현
- 유사 Signature 기반 반복 제한: 미구현, 후속 정책 필요
- 영구 History 저장: 미구현, 후속 저장 주체 필요

최근 Canonical Hash와 동일한 후보는 최대 시도 안에서 거부한다. 소진되면 `DuplicateHistoryExhausted`를 반환한다.

결과에는 `RequestedSeed`, `EffectiveSeed`, 0 기준 `AttemptIndex`, `GeneratorVersion`을 구분해 기록한다. `Seed`는 호환용 `RequestedSeed` 별칭이다. `EffectiveSeed`는 Requested Seed, Difficulty, Attempt Index로 결정적으로 파생된다. `RegenerateCandidate(requestedSeed, difficulty, attemptIndex)`로 History 판정 없이 채택 후보 하나를 정확히 재생성할 수 있으며, 검사에서 Polygon 전체 데이터, Piece 수, Hash, Signature, Effective Seed 일치를 확인한다.

## 9. 소형 순수 로직 검사

검사는 Scene, Canvas, Camera, RenderTexture, EventSystem, 입력 장치를 만들지 않는다.

- Easy/Normal/Hard 8/10/12 Piece
- 동일 Version/Seed/Difficulty 결정성
- Polygon 전체 면적 256
- 내부 겹침과 Field 밖 영역 0
- Polygon 유효성, 볼록 연결성, 자기 교차 0
- Piece/Slot ID 고유성과 1:1 Target
- 허용 ShapeKind 외 형상 0
- 삼각형·사각형·평행사변형 생성
- 최소 면적·각도·폭
- 실제 Geometry 대칭 주기
- 초기 Rotation의 목표 대칭 동등 상태 금지
- Canonical Hash 결정성과 Piece 순서 독립성
- Seed 다양성
- History 동일 Hash 거부와 제한 종료

저장소 밖 순수 Roslyn 실행 결과:

- P3-1 기존 Core: `154/154`, 실패 0
- P3-2 기존 Safe Template/Core: `362/362`, 실패 0
- P3-5 신규 Generator: `170/170`, 실패 0

## 10. Targeted Contract Review

| 계약 | 자가판정 | 근거 및 조치 |
|---|---|---|
| Convexity | `TEST_OR_DOC_ONLY` | 기존 최종 생성 검증이 중복 Vertex, 퇴화, 자기 교차, Cross Product 부호 기반 볼록성을 이미 강제했다. Production 변경 없이 오목 사각형, Bow-tie, 공선 퇴화 거부 검사를 명시했다. |
| Minimum Thickness | `TARGETED_CODE_FIX` | 기존 `min(AABB width, height)`는 대각선 얇은 형상을 보장하지 못했다. 실제 평행 지지선 최소 거리로 교체했다. |
| Hash Polygon Canonicalization | `TARGETED_CODE_FIX` | 시작점과 Winding은 기존 Canonicalize가 처리했지만 닫힘 Vertex 입력은 검증 전에 거부됐다. 입력 경계에서 연속/닫힘 중복만 제거했다. Hash 알고리즘은 재작성하지 않았다. |
| History Seed Reproducibility | `TARGETED_CODE_FIX` | 기존 결과는 Requested Seed와 1기준 AttemptsUsed만 남겼다. Effective Seed와 0기준 Attempt Index를 추가하고 단일 후보 재생성 API를 제공했다. |

Production 코드 수정 파일은 `Phase3PuzzleGenerationTypes.cs`, `Phase3PuzzleGenerator.cs` 두 개다. 검사는 `Phase3StageP3_5PuzzleGeneratorValidation.cs`, 설명은 이 보고서에서 보강했다. 기존 `Phase3PartitionValidator.cs`의 볼록성/Partition 계약은 그대로 사용했고 이번 계약 검수로 추가 수정하지 않았다.

## 11. v3 구조 다양성 보완

### v2 원인과 v3 선택 방식

v2의 고유 구조는 Easy 12/256, Normal 71/256, Hard 85/256이었다. 원인은 Band 높이와 순서가 항상 5/6/5이고, 행별 Piece 수, 중앙 기하 Band 좌표, 난이도별 Cut 수가 고정된 상태에서 Seed가 폭 순열·일부 Cut 방향·전체 반사에만 사용된 점이다. Canonical 정규화가 대칭적인 순열을 동일 구조로 올바르게 합치면서 실제 후보 공간의 한계가 그대로 드러났다.

v3는 다음 실제 Polygon 결정값을 독립적으로 Seed에서 선택한다.

- 기하 Band 높이와 세 Band 내 위치
- 나머지 두 Band 높이와 2/3 Piece 행 또는 Cut 0/1/2개 배정 순서
- 최소 면적·두께·최대 면적·Aspect Ratio 안에서의 정수 폭 분배
- Cut 대상 Segment, 대각선 방향, 삼각형/사다리꼴 구성
- 중앙 평행사변형과 사다리꼴의 공유 경계 좌표
- 전체 X/Y 반사

Hash 입력에는 Seed, Attempt, ID, Salt, Initial Rotation을 추가하지 않았다. 시작 Vertex/Winding/닫힘 Vertex/Piece 순서 독립 계약도 유지했다. 고유 Hash 증가는 아래 대표 Vertex처럼 실제 분할 좌표와 Polygon 연결이 달라진 결과다. 최소 면적·두께·각도 및 허용 ShapeKind 기준도 완화하지 않았다.

### 256 Seed 결과

ShapeKind 순서는 Triangle / Quadrilateral / Rectangle / Square / Parallelogram이다.

| 난이도 | 성공/실패 | Hash 고유/중복 | 고유 비율 | 최소 면적/두께/각도 | ShapeKind 총분포 | 조합 고유 | 면적 분포 고유 | 최대/평균 Attempt |
|---|---:|---:|---:|---:|---|---:|---:|---:|
| Easy | 256/0 | 255/1 | 99.609375% | 18 / 4 / 45도 | 256 / 256 / 1106 / 174 / 256 | 4 | 69 | 0 / 0 |
| Normal | 256/0 | 256/0 | 100% | 12.5 / 3.535533905932736 / 45도 | 533 / 491 / 736 / 544 / 256 | 9 | 80 | 0 / 0 |
| Hard | 256/0 | 256/0 | 100% | 12.5 / 3.535533905932736 / 45도 | 1321 / 727 / 420 / 348 / 256 | 10 | 65 | 0 / 0 |

세 난이도 모두 Convex, 자기 교차, Field 이탈, Piece 겹침, ShapeKind, 최소 면적, 최소 실제 두께, 최소 각도 위반이 각각 0이고 재시도 소진도 0이다.

### 64개 누적 History

| 난이도 | 성공 | 고유 Hash | 소진 | 재현 불일치 | Requested Seed 불일치 | 최대/평균 Attempt Index |
|---|---:|---:|---:|---:|---:|---:|
| Easy | 64 | 64 | 0 | 0 | 0 | 0 / 0 |
| Normal | 64 | 64 | 0 | 0 | 0 | 0 / 0 |
| Hard | 64 | 64 | 0 | 0 | 0 | 0 / 0 |

각 성공 Hash를 다음 요청의 History에 누적했다. 모든 결과는 기록된 Requested Seed와 Attempt Index로 `RegenerateCandidate`를 호출했을 때 Effective Seed, Polygon 데이터, Hash, Signature가 일치했다.

### 대표 고유 구조 Vertex

각 `/` 구분은 한 Piece이고 `[x,y;...]`는 그 Piece의 Canonical Vertex 순서다.

Easy:

1. `[0,0;5,0;5,4;0,4]/[5,0;11,0;11,4;5,4]/[11,0;16,0;16,4;11,4]/[9,4;16,4;16,11]/[1,4;9,4;16,11;8,11]/[0,4;1,4;8,11;0,11]/[0,11;7,11;7,16;0,16]/[7,11;16,11;16,16;7,16]`
2. `[0,0;6,0;6,5;0,5]/[6,0;12,0;12,5;6,5]/[12,0;16,0;16,5;12,5]/[0,5;9,5;9,9;0,9]/[9,5;16,5;16,9;9,9]/[0,9;9,9;2,16;0,16]/[2,16;9,9;16,9;9,16]/[9,16;16,9;16,16]`
3. `[7,0;16,0;16,7;14,7]/[0,0;7,0;14,7;7,7]/[0,0;7,7;0,7]/[0,7;6,7;6,11;0,11]/[6,7;11,7;11,11;6,11]/[11,7;16,7;16,11;11,11]/[0,11;10,11;10,16;0,16]/[10,11;16,11;16,16;10,16]`

Normal:

1. `[0,0;9,0;4,5;0,5]/[4,5;9,0;16,0;11,5]/[11,5;16,0;16,5]/[0,5;7,5;7,10;0,10]/[7,5;12,5;12,10;7,10]/[12,5;16,5;16,10;12,10]/[0,10;6,10;0,16]/[6,10;12,10;12,16;6,16]/[12,10;16,10;16,16;12,16]/[0,16;6,10;6,16]`
2. `[5,0;16,0;16,6;11,6]/[0,0;5,0;11,6;6,6]/[0,0;6,6;0,6]/[5,6;12,6;7,11;5,11]/[0,6;5,6;5,11;0,11]/[12,6;16,6;16,11;12,11]/[7,11;12,6;12,11]/[0,11;5,11;5,16;0,16]/[5,11;10,11;10,16;5,16]/[10,11;16,11;16,16;10,16]`
3. `[11,0;16,0;16,5]/[5,0;11,0;16,5;10,5]/[0,0;5,0;10,5;0,5]/[9,5;14,5;9,10]/[0,5;5,5;5,10;0,10]/[5,5;9,5;9,10;5,10]/[9,10;14,5;16,5;16,10]/[0,10;6,10;6,16;0,16]/[6,10;11,10;11,16;6,16]/[11,10;16,10;16,16;11,16]`

Hard:

1. `[5,0;10,0;10,5]/[0,0;4,0;4,5;0,5]/[10,0;16,0;16,5;10,5]/[4,0;5,0;10,5;4,5]/[0,5;8,5;2,11;0,11]/[2,11;8,5;16,5;10,11]/[10,11;16,5;16,11]/[1,11;6,11;6,16]/[6,11;12,11;12,16;11,16]/[12,11;16,11;16,16;12,16]/[0,11;1,11;6,16;0,16]/[6,11;11,16;6,16]`
2. `[7,0;16,0;16,6;13,6]/[0,0;7,0;13,6;6,6]/[0,0;6,6;0,6]/[4,6;10,6;5,11;4,11]/[10,6;16,6;11,11;10,11]/[0,6;4,6;4,11;0,11]/[5,11;10,6;10,11]/[11,11;16,6;16,11]/[10,11;15,11;10,16]/[0,11;6,11;6,16;0,16]/[6,11;10,11;10,16;6,16]/[10,16;15,11;16,11;16,16]`
3. `[0,0;5,0;0,5]/[0,5;5,0;11,0;6,5]/[6,5;11,0;16,0;16,5]/[6,5;11,5;11,10]/[11,5;16,5;11,10]/[0,5;5,5;5,10;0,10]/[5,5;6,5;11,10;5,10]/[11,10;16,5;16,10]/[1,10;7,10;7,16]/[7,10;11,10;11,16;7,16]/[11,10;16,10;16,16;11,16]/[0,10;1,10;7,16;0,16]`

유사 Signature 기반 반복 제한과 영구 History 저장은 여전히 미구현이며 P3-6 연결 정책에서 별도로 결정해야 한다.

## 12. P3-6 연결 전 남은 항목

- 실제 `IPhase3SessionSource` 구현
- Generator의 명시적 Partition Rules를 Session 생성 계약에 전달
- 10/12 Piece를 위한 Runtime Deck 8개 상한 및 페이지 정책 검토
- Run Seed 공급 정책
- 최근 Hash 제공 주체와 영구 저장 정책
- Generator 실패를 Prepare 실패로 전달
- Safe Template fallback 허용 정책
- Adapter/INGAME 연결

Runtime, Input, Presenter, Scene, 모바일, Result/GameFlow는 이번 수정에서 변경하지 않았다.
