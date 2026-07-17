# Phase 3 흰색 구분선 및 Deck 영역 가림 방지 적용 보고

## 이번 단계 핵심 요약

- 최종 판정: 정적 구현 및 컴파일 PASS, Unity Play Mode 시각·입력 검증 필요
- Branch: `OutGame_Contec`
- HEAD: `b3848fe10603d7a027a722d3611d4589f26fc88d`
- 작업 전 Git 상태: INGAME Scene, Phase 1 코드·설정, Phase3TangramManager 및 기존 보고서/이미지가 이미 변경된 dirty 상태였으며 staged 변경은 없었다.
- 작업 후 Git 상태: 기존 변경을 보존한 채 이번 작업의 Phase 3 표시 코드·Validation·INGAME Scene 및 이 보고서가 추가 변경되었다.
- 이번 작업 수정 파일: `Phase3TangramGuide.cs`, `Phase3TangramPiece.cs`, `Phase3TangramManager.cs`, `Phase3TangramValidation.cs`, `INGAME.unity`, 본 보고서
- 예상 밖 변경: 없음. 기존 Phase 1·이전 INGAME 변경과 untracked PNG/meta는 건드리지 않았다.
- Staged/Commit/Push: 모두 0
- INGAME Scene 작업 전 SHA256: `D5F678C497BE265B7CE633A276AABC0F5D3C45D0505101A975A1A3E9B1DDD172`
- INGAME Scene 작업 후 SHA256: `1C6EB6DDD371933EF7CFF0E48ED6D5A8A50E540D7FB5315258B218803AD88F0F`

## 기존 구조

- Board Root: `Phase3TangramManager`가 `Phase3 Tangram Field`(1250×1250)를 Runtime 생성한다.
- Placed Piece Root: 별도 UI Root가 아니라 `Phase3 Tangram World Runtime` 아래의 각 `TangramPiece_{id}` MeshRenderer이며, 상태와 sortingOrder로 구분된다.
- Loose Piece Root: Placed와 동일한 World Runtime Root를 사용한다. Loose 전환 시 6000 이상 순서를 부여한다.
- Drag Piece Root: 별도 Transform 이동 없이 동일 World Runtime Root를 사용한다. 일반 Drag는 sortingOrder 32000, 다른 모든 Piece가 Placed인 마지막 조각 Drag는 Case 아래 전용 sortingOrder 9000을 사용한다.
- Partition Line Root: `Phase3 Tangram World Runtime/Phase3 Tangram Closed Guides`; `Phase3TangramGuide`가 폐곡선 LineRenderer를 생성한다.
- DeckPanel: Scene의 `Canvas/Game_UI_General/Middle_GameDeck` 계층이며 Runtime Deck UI의 host다.
- Deck Piece Root: Transform은 World Runtime Root에 유지되고, InDeck 상태의 MeshRenderer sortingOrder로 Deck UI 위에 표시된다.
- Img_GamePanelCase: `Canvas/Game_UI_General/Middle_GamePanel/Img_GamePanelCase`.
- Canvas 및 Sorting 구조: 루트 Canvas는 Phase 3 활성 시 Screen Space Camera로 전환된다. World Mesh/Line의 높은 sortingOrder가 같은 화면 영역의 UI sibling 순서보다 우선했던 것이 침범 원인이다.
- Deck 영역 침범 원인: Board와 Deck의 화면 Rect가 약간 겹치는 상태에서 Placed Piece(기존 4000대)와 Guide(기존 4900)가 루트 UI보다 앞에 렌더링되었다. 단순 sibling 문제나 canonical Board 크기 문제는 아니다.
- 자동 Scene 저장 위험: `IngameHudPersistenceFix`에는 수동 `MenuItem`만 있으며 InitializeOnLoad, delayCall, DidReloadScripts, playModeStateChanged, 자동 SaveScene/SaveAssets 경로는 없다.

## 해결 방식 비교

### 퍼즐 크기 변경

- 장점: 화면상 Deck과의 기하학적 겹침을 줄일 수 있다.
- 위험: Field/Board의 표시 Scale 또는 Rect를 건드리면 world 변환, press offset, drag, snap 거리, guide/shine 정렬과 1250×1250 기준선의 결합을 다시 검증해야 한다.
- canonical 좌표 영향: 데이터 16×16 또는 1250×1250 자체를 유지하더라도 화면 변환 계층에 영향을 준다.
- Snap 영향: 직접 계산식 변경이 없어도 screen/world 변환이 바뀌어 회귀 위험이 있다.
- 최종 선택 여부: 선택하지 않음.

### Clip·Mask·전경 가림

- 장점: canonical 좌표, 퍼즐 크기, Collider, Snap, Seed, Geometry를 변경하지 않고 시각적 겹침만 차단할 수 있다.
- 위험: RectMask2D/Mask는 현재 World MeshRenderer와 LineRenderer에 직접 적용되지 않는다. 무리하게 사용하면 Loose/Drag Piece까지 잘리거나 입력 계층이 꼬일 수 있다.
- 적용 가능 여부: UI Mask는 부적합. 기존 전경 UI를 독립 sorting Canvas로 승격하고 World Renderer 순서를 그 사이에 배치하는 방식은 적용 가능하다.
- 최종 선택 여부: 전경 가림 방식을 선택함.

## 최종 선택

- 선택한 방식: 플레이 중 Board·Placed·Partition Line은 GamePanelCase 아래, Deck Piece와 일반 Drag는 DeckPanel 위에 둔다. 클리어 시 Live World Puzzle을 숨기고 Completion Image와 Shine을 동일한 Completion Canvas(9000)에서 표시하며, 결과 Overlay는 기존 Transition 최상위 Canvas를 재사용한다.
- 선택 근거: RectMask2D가 World Renderer에 적용되지 않으며, 표시 크기 축소는 좌표·입력 회귀 위험이 있다. sorting 계층은 시각 순서만 바꾼다.
- canonical 좌표 변경: 0
- Puzzle 크기 변경: 0
- Snap 로직 변경: 0
- Geometry 변경: 0
- 생성기·Piece Count·Puzzle Seed·Shape Signature 변경: 0

## 구분선

- 변경 전 색상: `RGBA(0.35, 0.88, 1.0, 0.95)` 계열 청록색
- 변경 후 색상: `#FFFFFF`, alpha 1.0
- Alpha: 불투명 흰색
- Easy 표시 방식: 현재 Production 구현은 난이도별 별도 선형을 사용하지 않고 동일한 폐곡선 실선과 동일 width를 사용한다.
- Normal 표시 방식: Easy/Hard와 동일한 폐곡선 실선이다.
- Hard 표시 방식: Easy/Normal과 동일한 폐곡선 실선이다.
- 점선·실선 회귀: 기존 Production에 난이도별 점선 계약이 실제 구현되어 있지 않았다. 이번 작업은 기존 폐곡선·width·cap/corner 형태를 유지하고 색상만 변경했다.
- Deck 영역 표시 여부: Guide sortingOrder 4900, DeckPanel Canvas 20000이므로 DeckPanel 아래에 가려진다.
- Snap Highlight, Placed pop, target highlight, completion shine 색상 계약은 변경하지 않았다.

## 최종 렌더링 순서

뒤쪽부터 앞쪽 순서:

1. Phase 3 배경
2. Board 및 Placed Piece(4000+id)
3. Partition Line(4900)
4. Loose Piece(6000+sequence), Img_GamePanelCase Canvas(10000)
5. 마지막 조각 Drag 또는 Completion Image·Shine Canvas(9000), Img_GamePanelCase(10000), DeckPanel Canvas(20000), Deck Piece(25000+id), 일반 Drag Piece(32000), Settings Canvas(32750), Transition·결과 Canvas(32760)

필수 판정:

- Board·Placed·Line < Img_GamePanelCase: 충족
- Img_GamePanelCase < DeckPanel: 충족
- DeckPanel 영역 침범: sorting 계약상 Placed Piece와 Line은 DeckPanel 아래에 있으므로 차단
- 별도 Canvas 충돌: 기존 루트 Canvas는 유지했다. Case/Deck 및 최상위 Settings/Transition 계층만 overrideSorting Canvas로 분리했다.
- Runtime Sibling 충돌: Manager는 해당 Scene UI의 sibling/sortingOrder를 재설정하지 않는다. World Piece의 상태별 sortingOrder만 변경한다.
- Raycast: 새 Deck/Settings/Transition Canvas에 GraphicRaycaster를 배치했고, Case Canvas는 이벤트를 받지 않는다.

## 입력 및 기능 검증

- Drag: 일반 Drag 경로는 보존했다. 다른 모든 Piece가 Placed인 마지막 조각만 Drag 시작과 같은 프레임에 sortingOrder 9000으로 내려 Case 아래에 유지한다. Play Mode 미검증
- Rotate: 코드 변경 0. Play Mode 미검증
- Snap: 계산 변경 0. Play Mode 미검증
- Deck 복귀: 판정·slot/page 변경 0. Play Mode 미검증
- Deck 페이지: 코드 변경 0. Play Mode 미검증
- Press Offset: 코드 변경 0. Play Mode 미검증
- Loose Piece: 기존 6000+sequence 경로 보존. Play Mode 미검증
- Placed Piece: 기존 4000+id 의미를 상수로 보존. Play Mode 미검증
- 완료: 성공 Commit·점수·이벤트·ExitReady 순서를 유지한다. 완료 시 Live Placed Piece와 Guide를 숨기고 동일 Completion Canvas의 합성 이미지로 전환한 뒤, 최종 결과 Overlay를 표시한다. Play Mode 미검증
- Shine: 기존 Runtime Material과 0.75초 Coroutine 수명을 유지한다. Completion Image와 Shine을 sortingOrder 9000의 동일 Canvas에 배치했으며, Material unavailable 시 기본 UI pulse fallback을 재생한다. Play Mode 미검증
- Timer: 코드 변경 0. Play Mode 미검증
- Score: 코드 변경 0. Play Mode 미검증

## 난이도별 검증

- Easy: Piece Count/Generator/표시 형태 코드 변경 0. 실제 화면 미검증
- Normal: Piece Count/Generator/표시 형태 코드 변경 0. 실제 화면 미검증
- Hard: Piece Count/Generator/표시 형태 코드 변경 0. 실제 화면 미검증
- Placed Piece Deck 침범: 정렬 계약상 차단, 실제 화면 미검증
- Line Deck 침범: 정렬 계약상 차단, 실제 화면 미검증
- Deck Piece 가림: Deck Piece 25000+id > DeckPanel 20000 정적 확인, 실제 화면 미검증
- Deck 버튼 가림: Deck Canvas/Raycaster 정적 확인, 실제 클릭 미검증
- Mask 경계 깨짐: Mask를 사용하지 않아 World Renderer/Mask 비호환 경로 없음
- 시각적 점프: Piece 좌표·Transform·Scale 변경 0. 상태 전환의 기존 시각감은 Play Mode 확인 필요

### 최종 클리어 계층

- 마지막 Piece Snap 전 Root: `Phase3 Tangram World Runtime`. 별도 Drag Root로 이동하지 않으며, 마지막 남은 조각이면 sortingOrder 9000을 사용한다.
- Snap 후 Root: `Phase3 Tangram World Runtime`. Parent·sibling 변경 없이 상태만 Placed로 전환하고 sortingOrder를 `4000 + Piece ID`로 복원한다.
- 완성 퍼즐 Root: 실제 Piece의 부모는 계속 `Phase3 Tangram World Runtime`이며 이동하지 않는다. 완료 연출 중에는 Renderer만 숨기고 `Tangram Completion Root`의 합성 이미지로 전환한다.
- Partition Line Root: `Phase3 Tangram World Runtime/Phase3 Tangram Closed Guides`, sortingOrder 4900. 완료 이미지 전환 시 함께 숨긴다.
- Completion Shine Root: `Phase3Root/Phase3 Tangram Field/Tangram Completion Root/Tangram Completion Shine`. Image와 동일 Completion Canvas(9000)에서 Image 다음 sibling으로 렌더링되며 raycastTarget은 false다.
- Completion Image: `Phase3Root/Phase3 Tangram Field/Tangram Completion Root/Tangram Completion Image`. Completion Root 자체가 override sortingOrder 9000 Canvas이며 Image가 Shine보다 먼저 렌더링된다.
- Img_GamePanelCase Root: `Canvas/Game_UI_General/Middle_GamePanel/Img_GamePanelCase`, override sortingOrder 10000.
- 완료 중 Sibling 변경: 0. `SetAsLastSibling()`, `SetSiblingIndex()`, 완료 시 `SetParent()` 호출이 없다.
- 완료 시 Sorting 변경: 마지막 조각은 Drag 9000 → Placed 기본 5000대 → 명시적 `4000 + id` 순서로 같은 Snap 호출 안에서 복원된다. 다른 Placed Piece와 Guide의 order는 변경하지 않는다.
- DeckPanel 영향: 완료 시 DeckPanel 또는 Case를 비활성화하거나 재부모화하지 않는다. InDeck Piece만 완료 가시성 규칙으로 숨겨진다.
- 완료 Coroutine 종료: Shine Image만 비활성화하고 ExitReady를 한 번 발생시킨다. Completion Image는 유지되며 Live Piece·Guide의 부모는 변경하지 않는다.
- 완료 후 최종 렌더링 순서: Phase 3 배경 → 숨겨진 Live Puzzle → Completion Image → Completion Shine(9000) → Img_GamePanelCase(10000) → DeckPanel(20000) → Settings(32750) → Transition·결과 Overlay(32760).
- Case 바깥 노출 여부: 마지막 Drag와 Completion Visual Canvas가 모두 Case 10000 아래다. 실제 픽셀 노출 여부는 Play Mode 미검증이다.
- 성공·점수·Timer·ExitReady: terminal clear commit, Piece 점수, Clear 점수, PhaseCleared, Shine 종료, PhaseExitReady 호출 순서는 변경하지 않았다.
- 게임 완료 후: 기존 `PhaseTransitionOverlay`를 결과 화면으로 유지해 최종 점수와 `CLICK TO LOBBY`를 표시한다. 좌클릭·Primary Touch는 Build Settings에 등록된 `OUTGAME_LOBBY`를 한 번만 Load한다.
- Play Mode 검증 여부: 미실행. 판정은 `STATIC READY / FINAL CLEAR VISUAL PLAY MODE VALIDATION REQUIRED`다.

## Unity 검증

- Runtime Compile: `dotnet build Assembly-CSharp.csproj --no-restore` 경고 0, 오류 0
- Editor Compile: `dotnet build Assembly-CSharp-Editor.csproj --no-restore` 경고 0, 오류 0
- Console Error: 이번 작업 후 Unity Editor Refresh/Play Mode를 실행하지 않아 신규 Console 수치는 미검증
- Console Warning: 이번 작업 후 Unity Editor Refresh/Play Mode를 실행하지 않아 신규 Console 수치는 미검증
- Missing Script: Scene YAML의 null script/zero GUID 표식 0
- Missing Reference: Scene YAML 문서 289개/고유 289개, 내부 fileID 참조 누락 0
- Play Mode: 실행하지 않음
- 3,000 Seed: 현재 빌드 DLL의 읽기 전용 반복 검사를 시도했으나 프로세스 생성이 Windows 오류 5로 거부되어 실행 미검증. Generator 파일 diff는 0이며 생성 규칙·Seed·Geometry는 수정하지 않았다.
- `git diff --check`: 대상 Scene 및 Phase 3 코드 통과

## 미검증 항목

- Easy/Normal/Hard 실제 화면에서 흰 구분선 대비와 두께
- 실제 DeckPanel 픽셀 영역에서 Placed Piece/Line 침범 0
- Deck Piece·좌우 버튼의 실제 표시 및 Raycast
- Drag 중 Deck 경계 통과 시 가시성, Rotate, Snap, 복귀, 페이지 전환
- 완료 Shine, ExitReady, Timer, Score 회귀
- Unity Console Error/Warning와 3,000 Seed Runtime Validation

위 항목은 열린 Unity Editor에서 Scene 자동 저장 없이 사용자 Play Mode로 확인해야 한다. 확인하지 않은 항목은 PASS로 기록하지 않았다.

## 최종 결론

- 퍼즐 구분선이 흰색으로 변경되었는가: 코드상 예
- 난이도별 실선·점선 계약이 유지되는가: 실제 Production에는 난이도별 차등 계약이 없었으며, 기존 공통 폐곡선 실선 계약을 그대로 유지함
- 보드에 부착된 조각이 DeckPanel에 가리지 않는가: 의도는 반대로 DeckPanel이 Placed Piece를 가리도록 정렬했으며 정적 계약상 충족, 화면 확인 필요
- 구분선이 DeckPanel 영역에 표시되지 않는가: 정적 sorting 계약상 충족, 화면 확인 필요
- Deck Piece와 버튼이 정상 표시되는가: 정적 계층은 충족, Play Mode 미검증
- canonical 좌표와 Snap이 유지되는가: 관련 계산·생성기 변경 0
- 퍼즐 크기를 불안전하게 변경하지 않았는가: 크기·Scale 변경 0
- Drag·Rotate·Snap·Deck 복귀가 정상인가: 코드 경로 변경 0, Play Mode 미검증
- Img_GamePanelCase와 DeckPanel 계층 계약이 유지되는가: sortingOrder 10000 < 20000으로 명시
- 관련 없는 파일 변경이 없는가: 이번 작업에 의한 범위 외 변경 0. 작업 전부터 존재한 Phase 1·PNG/meta·이전 보고서 변경은 보존함
- 최종 상태: **STATIC READY / FINAL CLEAR VISUAL PLAY MODE VALIDATION REQUIRED**
