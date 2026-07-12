# Phase 2 개발 중 후순위 작업

이 문서는 Phase 2 시멘트 도포 시스템의 설계·조사·구현 과정에서 순수 논리 코어와 기본 플레이 완성을 위해 의도적으로 미룬 작업을 관리한다.

Phase 2 순수 논리 코어는 아래 후순위 작업과 분리해서 구현한다.

## 공용 GameRunContext 및 Difficulty

- 상태: DEFERRED
- 분류: 공용 시스템
- 보류 이유: 128×128 논리 코어는 Difficulty를 외부 입력으로 받으면 Context 없이 구현·검증할 수 있다.
- 현재 임시 처리: Config 또는 Model 생성 시 Difficulty를 명시적으로 주입한다.
- 재개 시점: Phase 1→2 실제 Scene·Flow 통합 전.
- 선행 조건: 인게임 초기화 책임과 Phase 1 serialized Difficulty 이관 방식 결정.
- 영향 범위: Phase 1·2·3 난이도, Request 초기 조건, Editor 직접 실행.
- 구현 시 주의사항: 인게임 시작 시 1회 확정해 세 Phase가 공유한다. Phase 2가 Phase1BoardController에서 읽거나 Phase별 기본값을 만들지 않는다.

## 공용 Current Phase Flow

- 상태: DEFERRED
- 분류: 공용 시스템 / Phase Flow
- 보류 이유: current IGamePhase 소유와 Root 교체는 순수 논리 코어와 무관하다.
- 현재 임시 처리: 실제 Phase1Cleared를 Phase 2로 연결하지 않는다.
- 재개 시점: Phase 1→2 실제 전환 연결 전.
- 선행 조건: Phase2 Adapter, Root, IsReady.
- 영향 범위: 입력 차단, midpoint 교체, 전환 성공 후 활성 Phase.
- 구현 시 주의사항: Phase 1 입력만 다시 여는 현재 GameSessionController 구조를 그대로 사용하지 않는다.

## Preparing 단계 Prepare 계약

- 상태: DEFERRED
- 분류: 공용 시스템 / 초기화
- 보류 이유: Grid 외 RT·Material·Scene 참조는 시각 통합 단계의 책임이다.
- 현재 임시 처리: 순수 논리 코어는 Scene 초기화와 독립적으로 생성한다.
- 재개 시점: RenderTexture와 Scene 통합 전.
- 선행 조건: Flow bootstrap과 실패 처리 정책.
- 영향 범위: READY 시작, Grid, RT, Material, 참조, IsReady.
- 구현 시 주의사항: midpoint에서 무거운 초기화를 처음 하지 않고 READY 전에 준비한다.

## Production 시멘트 완료 텍스처

- 상태: WAITING_FOR_RESOURCE
- 분류: Phase 전용 / 리소스
- 보류 이유: CementFilledLayer의 최종 카툰풍 텍스처가 없다.
- 현재 임시 처리: 논리 단계에서는 사용하지 않고 기술 프로토타입은 단색만 허용한다.
- 재개 시점: Phase 2 시각 제작 기준 확정 후.
- 선행 조건: 타일링, 색상, 해상도와 모바일 압축 기준.
- 영향 범위: CementFilledLayer.
- 구현 시 주의사항: TMP Example의 Floor Cement를 Production 리소스로 채택하지 않는다.

## Production 미도포 바닥 텍스처

- 상태: WAITING_FOR_RESOURCE
- 분류: Phase 전용 / 리소스
- 보류 이유: UnpaintedTopLayer의 빈 바닥·거친 콘크리트 최종 표현이 없다.
- 현재 임시 처리: 논리 코어에는 시각 리소스를 넣지 않는다.
- 재개 시점: Phase 2 시각 통합 단계.
- 선행 조건: Mask Shader의 base texture 요구사항.
- 영향 범위: UnpaintedTopLayer.
- 구현 시 주의사항: Paint mask와 논리 Grid 좌표에 종속시키지 않는다.

## Paint Mask Shader 및 Material

- 상태: BLOCKED
- 분류: Phase 전용 / 그래픽
- 보류 이유: Source Alpha × (1 - Paint Mask), Soft Brush와 persistent 누적을 지원하는 기존 Shader가 없다.
- 현재 임시 처리: 논리 진행도는 RenderTexture 없이 128×128 Grid로 계산한다.
- 재개 시점: Phase 2 시각 기술 검증 단계.
- 선행 조건: R8/RGBA8, BlendOp Max 또는 composite 방식 결정.
- 영향 범위: RawImage, Mask RT, Stamp batch.
- 구현 시 주의사항: 진행도를 RT 픽셀 readback으로 계산하지 않는다.

## 모바일 RenderTexture 및 Blend 검증

- 상태: BLOCKED
- 분류: 플랫폼 검증
- 보류 이유: Windows Editor의 R8 지원은 Android/iOS 증거가 아니다.
- 현재 임시 처리: 설계상 R8 primary와 RGBA8 fallback만 기록한다.
- 재개 시점: Shader 기술 프로토타입 이후.
- 선행 조건: Android Vulkan/OpenGLES3, iOS Metal 빌드.
- 영향 범위: R8_UNorm, BlendOp Max, 1024 RT 메모리·발열·성능.
- 구현 시 주의사항: 실기기 결과 없이 모바일 PASS로 보고하지 않는다.

## RenderTexture 유실 및 Stamp History 복구

- 상태: DEFERRED
- 분류: Phase 전용 / 안정성
- 보류 이유: 기본 visual stamp와 RT 생명주기가 아직 없다.
- 현재 임시 처리: 순수 논리 Grid를 RT와 독립시킨다.
- 재개 시점: 시각 시스템 기본 Stamp 안정화 후.
- 선행 조건: History 형식과 Focus/Pause/IsCreated 처리.
- 영향 범위: RT 재생성, visual replay, Phase Reset.
- 구현 시 주의사항: replay가 점수·진행도·Clear를 다시 발생시키지 않는다.

## Progress Gauge 최종 스타일

- 상태: WAITING_FOR_RESOURCE
- 분류: Phase 전용 / UI
- 보류 이유: Track/Fill Sprite, 최종 색상과 장식이 없다.
- 현재 임시 처리: 상단 Stretch, Gauge Bar, 우측 정수 0~100%, 단색 Image 프로토타입을 허용한다.
- 재개 시점: Phase 2 시각 통합 및 UI 검수 단계.
- 선행 조건: Production UI 스타일.
- 영향 범위: Phase2Root ProgressUI.
- 구현 시 주의사항: 모든 Graphic과 TMP를 raycastTarget=false로 둔다.

## Safe Area와 실제 화면 비율 검증

- 상태: DEFERRED
- 분류: UI / 플랫폼 검증
- 보류 이유: Phase2Root와 Progress가 아직 Scene에 없다.
- 현재 임시 처리: 1250 보드와 권장 Progress RectTransform만 문서화한다.
- 재개 시점: Phase2Root와 Progress 추가 후.
- 선행 조건: 모바일 Game View와 실기기.
- 영향 범위: Cutout, Progress 위치, 보드 전체 노출.
- 구현 시 주의사항: 기존 HUD RectTransform을 임의 변경하지 않는다.

## 완료 광택 최종 시각 확정

- 상태: WAITING_FOR_DESIGN
- 분류: Phase 전용 / 연출
- 보류 이유: Production 시멘트 텍스처가 없어 Sheen의 Alpha·방향·폭을 판단할 수 없다.
- 현재 임시 처리: 별 Sparkle은 사용하지 않고 낮은 Alpha의 Soft Wet Sheen 1회만 후보로 유지한다.
- 재개 시점: Production 시멘트 텍스처 적용 후 시각 검수.
- 선행 조건: 완성 화면 아트 리뷰.
- 영향 범위: CompletionSheen.
- 구현 시 주의사항: Progress보다 아래에서 입력을 막지 않도록 한다.

## Phase 2 사운드·파티클·진동

- 상태: WAITING_FOR_RESOURCE
- 분류: Phase 전용 / 연출
- 보류 이유: 도포음, 입자, 진동과 완료 사운드 기준이 없다.
- 현재 임시 처리: 논리·입력·마스크·Clear를 무음으로 검증한다.
- 재개 시점: 기본 플레이 안정화 후.
- 선행 조건: 공용 피드백 및 성능 예산.
- 영향 범위: Stamp, milestone, Clear.
- 구현 시 주의사항: 시각/청각 피드백이 논리 mutation을 소유하지 않는다.

## Phase 2 Item

- 상태: WAITING_FOR_DESIGN
- 분류: 공용 시스템 / Item
- 보류 이유: 흙손, 시멘트 바스킷, 수량·소비·효과 지속과 UI가 미정이다.
- 현재 임시 처리: Item 기능을 연결하지 않는다.
- 재개 시점: 전체 Item 시스템을 한 번에 설계할 때.
- 선행 조건: 공용 Item 소유권과 소비 정책.
- 영향 범위: Stamp source, 진행도, 점수, UI.
- 구현 시 주의사항: Item도 동일 Production Stamp 경로를 사용하며 별도 진행도·점수를 만들지 않는다.

## Phase 2→3 실제 전환

- 상태: WAITING_FOR_PHASE3
- 분류: 공용 시스템 / Phase Flow
- 보류 이유: Phase 3 Controller, Root, Adapter와 IsReady가 없다.
- 현재 임시 처리: Phase2Cleared와 안전 잠금 상태까지만 구현한다.
- 재개 시점: Phase 3 공통 계약 구현 후.
- 선행 조건: Phase 3 준비·실패 정책.
- 영향 범위: Checked Transition, HUD, Timer, 입력.
- 구현 시 주의사항: 가짜 Phase 3 Root나 빈 화면을 만들지 않는다.

## Phase 2 임시 점수 재조정

- 상태: WAITING_FOR_PHASE3
- 분류: 공용 시스템 / 점수
- 보류 이유: 전체 Phase 점수 비중이 확정되지 않았다.
- 현재 임시 처리: Coverage Budget 500, 25% +100, 50% +150, 75% +200, Clear +500을 설계 기준으로만 유지한다.
- 재개 시점: Phase 3 구현 후 전체 밸런스 검토.
- 선행 조건: 실제 플레이 시간과 점수 telemetry.
- 영향 범위: GameScoreController와 최종 점수.
- 구현 시 주의사항: 현재 값은 최종 밸런스가 아니다.

## Request의 Phase 2 실제 효과

- 상태: WAITING_FOR_DESIGN
- 분류: 공용 시스템 / Request
- 보류 이유: Normal/Sudden의 Phase 2 보정 규칙이 없다.
- 현재 임시 처리: RequestType, 텍스트와 아이콘만 세션에서 유지한다.
- 재개 시점: Phase 1~3 기본 메커니즘 완료 후.
- 선행 조건: 전체 세션 Request 설계.
- 영향 범위: 도포 규칙, 점수, Timer.
- 구현 시 주의사항: Phase 2가 별도 Request 상태를 소유하지 않는다.
