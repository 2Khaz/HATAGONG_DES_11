# Phase 1 재질 최소 등장 및 색상 분산 적용 보고

## 이번 단계 핵심 요약

- 최종 판정: 정적·Editor 검증 PASS. Play Mode 육안 검증은 미수행.
- Branch: `OutGame_Contec`
- HEAD: `b3848fe10603d7a027a722d3611d4589f26fc88d`
- 작업 전 Git 상태: staged 변경 없음. 최초 제한 환경 조회에서는 기존 PNG 10개가 Modified로 관찰됐으나, Git LFS 권한을 허용한 최종 조회에서는 해당 PNG의 실제 diff가 없었다. 해당 파일을 수정하거나 복구하는 명령은 실행하지 않았다.
- 작업 후 Git 상태: Phase 1 코드·설정 6개 수정, 본 보고서 1개 신규, staged 변경 없음.
- 수정 파일: `Phase1GameConfig.cs`, `Phase1TileGradeDefinition.cs`, `Phase1PrototypeSetup.cs`, `Phase1BoardGenerator.cs`, `Phase1GradeAssigner.cs`, `Phase1GameConfig.asset`, 본 보고서.
- 예상 밖 변경: 최종 Git LFS 기준 없음.
- Scene SHA: 작업 전·후 `E304F937DE8BA6E38977A20385B5AA79FE5025FC8A728F43B685A0D1D7016438`.
- Staged/Commit/Push: 모두 0.

## Runtime 경로

- Grade 배정 호출 위치: `Phase1BoardGenerator.TryGenerate()`가 Geometry 생성 성공 후 `Phase1GradeAssigner.TryAssign()`을 호출한다.
- Config 소스: `Assets/Settings/Phase1/Phase1GameConfig.asset`의 직렬화된 Grade 정의.
- Asset 우선순위: Runtime은 Config Asset 값을 사용하며 `EnsureDefaults()`가 누락 목록을 만들고 확정 MinCount를 동기화한다.
- Grade Seed: SHA-256 기반 `StableHash(BoardSeed, BagId, Difficulty, "PHASE1_GRADE")`의 결정적 정수 Seed.
- Board Random과 분리 여부: Geometry용 `System.Random(boardSeed)`와 Grade용 `System.Random(gradeSeed)`를 분리했다.
- Board 재시도와 Grade 관계: 기존과 같이 전체 생성 실패 시 호출자가 `seed + attempt * 37`로 재시도한다. 3,000 Seed에서 Grade 배정 실패는 0이었다.
- fallback 조건: 가중 후보가 없을 때 MaxCount를 위반하지 않는 최소 사용 Grade 중 결정적으로 선택한다. Brown 강제 편향은 제거했다.

## Min/Max 설정

| Grade | Easy Min/Max | Normal Min/Max | Hard Min/Max |
|---|---:|---:|---:|
| Beige | 1 / 무제한 | 1 / 무제한 | 1 / 무제한 |
| Brown | 1 / 무제한 | 1 / 무제한 | 1 / 무제한 |
| Gray | 1 / 무제한 | 1 / 무제한 | 1 / 무제한 |
| Marble | 1 / 2 | 2 / 3 | 3 / 5 |

기존 Base HP, Grade Modifier(+0/+2/+4/+7), Weight(20/30/25/25, 10/25/30/35, 5/15/35/45), Marble MaxCount는 변경하지 않았다.

## 개수 생성 방식

- 최소 개수 선배정: 활성 Grade별 MinCount를 먼저 개수 풀에 넣는다.
- 잔여 슬롯 Weight 추첨: 남은 슬롯만 기존 Difficulty별 Weight로 추첨한다.
- MaxCount 처리: MaxCount에 도달한 Grade를 후보에서 제외한다.
- fallback 처리: 유효 후보 중 사용 수가 가장 적은 Grade를 Grade Seed 기반 결정 순서로 선택한다.
- 동일 Seed 결정성: Bag, Difficulty, Board Seed 및 고정 Salt로 Grade Seed를 만들며 `UnityEngine.Random`을 사용하지 않는다.

## 공간 분산 방식

- 인접 판정: 기존 `Phase1PlacementValidator.EdgeTouch()`를 재사용하며 모서리 접촉은 제외한다.
- Tile 처리 순서: 인접 차수 내림차순, 동률은 Grade Seed 기반 결정 순서.
- 점수 우선순위: Marble 동일 Grade 공유 변, 전체 동일 Grade 공유 변, 최대 동일 Grade 연결 컴포넌트, 동일 Shape+Grade 공유 변 순의 사전식 최소화.
- Swap 최적화: 초기 배정 후 점수가 실제 개선되는 두 Tile의 Grade 교환만 반영한다.
- 최대 반복: 32회 또는 개선 후보 없음.
- 생성 실패 방지: 무제한 탐색·Backtracking을 쓰지 않고 기존 20회 Grade assignment 범위 안에서 유효 결과를 선택한다.

## 3,000 Seed 결과

- Easy: 1,000/1,000, 5,500 Tile, 생성 재시도 0, Grade 실패 0.
- Normal: 1,000/1,000, 7,500 Tile, 기존 Geometry 재시도 57, Grade 실패 0.
- Hard: 1,000/1,000, 9,500 Tile, 기존 Geometry 재시도 317, Grade 실패 0.
- 전체: 3,000/3,000, 22,500 Tile.
- 실패 Board: 0.
- MinCount 위반: 0.
- Marble Max 위반: 0.
- 전 Grade 등장 위반: 0.
- Geometry Signature 불일치: 0.
- 결정성 불일치: 0.
- Base HP 불일치: 0.
- HP 범위 위반: 0.
- Null Grade: 0.
- Modifier 총합 범위 위반: 0.
- fallback 호출: 0.

## Grade 분포

| 난이도 | Beige 평균 | Brown 평균 | Gray 평균 | Marble 평균 | Marble 최소 | Marble 최대 |
|---|---:|---:|---:|---:|---:|---:|
| Easy | 1.302 | 1.436 | 1.404 | 1.358 | 1 | 2 |
| Normal | 1.297 | 1.671 | 1.852 | 2.680 | 2 | 3 |
| Hard | 1.334 | 1.936 | 2.462 | 3.768 | 3 | 5 |

## 공간 분산 통계

| 난이도 | 평균 동일 Grade 공유 변 | 최대 공유 변 | 평균 Marble 공유 변 | 최대 연결 덩어리 |
|---|---:|---:|---:|---:|
| Easy | 0.000 | 0 | 0.000 | 1 |
| Normal | 0.000 | 0 | 0.000 | 1 |
| Hard | 0.013 | 1 | 0.001 | 2 |

- 동일 Grade 3개 이상 연결 Board: Easy 0, Normal 0, Hard 0.
- Marble 3개 이상 연속 연결 Board: Easy 0, Normal 0, Hard 0.
- 동일 Shape + 동일 Grade 인접: 전 난이도 0.
- 개선 Board: 1,794 (Easy 377, Normal 645, Hard 772).
- 동일 Board: 1,206 (Easy 623, Normal 355, Hard 228).
- 악화 Board: 0.

## 회귀검증

- Matrix: 120/120, Tile 900, minimum HP 위반 0, HP 불일치 0.
- Stress: 1,200/1,200, Tile 9,000, minimum HP 위반 0, HP 불일치 0.
- Board: 1,320/1,320.
- Tile: 9,900/9,900.
- Damage State: 1,140/1,140, 실제 범위 8~47.
- Timer: 39/39.
- HP 8~47: PASS.
- Expected Bag HP: 불일치 0.
- Runtime Compile: warning 0, error 0.
- Editor Compile: warning 0, error 0.
- Console: 최종 자동검증 `PASS`; 최종 코드 컴파일 error/warning 0. 구현 중 발생 후 수정된 과거 컴파일 로그는 최종 상태에 포함하지 않았다.
- Visual resource validation: 6 sizes × 4 damage sprites, 4 material textures, 5-state damage model PASS.
- Missing Reference: 일반 Scene 전체 검사는 미수행.
- Play Mode: 미수행.

## 미검증 항목

- MCP 연결을 사용할 수 없었고 안전한 난이도 순환 Play Mode 자동 경로가 없어 Easy/Normal/Hard 육안 플레이를 실행하지 않았다.
- Scene 전체 Missing Script/Reference 검사는 Scene 저장 위험 없이 수행할 기존 소형 검증 메뉴를 확인하지 못해 PASS로 기록하지 않았다.
- 검증은 열린 Unity Editor에서 기존 Phase 1 검증 메서드를 일회성으로 실행했으며, 임시 실행 훅은 결과 확인 직후 제거했다.

## 최종 결론

- 모든 Board에 Beige, Brown, Gray, Marble이 등장한다: PASS (3,000/3,000).
- Easy Marble 1~2: PASS.
- Normal Marble 2~3: PASS.
- Hard Marble 3~5: PASS.
- 같은 재질끼리 최소한으로 분산된다: PASS. 개선 1,794, 동일 1,206, 악화 0이며 3개 이상 연결 Board 0.
- Geometry가 변경되지 않는다: PASS, 3,000 Seed 불일치 0.
- Seed 결정성이 유지된다: PASS, 불일치 0.
- Final HP 8~47이 유지된다: PASS.
- 기존 Phase 1 회귀검증이 통과한다: PASS.
- 예상 밖 파일 변경이 없다: PASS (Git LFS 활성 최종 상태 기준).
- Scene, Prefab, Texture, Sprite, Phase 2/3, OUTGAME, ProjectSettings는 수정하지 않았다.
- Git add, commit, push는 수행하지 않았다.
