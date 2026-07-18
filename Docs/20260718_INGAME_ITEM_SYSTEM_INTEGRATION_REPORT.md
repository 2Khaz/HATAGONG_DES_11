# INGAME 아이템 시스템 통합 보고서

작성일: 2026-07-18
대상 프로젝트: `C:\Project\HATAGONG_DES_11`

## 1. 작업 전 구조 감사

- INGAME은 `GameSessionController`가 `Preparing → Ready → Playing → Transitioning/Expired/Completed` 상태를 소유한다.
- Phase 전환은 `IGamePhase.PhaseExitReady`와 `GamePhaseTransaction`을 통해 처리된다.
- 제한시간은 `GameCountdownTimer`와 `GameTimerController`, 점수는 `GameScoreController`가 소유한다.
- Phase 1 타격의 정상 진입점은 `Phase1BoardController.TryHitDetailed`, Phase 2 도포는 `Phase2PhaseAdapter`와 `Phase2PaintOrchestrator`, Phase 3 배치·회전·완료는 `Phase3TangramManager`가 소유한다.
- 영구 진행도는 `PlayerProgressRepository`의 단일 JSON 파일 `hatagong-player-progress-v1.json`을 사용한다.
- 최종 의뢰 성공은 Phase 3 ExitReady 후 `GameSessionController.CompleteGame`에서 확정된다.

## 2. 기존 Item 버튼 계층 및 RectTransform 보존

디스크에 직렬화된 `Assets/Scenes/INGAME.unity`를 재검사한 결과 `Item_ValuePanel01~03` 이름의 GameObject는 존재하지 않고 수량 TMP가 버튼 직속 자식이다. 사용자가 제시한 Editor 화면의 Panel 계층은 현재 디스크 YAML과 일치하지 않으므로, 미저장 Editor 변경 또는 서로 다른 시점의 Scene 표시로 판정한다. Unity를 강제 실행·저장하지 않았으므로 미저장 Hierarchy를 디스크 내용으로 덮어쓰지 않았다.

| 슬롯 | 위치 | 크기 | Anchor/Pivot | 기존 자식 |
|---|---:|---:|---|---|
| Item_Button01 | (40, -2214) | 330×284 | Min/Max/Pivot (0,1) | Item_Icon01, Item_Value01 |
| Item_Button02 | (382, -2214) | 330×284 | Min/Max/Pivot (0,1) | Item_Icon02, Item_Value02 |
| Item_Button03 | (724, -2214) | 330×284 | Min/Max/Pivot (0,1) | Item_Icon03, Item_Value03 |

세 버튼의 Scale은 모두 `(1,1,1)`이다. `Item_Value01~03`은 각각 해당 버튼 직속 자식이며 위치 `(-72,72)`, 크기 `60×60`, Anchor `(1,0)`, Pivot `(0.5,0.5)`이다. 컨트롤러는 버튼 아래를 재귀 탐색하므로 Panel이 있는 미저장 Hierarchy에서도 같은 이름의 TMP를 찾지만, 버튼·Panel·TMP를 삭제·이동·재생성하지 않는다. 버튼 위치·크기·Anchor·Pivot·간격, 하단 HUD 및 설정 UI는 수정하지 않았다.

## 2-1. 컨트롤러 생성·참조 진입점

- `BeforeSceneLoad` 런타임 초기화에서 `SceneManager.sceneLoaded`를 제거 후 재등록한다.
- 초기 Scene이 INGAME인 경우 `AfterSceneLoad`에서도 현재 Scene을 확인한다.
- 이후 OUTGAME→INGAME 또는 INGAME 재진입은 `sceneLoaded`가 전달한 정확한 Scene의 Root만 검색한다.
- Scene 이름이 정확히 `INGAME`이고 로드 완료 상태일 때만 그 Scene의 `GameSessionController`에 컴포넌트를 추가한다.
- 같은 owner에 기존 `IngameItemSystemController`가 있으면 추가하지 않고, `Awake`의 정적 `Instance` 검사도 이중 인스턴스를 제거한다.
- 버튼은 초기화 시 `Item_Button01~03`을 한 번 찾고, 아이콘과 TMP는 각 버튼 자식에서 정확한 이름으로 한 번 찾아 캐시한다. 매 프레임 UI 이름 검색은 없다.
- Timer/Phase 2/Phase 3 참조도 `Awake`에서 비활성 포함 한 번 확보한다. Phase 1 타일 검색은 Hammer/Scraper 활성 가능 대상 판정 시에만 수행한다.
- `GameCompleted`와 `SessionStateChanged`는 `Awake`에서 구독하고 `OnDestroy`에서 해제한다. Button Listener도 슬롯당 한 번 등록하고 `OnDestroy`에서 제거한다.

## 3. 변경 파일

- `Assets/Scripts/GameFlow/Runtime/GameCountdownTimer.cs`
- `Assets/Scripts/GameFlow/Runtime/GameTimerController.cs`
- `Assets/Scripts/GameFlow/Runtime/Phase2PhaseAdapter.cs`
- `Assets/Scripts/GameFlow/UI/IdleGameplayGuidePresenter.cs`
- `Assets/Scripts/Outgame/Runtime/PlayerProgressData.cs`
- `Assets/Scripts/Outgame/Runtime/PlayerProgressRepository.cs`
- `Assets/Scripts/Phase1/Runtime/Phase1BoardController.cs`
- `Assets/Scripts/Phase1/Runtime/Phase1InputController.cs`
- `Assets/Scripts/Phase1/Runtime/Phase1TileView.cs`
- `Assets/Scripts/Phase1/Runtime/Phase1TouchEffectController.cs`
- `Assets/Scripts/Phase1/Runtime/Phase1TouchEffectView.cs`
- `Assets/Scripts/Phase2/Runtime/Phase2PaintGrid.cs`
- `Assets/Scripts/Phase2/Runtime/Phase2PaintOrchestrator.cs`
- `Assets/Scripts/Phase3Tangram/Phase3TangramManager.cs`

## 4. 신규 파일

- `Assets/Scripts/GameFlow/Runtime/IngameItemSystemController.cs`
- `Assets/Scripts/GameFlow/Runtime/IngameItemSystemController.cs.meta`
- `Docs/20260718_INGAME_ITEM_SYSTEM_INTEGRATION_REPORT.md`

## 5. 아이템 ID·이미지·Phase·슬롯 매핑

| ID | 이미지 | Phase/슬롯 |
|---|---|---|
| Stopwatch | Img_icon_item1 | 모든 Phase / 03 |
| Hammer | Img_icon_item2 | Phase 1 / 01 |
| TileGrinder | Img_icon_item3 | Phase 3 / 02 |
| TileCutter | Img_icon_item4 | Phase 3 / 01 |
| CementBasket | Img_icon_item5 | Phase 2 / 02 |
| Trowel | Img_icon_item6 | Phase 2 / 01 |
| Scraper | Img_icon_item7 | Phase 1 / 02 |

Resolver는 각 ID를 명시적 `Resources/Ingame/Item/Img_icon_itemN` 경로로 연결한다. 번호 계산이나 배열 순서 추정은 사용하지 않는다. 모든 아이콘은 `preserveAspect=true`다.

## 6. 아이템 7종 효과

- 스톱워치: Running 상태에서 남은 시간에 5초를 더하고 99초로 Clamp한다. Expired 타이머는 되살리지 않는다.
- 망치: 다음 유효 직접 타격 8회의 피해만 2로 전달한다. 기존 HP·DamageState·점수·파괴 경로를 사용하며, 해당 직접 타격의 기존 터치 이펙트만 금빛(`#FFB814` 계열)으로 Tint한다. 일반 타격은 흰색을 유지하고 풀링된 이펙트는 종료 시 흰색으로 초기화한다.
- 스크래퍼: 다음 유효 직접 타격 8회마다 직접 타일을 제외한 공격 가능한 생존 타일을 중복 없이 최대 2개 선택해 피해 1을 적용한다. 각 추가 피해가 성공하면 대상 타일 Rect의 실제 월드 중앙에서 기존 흰색 터치 이펙트를 1회 재생한다. 이 시각 피드백은 추가 피해 체인을 다시 호출하지 않는다.
- 흙손: Phase 2 Difficulty 기본 반지름에 1.4배를 적용하고 게임 시간 6초 후 정확히 기본값으로 복구한다.
- 시멘트 바스킷: 사용 시점 미도포 셀의 비결정 난수 8~12%를 목표로 한다. 무작위 미도포 Seed 셀 내부의 한 중심에서 각 미도포 셀 중심까지 정규화 거리를 계산하고 목표 수에 가장 가까운 단일 반지름을 선택한다. 최종 적용은 셀별 Stamp 목록이 아니라 기존 플레이어 Brush와 동일한 `RequestStamp` 한 번이며, 논리 Grid와 GPU Mask가 같은 원형 Stamp를 처리한다. 아이템 클릭 자체는 게임 입력 안내를 숨기는 입력으로 통지하지 않는다.
- 타일 커터: Drag 중이 아닌 미배치 조각 하나를 해당 Assignment의 정답 위치·정답 45도 회전으로 배치하고 자동 배치 점수 100을 지급한다.
- 타일 그라인더: Drag 중이 아닌 미배치 조각 중 회전 보정이 필요한 대상을 최대 2개 선택해 정답 회전만 적용한다.

## 7. 지속형·즉발형 분류

- 지속형: Hammer, Scraper, Trowel
- 즉발형: Stopwatch, CementBasket, TileCutter, TileGrinder

## 8. ActiveItem 단일 잠금

`IngameItemSystemController.activeItem` 한 곳에서만 잠금을 소유한다. 지속형 활성 중에는 활성 버튼을 포함한 세 버튼 모두 재사용할 수 없으며 다른 즉발형도 차단한다. 전환·만료·완료·Scene 파괴 시 활성 상태를 제거한다. 설정 Pause 동안에는 Trowel의 `Time.deltaTime` 진행도 정지한다.

## 9. ActiveOutline

버튼 자식 `ActiveOutline`은 실제 표시 책임에서 제외하고 존재할 경우 비활성화만 한다. 실제 활성 표시는 아이템 버튼 최상위 Canvas의 단일 `ItemActiveOverlayRoot`가 담당한다. Overlay는 Canvas Stretch·Offset 0·마지막 Sibling이며 GraphicRaycaster 없이 기본 UI Image 6개로 버튼 내부 14px 금빛 테두리를 그린다. 활성 버튼의 World Corner를 Overlay Local 좌표로 변환해 해상도와 CanvasScaler 변화에 대응한다. 수량 Panel과 TMP의 Union Rect에 10px Padding을 적용하고 하단·우측 Border를 분할해 수량 영역을 가리지 않는다. 첫 프레임 Alpha 1.0, 이후 0.65~1.0 펄스이며 모든 Graphic은 Raycast를 차단하지 않는다.

## 10. 수량 저장

기존 `PlayerProgressRepository`와 동일 파일을 사용한다. 데이터 Version은 2이며 v1 데이터의 `ClearedStageCount`를 보존하면서 아이템 필드가 없는 경우에만 각 5개로 초기화한다. 모든 값은 저장 전 0~99로 Clamp한다. 소비·보상 직후 원자적 임시 파일 교체 방식으로 저장한다.

## 11. 사용·차감 계약

Playing, 현재 Phase Running, 입력 허용, 수량 1 이상, ActiveItem 없음, 실제 대상 존재 조건을 모두 만족해야 한다. 실제 효과 메서드가 성공한 경우에만 `TryConsumeItem`을 호출한다. 스톱워치는 Timer가 Running이고 실제 `RemainingSeconds < 99`일 때만 성공한다. 따라서 99초·Expired는 실패 및 미차감, 98초는 99초, 95초와 94초는 99초, 93초는 98초가 된다. 기존 `Ceiling` 표시, Pause Tick 정지, Expired 부활 금지와 만료 이벤트 1회 계약은 변경하지 않았다. 버튼 Listener는 컨트롤러 인스턴스당 한 번 등록하고 `OnDestroy`에서 해제한다.

## 12. 전체 클리어 보상

`GameCompleted`에만 연결하며 Session 로컬 `rewardGranted`로 중복 지급을 차단한다. `GameSessionModel`은 Completed를 terminal state로 유지해 같은 Session에서 `GameCompleted`를 한 번만 발생시키고, 결과 Overlay 재표시는 이벤트를 다시 호출하지 않는다. Controller 재생성 시에도 이미 Completed인 Model은 이벤트를 재발행하지 않는다. 실패 Retry는 Expired 상태에서만 새 INGAME Session을 만들기 때문에 Retry 동작 자체에는 지급이 없다. 성공 후에는 Active Request를 Clear하고 로비로 이동하며, 다음 신규 의뢰의 새 Session은 다시 보상 가능하다. Hammer/Trowel/TileGrinder +2, Scraper/Stopwatch/CementBasket/TileCutter +1이며 각 99로 Clamp한 뒤 한 번 저장한다.

## 13. 전환·Retry·실패·로비 복귀

Session 상태가 Playing을 벗어나면 지속 효과를 정리한다. Phase ID 변경 시 Trowel 반지름과 활성 잠금·Outline을 초기화하고 새 Phase 슬롯을 다시 매핑한다. Retry와 로비 복귀는 Scene 파괴 시 `OnDestroy` 정리를 거치며 소비 수량은 영구 저장되어 복구되지 않는다.

## 14. 점수

아이템 자체 점수는 추가하지 않았다. Phase 1 추가 피해와 Phase 2 추가 도포는 기존 점수 경로를 사용한다. Grinder와 Stopwatch는 0점이다. Cutter만 지정된 자동 배치 점수 100을 사용한다. 기존 Phase Clear 점수와 완료 이벤트 방어는 유지했다.

## 15. 난수

Scraper, CementBasket, Cutter, Grinder의 대상 선택은 `UnityEngine.Random.Range`의 런타임 비결정 난수를 사용한다. `Phase1Seed`, `PermanentSeed`, `Phase3Seed` 또는 Request Snapshot을 참조하지 않는다. 기존 CSV Seed와 Phase 3 이미지 Resolver는 수정하지 않았다.

## 16. 테스트 결과

- Scene 버튼 존재·좌표·크기: PASS
- 이미지 7개 PNG/meta 및 Sprite Import: PASS
- 이미지 명시 매핑: PASS
- Phase별 슬롯 순서: PASS
- 단일 잠금·Outline Raycast 차단: PASS
- 저장 Clamp·소비·보상 API: PASS
- Timer/Phase 1/2/3 효과 진입점: PASS
- 정답 회전 탐색: PASS
- 최초 구현 독립 정적 검사: 55/55 PASS
- 집중 재검토 표적 정적 검사: 33/33 PASS
- Play Mode 피드백 표적 계약 정적 검사: 32/32 PASS
- 두 번째 Play Mode 피드백 표적 정적 검사: 33/33 PASS
- 네 번째 Play Mode 피드백 최종 구조 전환 정적 검사: 42/42 PASS
- Basket 128×128 수치 시뮬레이션: 600/600 PASS, 실제 비율 8.0017%~11.9949%, 목표 최대 오차 1셀

## 17. 컴파일

- 독립 Runtime Roslyn 재검증: warning 0 / error 0
- 독립 Editor Roslyn 재검증: warning 0 / error 0
- Unity Editor 실제 컴파일: 미실행·미검증

## 18. 정적 Validation

이번 집중 재검토에서는 Scene YAML 계층·RectTransform, Panel 부재와 TMP 부모, INGAME Scene load 진입점, 중복 인스턴스·Listener·Outline 방어, Stopwatch 경계, Basket 경계와 선택 방식, 보상 범위, 아이콘 명시 매핑 및 작업 범위 diff를 별도 검사해 33/33 PASS했다. 기존 55/55 수치를 재사용하지 않았다. Runtime Fixture Scene과 임시 Scene은 만들지 않았다.

Play Mode 피드백 수정 후에는 망치 Tint와 풀 초기화, 스크래퍼 타일 중앙 피드백과 재귀 방지, Basket 8방향 Frontier·거리 보충·단일 Batch, Phase 3 조각/완성 이미지 분리, 직접 실행 기본 `Img_bigtiles1`, Phase 안내 최초 표시/첫 실제 입력 종료/아이템 클릭 제외, ActiveOutline Sibling·Alpha·Raycast 계약을 새로 32개 검사해 32/32 PASS했다.

## 19. Play Mode

미실행. 현재 작업 트리에 사용자의 Scene 변경이 존재하고 자동 저장 위험이 있어 Unity를 강제 실행하지 않았다. Play Mode PASS로 기록하지 않는다.

## 20. 수동 확인 체크리스트

- 각 Phase 진입 시 3개 슬롯 순서와 실제 아이콘·수량
- 아이템 0개 상태와 지속형 활성 중 버튼 잠금
- 현재 Editor의 미저장 Hierarchy에 `Item_ValuePanel01~03`가 실제로 있는지와 저장 전 의도 확인
- OUTGAME에서 INGAME 최초 진입 및 INGAME Retry 때 컨트롤러가 정확히 1개인지
- ActiveOutline 펄스와 아이콘/TMP 비가림 및 재진입 중복 0
- 일반 Phase 1 타격은 흰색, Hammer 직접 타격은 금빛, 8회 종료 직후 흰색 복구
- Scraper 추가 대상 0/1/2개 각각의 타일 중앙 이펙트와 추가 체인 0
- Hammer/Scraper 8회 및 무효 타격 미차감
- Trowel 6초, 설정 Pause 동안 정지, 복구 반지름
- Basket 8~12%가 한 연결 덩어리로 보이는지, 도포 장벽 분리 시 Seed 근거리 보충인지, 완료 이벤트 1회인지
- Phase 3 시작/Deck/Dragging/Loose/Placed 조각이 기존 단색인지
- 직접 실행 완료 이미지는 Img_bigtiles1인지, Request Key 1/2/3별 완료 이미지와 Shine가 해당 Sprite인지
- Cutter 마지막 조각 완료·100점·완료 Shine
- Phase 시작 직후 안내가 보이고 실제 게임 영역 첫 입력에서만 숨는지, 아이템 버튼만 눌렀을 때 유지되는지
- Basket 버튼 직후 추가 터치 없이 도포 결과가 보이는지와 한 중심 주변 compact blob인지
- Hammer/Scraper/Trowel 활성 슬롯은 정상 밝기·금빛 내부 Outline인지, 다른 두 슬롯만 어두운지
- Hammer/Scraper/Trowel에서 최상위 Canvas Overlay가 실제 보이고 수량 Panel/TMP를 가리지 않는지
- Retry 후 `ItemActiveOverlayRoot` 1개, Border Segment 6개 이하, 기존 버튼 자식 Outline 비활성인지
- Grinder 위치/Deck 상태 불변
- 실패 Retry 후 소비 수량 유지
- 최종 성공 보상 1회 및 99 Clamp

## 21. 알려진 위험과 후속 개선

- Unity Editor 실제 컴파일, Play Mode와 실제 모바일 입력은 이번 작업에서 실행 검증하지 않았다.
- 저장 장치 쓰기 실패가 효과 적용 직후 발생하면 오류 로그를 남기고 지속형 효과는 즉시 해제하지만 즉발 효과 자체는 롤백할 수 없다.
- 디스크 Scene에는 요구서 예시의 `Item_ValuePanel01~03`가 없지만 제공된 Editor 화면에는 보인다. 미저장 Hierarchy는 Unity를 실행·저장하지 않고는 확정할 수 없으며, 현재 탐색 코드는 두 계층 모두 지원한다.

이번 집중 재검토에서 Production 수정은 `IngameItemSystemController.cs` 한 파일뿐이다. OUTGAME→INGAME Scene load 생성 진입점과 `ActiveOutline` 재사용 방어만 최소 보완했다. 보고서 본문은 현재 근거에 맞게 갱신했다.

## 22. Git 및 Untracked 리소스

git add/commit/push는 수행하지 않았다. `Assets/Resources/Ingame/Item/`의 기존 아이콘 PNG/meta는 현재 Untracked이며 수정하지 않았다. 작업 전부터 존재한 다른 Modified/Untracked 파일도 보존했다.

## 23. 2026-07-18 Play Mode 피드백 표적 수정

### 확인된 실제 원인

- Phase 1 터치 이펙트 View가 매 프레임 `Color.white`를 강제해 Hammer 상태를 시각적으로 구분할 입력이 없었다.
- Scraper 추가 피해는 기존 피해·점수 경로만 호출하고 추가 대상 위치의 터치 이펙트를 호출하지 않았다.
- CementBasket은 미도포 전체 목록을 섞어 앞의 N개를 취해 보드 전체에 산발적으로 표시됐다.
- Phase 3는 선택된 `Phase3ImageKey` Sprite를 각 조각 Renderer에도 전달해 고유 단색 대신 이미지 UV 조각이 보였다.
- 안내 Presenter는 Phase 진입 시 즉시 표시하지 않고 유휴 시간부터 계산했다.
- ActiveOutline은 자식 Sibling 최상위 보장 없이 낮은 Alpha 구간까지 펄스했다.

### 수정 결과와 보존 범위

- Hammer만 기존 직접 타격 이펙트에 금빛 Tint를 전달하며 풀 반환 시 흰색을 복원한다.
- Scraper 추가 피해 성공 직후 파괴 전 확보한 타일 중앙에 기존 흰색 이펙트를 재생한다.
- Basket은 임의 Seed 기반 8방향 Region Growing을 사용하고 목표 수량·Batch·점수·완료 계약은 유지한다.
- Phase 3 조각 Sprite는 항상 `null`로 전달해 기존 팔레트 색을 유지한다. `Phase3ImageKey` Sprite는 완료 Image와 Shine에만 사용하며 Request가 없는 직접 실행은 `Img_bigtiles1`을 기본으로 한다.
- 각 Phase의 실제 Playing 진입 직후 기존 문구를 최대 Alpha로 표시하고, 해당 Phase의 첫 유효 게임 영역 입력에서 숨긴다. 아이템 버튼 클릭만으로는 숨기지 않으며 Phase 전환과 Retry에서 초기화한다.
- ActiveOutline은 버튼 직속 최상위 Sibling, 금빛 테두리, Alpha `0.55~1.0`, Raycast 비차단으로 표시한다.
- 버튼 배치·수량·보상·저장·점수·Seed·CSV·Request Snapshot·Shape/Snap 계약·PNG/meta·Scene은 이 표적 수정에서 변경하지 않았다.

### 검증 상태

- 독립 Runtime/Editor Roslyn: 각각 warning 0 / error 0
- 신규 표적 정적 검사: 32/32 PASS
- 이번 표적 수정 파일 `git diff --check` 및 보고서 trailing whitespace 검사: PASS
- 저장소 전체 `git diff --check`: 기존 범위 밖 `Assets/Resources/Fonts/Jua-Regular SDF.asset` trailing whitespace 6건으로 FAIL. 해당 Asset은 이번 작업에서 수정하지 않았다.
- Unity Editor 실제 컴파일: 미검증
- Play Mode 표적 재검증: 미실행. 따라서 시각 결과는 수동 Play Mode 확인이 필요하다.

## 24. 두 번째 Play Mode 피드백 표적 수정

### Basket 즉시 표시 실패 원인과 Visual Commit

`Phase2PaintMaskRenderer.ApplyStampBatch`는 GPU 합성 뒤 `_mask`와 `_scratch` RenderTexture 참조를 교환한다. 그러나 `Phase2MaskPresenter.Bind`는 Prepare 시점의 `_mask`만 `RawImage.texture`에 보관하므로, Basket Batch 직후 Presenter는 이전 버퍼를 계속 표시했다. 다음 Pointer 입력이 다시 Batch를 제출해 버퍼가 교환되면서 앞선 Basket 결과가 뒤늦게 보였다.

`Phase2MaskPresenter.RefreshBoundMask`를 추가하고 모든 성공적인 Visual Batch의 `ConsumeResult` 시작에서 현재 `_orchestrator.MaskTexture`를 `RawImage.texture`에 다시 연결한 뒤 `SetAllDirty()`를 정확히 한 번 호출하도록 했다. 가짜 Pointer, 셀별 UI 갱신, 중복 점수·진행률·완료 호출은 추가하지 않았다. Presenter는 외부 RenderTexture를 Release하지 않는다.

### Compact blob 선택과 좌표 검증

이전 8방향 Region Growing은 이미 도포된 셀을 장벽으로 취급했고 작은 연결 성분 이후 거리 보충이 여러 방향으로 분리될 수 있었다. 이를 폐기하고 전체 미도포 후보를 하나의 무작위 Seed 기준 Euclidean Distance Squared Ring으로 정렬한 뒤, 같은 거리 Ring에서만 Fisher-Yates 난수를 적용하고 가까운 후보부터 `targetCount`까지 선택한다. 따라서 기존 도포 셀은 건너뛰지만 바깥 Ring 탐색을 막지 않는다.

좌표 경로는 `index = y * width + x`, 역변환 `x = index % width`, `y = index / width`, Stamp 중심 `u=(x+0.5)/width`, `v=(y+0.5)/height`이며 `Phase2PaintGrid`와 `Phase2PaintMaskRenderer.AppendStampQuad`도 같은 축과 중심 좌표를 사용한다. 논리 선택과 GPU 위치 사이 x/y 교환 또는 상하 반전 결함은 확인되지 않았다. Basket 1회마다 Seed, 선택 Bounds, 최대 Seed 거리, 요청/변경 수, 시각 Refresh와 완료 여부를 단발 로그로 남긴다.

### ActiveOutline 미표시 원인과 수정

기존 재사용 경로는 이미 존재하는 Outline의 RectTransform·자식 Image 설정을 다시 정상화하지 않았다. 신규 Root도 버튼 바깥으로 8px 확장하고 테두리 절반을 외부에 배치해 부모 Mask 또는 UI 겹침에 취약했으며, 활성화 첫 프레임도 현재 사인 펄스값을 즉시 사용했다.

재사용·신규 Root 모두 버튼 직속, Stretch, Offset 0, Scale 1, 마지막 Sibling으로 고정하고 네 테두리를 버튼 내부 12px에 배치한다. 활성화 첫 프레임 Alpha 1.0, 이후 0.65~1.0 펄스를 적용한다. 활성 슬롯 CanvasGroup Alpha는 1.0이고 `Button.interactable=false`의 Disabled Tint만 Normal Tint와 같게 해 본체·아이콘·수량 밝기를 유지한다. 다른 두 슬롯은 Alpha 0.42이며 모든 슬롯의 Raycast는 실제 interactable 상태일 때만 허용한다. 효과 종료 시 원래 ColorBlock과 Alpha를 복원하고 Outline을 숨긴다.

Outline 활성화 첫 프레임에는 `[ItemOutline][State]` 단발 로그로 activeSelf/activeInHierarchy, Root 크기, Sibling, Outline·부모 Alpha, Image 수·Alpha, Mask 여부와 예상 가시성을 기록한다. 매 프레임 로그는 추가하지 않았다.

### 변경·검증 범위

- 변경 파일: `Phase2MaskPresenter.cs`, `Phase2PhaseAdapter.cs`, `IngameItemSystemController.cs`, 이 보고서
- 독립 Runtime/Editor Roslyn: 각각 warning 0 / error 0
- 신규 표적 정적 검사: 33/33 PASS
- Unity Editor 실제 컴파일: 미검증. Editor.log가 06:58:41이고 수정 소스는 07:05 이후라 이번 변경의 Reload 증거가 없다.
- Play Mode 표적 재검증: 미실행·미검증. 시각 항목은 PASS로 판정하지 않는다.
- 정상 확인된 Phase 3 조각/완성 이미지, 안내 문구, Hammer 색상, Scraper 중앙 이펙트는 수정하지 않았다.

## 25. 네 번째 Play Mode 피드백 최종 구조 전환

### Basket 최종 원인과 연속 Stamp

거리 기반 선택 자체는 한 중심 주변의 후보를 고르고 있었지만, 적용 단계에서 각 셀 중심마다 반지름 `0.49 / GridSize`의 작은 `Phase2PaintStamp`를 만들어 `RequestStampBatch`로 제출했다. 셀 Pitch보다 각 원의 지름이 작아 GPU Mask에서 서로 떨어진 점으로 Rasterize된 것이 최종 원인이었다.

셀 중심 반복 Stamp를 제거했다. 무작위 미도포 Seed 셀 내부에서 중심 UV 하나를 정하고, 현재 미도포 셀들의 실제 정규화 거리 분포에서 목표 8~12%에 가장 가까운 반지름을 선택한다. 최종 호출은 `_orchestrator.RequestStamp(centerU, centerV, radius, true)` 한 번이다. 기존 `Phase2PaintSessionModel.ApplyStamp`가 PaintGrid를 갱신하고 동일 Stamp가 `Phase2PaintMaskRenderer.ApplyStamp`로 Rasterize되므로 논리 Grid·점수·진행률·완료와 GPU Mask가 같은 원형 영역을 공유한다.

Renderer에는 성공적인 GPU 합성과 Reset 시 증가하는 `Revision`을 추가하고 Orchestrator가 읽기 전용으로 노출한다. `[Basket][ContinuousStamp]` 단발 로그에는 중심, 목표 비율·면적, 반지름, 실제 변경 면적·셀, 예상 셀, Revision 전후, Mask Instance ID, Presenter Refresh와 완료 여부를 기록한다. 기존 `RefreshBoundMask`와 `SetAllDirty()` 즉시 표시 경로는 유지했다.

128×128 Grid에서 미도포 밀도를 바꾼 600회 수치 시뮬레이션 결과 600/600 PASS, 실제 추가 비율 8.0017%~11.9949%, 목표 대비 최대 오차 1셀이었다.

### 최상위 Canvas Active Overlay

버튼 자식 Outline은 Canvas·Mask·Button Disabled 상태에 함께 묶이는 구조였고 Rect/Alpha 보완 후에도 실제 Game View에 표시되지 않았다. 기존 자식은 삭제·이동하지 않고 발견 시 비활성화한다.

`ItemActiveOverlayRoot`를 아이템 버튼의 `rootCanvas` 직속으로 정확히 하나 생성한다. Canvas 전체 Stretch, Offset 0, Scale 1, 마지막 Sibling이며 CanvasGroup은 입력과 Raycast를 차단하지 않는다. `LateUpdate`에서 활성 버튼의 `GetWorldCorners`를 Overlay `InverseTransformPoint`로 변환하므로 Screen Space Overlay/Camera와 CanvasScaler·해상도 변화에 대응한다.

Border는 Top, Left, BottomLeft, BottomRight, RightTop, RightBottom의 기본 UI Image 6개로 구성한다. 수량 구조 A의 `Item_ValuePanel0N`과 구조 B의 `Item_Value0N` TMP를 모두 탐색하며, 존재하는 Rect들의 Union에 10px Padding을 적용한다. Bottom과 Right Border는 이 제외 영역 앞뒤로 분할되므로 Overlay가 최상위여도 수량 Panel·숫자 위를 지나지 않는다. 특수 Sprite·Material·Shader·GraphicRaycaster는 추가하지 않았다.

지속형 활성 슬롯은 Button Disabled Color를 Normal Color와 같게 하고 배경·아이콘·수량 Alpha 1.0을 유지한다. 다른 두 슬롯은 배경·아이콘 CanvasRenderer Alpha 0.42, 수량 Panel/TMP Alpha 1.0이다. 효과 종료 시 ColorBlock과 Alpha를 복구하고 Overlay를 숨긴다. `[ItemActiveOverlay][Show]`와 `[Hide]`는 상태 전환마다 한 번만 출력한다.

### 변경과 검증

- 변경 파일: `Phase2PaintMaskRenderer.cs`, `Phase2PaintOrchestrator.cs`, `Phase2PhaseAdapter.cs`, `IngameItemSystemController.cs`, 이 보고서
- 신규 구조 전환 정적 검사: 42/42 PASS
- Basket 수치 시뮬레이션: 600/600 PASS
- 독립 Runtime/Editor Roslyn: 각각 warning 0 / error 0
- Unity Editor 실제 컴파일: 미검증
- Play Mode 최종 시각 검증: 미실행·미검증이며 PASS로 기록하지 않는다.
- Phase 3, 안내 문구, Hammer/Scraper 이펙트, 수량·보상·Seed·CSV·Scene·PNG/meta는 수정하지 않았다.

## 26. 실제 Play Mode 잔여 결함 표적 수정 (활성 표시 / Phase 3 마지막 Snap)

### 실행 증거와 최초 실패 지점

- 실패 실행에서 `[ItemSystem][Applied] activeItem=Hammer`와 수량 차감은 확인됐지만, 같은 `Editor.log` 전체에서 `[ItemActiveOverlay]` 출력은 0건이었다. Hierarchy에는 비활성 `ItemActiveOverlayRoot`만 남아 있어 효과/상태가 아니라 실제 표시 경로가 최초 실패 지점이었다.
- 기존 표시는 버튼 Graphic과 분리된 root Canvas 직속 임시 Root 및 sprite가 없는 6개 `Image` segment에만 의존했다. 이 경로가 표시되지 않으면 활성 상태를 보여 주는 독립적인 시각 보장이 없었다.
- 실패한 Phase 3 실행은 seed `5735309653215780111`, Easy, 7 pieces였고 200점 Snap 로그가 6회만 발생한 뒤 시간 만료 패배가 확정됐다. Deck이 빈 것은 마지막 조각이 `Loose` 상태로 Board 위에 남을 수 있기 때문이며, 7번째 `Placed` 등록과 Completion 로그는 없었다.
- `TryInterchangeableSnap`은 위치·회전·도형 일치 후에도 변환된 `finalShape`를 고정 절대 오차 `0.0001`로 Board Bounds에 다시 대조했다. 생성 정답이 Board 외곽에 닿는 정상 경우에도 world/UI 변환의 부동소수 오차로 이 gate만 실패할 수 있었다.

### 최소 수정

- `IngameItemSystemController`는 각 버튼의 실제 `targetGraphic`에 런타임 `Outline`을 한 개 부착한다. 활성 지속형 아이템(Hammer/Scraper/Trowel)만 `#FFB814` 계열, 7px Outline을 켜며 효과 종료 즉시 끈다.
- Outline은 버튼 배경 Graphic의 mesh effect이므로 자식인 icon, `Item_ValuePanel`, TMP보다 먼저 렌더된다. 수량 UI의 위치·계층·alpha·raycast는 바꾸지 않았다.
- 기존 Canvas Overlay는 호환 진단용으로 유지했지만 활성 표시의 단일 실패 지점이 아니며, `[ItemActiveVisual][RenderedState]`가 적용 직후 실제 Outline 활성 여부를 한 번 기록한다.
- Phase 3의 거리 반경(`BoardWorldSide * 0.20`)과 polygon tolerance(`BoardWorldSide * 0.008`), 회전, 도형 일치 조건은 변경하지 않았다.
- final shape가 target polygon과 일치한 뒤에는 변환된 조각을 극소 bounds epsilon으로 재판정하지 않고 생성기가 확정한 `targetWorld`가 Board 내부인지 검사한다. Snap 위치, canonical geometry, 자동 완료 조건은 그대로다.
- 실패한 Pointer Release는 `[Phase3SnapResult]` 한 줄에 piece/assignment/state/distance/threshold/rotation/placedCount/rejectReason을 남긴다. 성공은 마지막 Piece일 때만 동일 요약을 남긴다.

### 검증 상태

- 임시로 최신 런타임 소스를 Unity 생성 C# 프로젝트에 포함해 Roslyn 빌드: warning 0 / error 0. 임시 `.csproj` 항목은 즉시 제거했으며 프로젝트 파일 최종 변경은 0이다.
- 대상 파일 `git diff --check`: PASS.
- 열려 있는 Unity Editor가 이번 변경을 자동 Import하지 않아 Unity DLL timestamp와 Editor.log가 갱신되지 않았다. 따라서 실제 Game View 재검증은 미실행이며 PASS로 기록하지 않는다.
- 다음 수동 확인은 Hammer/Scraper/Trowel의 금색 Outline 및 수량 가시성, 실패 seed와 일반 seed의 7/7 Placed·Completion·Shine·ExitReady, `[Phase3SnapResult] rejectReason`이다.
- Basket 연속 Stamp 경로와 Phase 1/2 기존 로직, CSV/Seed/Scene/PNG/meta는 이번 표적 수정에서 변경하지 않았다.
