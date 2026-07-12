# Phase 1 비리소스 안정화 및 자동 검증 보고서

## 판정

**PARTIAL** — 에디터에서 자동화 가능한 생성·HP·피해·입력·점수·HUD·참조·빌드 설정 검증은 통과했다. 실제 Android 기기의 진동 및 물리 터치 감각은 기기 실행이 필요해 수동 검증으로 남겼다.

## 검증 환경

- 프로젝트: `HATAGONG_DES_11`
- Unity: `6000.3.19f1`
- 활성 씬: `Assets/Scenes/INGAME.unity`
- 씬 상태: 저장됨(`isDirty=false`)
- Build Settings: `INGAME` 한 개가 활성화되어 build index 0에 등록됨
- Unity 씬 검증: 문제 0, 누락 스크립트 0, 깨진 프리팹 0
- 최종 Console: Error 0, Warning 0

## 자동 검증 결과

| 항목 | 결과 | 근거 |
|---|---:|---|
| 난이도×시드 생성 매트릭스 | 120/120 PASS | 900 tiles, 최소 HP 위반 0, HP 불일치 0 |
| 생성 스트레스 | 1200/1200 PASS | 9000 tiles, 최소 HP 위반 0, HP 불일치 0 |
| Damage State 경계값 | 30/30 PASS | 매트릭스/스트레스 실행에서 각각 15/15 |
| Easy 입력·점수 smoke | PASS | 38 hits, 930 score, clear event 1회 |
| Normal 입력·점수 smoke | PASS | 55 hits, 1200 score, clear event 1회 |
| Hard 입력·점수 smoke | PASS | 69 hits, 1440 score, clear event 1회 |
| 입력 방어 | 3/3 PASS | 첫 포인터 허용, 멀티 포인터·디바운스·파괴 타일 차단, 다른 타일 허용 |
| HUD 별 상태 | 3/3 PASS | Easy 1개, Normal 2개, Hard 3개 채움 |
| Fixed Seed 재현 | PASS | 디버그 재생성 전후 Bag/Layout/Variant 동일 |
| 씬/프리팹 참조 | PASS | Unity validate 결과 누락 및 파손 0 |

최종 생성 검증 합계는 **1320/1320 보드, 9900 타일**이다.

## 발견 및 최소 수정 사항

1. Build Settings가 존재하지 않는 `SampleScene`을 가리키던 문제를 수정해 `INGAME`을 index 0으로 등록했다.
2. 배치 생성 fallback 탐색이 후보를 찾은 뒤에도 계속 순회하던 제어 흐름을 중단하도록 수정했다.
3. Fixed Seed 디버그 재생성이 Shuffle Bag 이력 때문에 다른 보드를 만들던 문제를 수정했다. 디버그 이력 무시 시 현재 Bag ID와 고정 시드를 그대로 재사용한다.
4. 실제 배치 규칙과 중복된 3-in-line 검사를 제거하고 동일 Shape 연결 성분 3개 이상 금지 규칙만 유지했다.
5. 스프라이트/fallback 선택 결과가 `Phase1BoardState` 로그와 동기화되도록 보완했다.
6. 검증 메뉴에 120/1200 보드, HP 일관성, Damage State, 입력 차단 및 clear event 단일 발생 검사를 추가했다.

## 변경 파일

- `Assets/Scripts/Phase1/Editor/Phase1PrototypeSetup.cs` — 자동 검증 범위 확장
- `Assets/Scripts/Phase1/Generation/Phase1PlacementValidator.cs` — 중복 검증 제거
- `Assets/Scripts/Phase1/Runtime/Phase1BoardController.cs` — fallback 및 Fixed Seed 재현 안정화
- `Assets/Scripts/Phase1/Runtime/Phase1TileView.cs` — 시각 fallback 상태 동기화
- `ProjectSettings/EditorBuildSettings.asset` — `INGAME` 씬 등록
- `Docs/PHASE1_NON_RESOURCE_VALIDATION_REPORT.md` — 본 보고서

리소스(sprite/audio/texture), fallback 색상, UI 레이아웃은 변경하지 않았다. 사용자 소유의 미추적 `.vscode/`도 건드리지 않았다. 커밋 및 푸시는 수행하지 않았다.

## 잔여 수동 검증

- 실제 Android 기기에서 진동 발생 여부
- 실제 터치 환경의 멀티터치 체감 및 연타 디바운스 감각
- 기기 해상도별 최종 시각 확인

레거시 `Phase1TileVisualDefinition`/`visuals` 직렬화 필드는 현재 등급 기반 런타임 시각 선택 경로에서 사용되지 않지만, 기존 직렬화 데이터 마이그레이션 위험을 피하기 위해 제거하지 않았다.
