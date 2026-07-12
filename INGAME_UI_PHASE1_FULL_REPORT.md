# HATAGONG_DES_11 인게임 UI 및 Phase 1 전체 구현·인수인계 보고서

- 문서 버전: 1.0
- 작성일: 2026-07-12
- 대상 프로젝트: `HATAGONG_DES_11`
- Unity: `6000.3.19f1` / Unity 6.3 LTS
- 렌더 파이프라인: Universal Render Pipeline(Universal 3D)
- 대상 씬: `Assets/Scenes/INGAME.unity`
- 현재 상태: **인게임 UI 세팅 및 Phase 1 철거 코어 프로토타입 구현 완료, 전용 리소스·실기기 UX·후속 페이즈 미완료**

---

## 1. 문서 목적

이 문서는 다음 정보를 한 곳에 보존하기 위한 GitHub 인수인계 원문이다.

1. 현재 인게임 UI 계층과 RectTransform 설정
2. Phase 1 철거 시스템의 실제 구현 구조
3. Tile Bag, HP, Grade, 점수, 입력, Hash, 피드백 규칙
4. Unity MCP를 통해 연결·검증·저장된 Inspector 상태
5. 실제 수행한 자동 검증과 Smoke Test 결과
6. 아직 구현하지 않은 항목과 다음 작업 순서
7. Git 커밋 전에 반드시 정리해야 하는 저장소 상태
8. Codex가 제출한 원문 작업 보고서

검토 근거는 다음 업로드 자료다.

- `git_status.txt`
- `git_diff_stat.txt`
- `phase1_scripts_diff.txt`
- `phase1_config_diff.txt`
- `phase1_prefab_diff.txt`
- `ingame_scene_diff.txt`
- 대화 중 제출된 Unity MCP 구현·검증 보고서 3종

---

## 2. 현재 완료 판정

### 2.1 완료된 범위

- 1440×2560 기준 인게임 UI 계층 구성
- 5×5 Phase 1 전용 필드
- 12종 Tile Bag
- 난이도별 Shuffle Bag
- largest-first 랜덤 백트래킹 배치
- 경계·중첩·빈칸·Core 인접·같은 Shape 연결 제한 검증
- 단일 활성 포인터 입력
- HP·Damage State·파괴·클리어
- 타격/파괴/클리어 점수
- Layout Hash와 Variant Hash
- Beige/Brown/Gray/Marble Grade 난수 배정
- 모든 Final HP 2 이상 보장
- Grade별 fallback 색상
- Sprite·Audio 슬롯 구조
- null-safe 사운드와 모바일 조건부 진동
- 난이도 별 표시 고정
- Prefab, Config, Scene, Inspector 참조 저장
- 12 Bag × 10 Seed = 120보드 / 900타일 자동 검증

### 2.2 완료되지 않은 범위

- 실제 90초 카운트다운과 `Time_Value` 갱신
- Phase 2/3 전환
- Request, Deck, Item 버튼 기능
- 설정 화면
- 점수 팝업 애니메이션
- 파티클 및 최종 연출
- 실제 AudioClip
- Grade·비율·파손 단계별 전용 Sprite
- 정밀 햅틱
- 실제 모바일 입력/진동 검증
- 한국어 TMP 폰트 및 최종 문구
- Build Settings의 최종 씬 구성 검증

---

## 3. Diff 및 저장소 상태 분석

### 3.1 변경 규모

공급된 `git diff --stat` 기준:

- 60개 파일 변경
- 6,382줄 추가
- 436줄 삭제
- 신규 `INGAME.unity`: 5,106줄
- 신규 `Phase1_TilePrefab.prefab`: 180줄
- 신규 `Phase1GameConfig.asset`: 349줄
- Phase 1 C# 코드: 24개 파일

### 3.2 커밋 전 필수 정리 사항

#### A. `SampleScene.unity.meta` → `Assets/Scripts/Phase1/Data.meta` rename/GUID 재사용

현재 Git은 다음을 rename으로 인식한다.

```text
R Assets/Scenes/SampleScene.unity.meta -> Assets/Scripts/Phase1/Data.meta
```

`Data.meta`가 삭제된 SampleScene의 GUID를 이어받은 상태로 보인다. Unity GUID는 현재 파일이 하나뿐이면 즉시 중복 오류가 나지는 않지만, Build Settings나 다른 에셋이 예전 SampleScene GUID를 참조하고 있었다면 이제 폴더를 가리키게 될 수 있다.

**커밋 전에 처리할 사항:**

1. `SampleScene` 삭제가 의도인지 확인
2. Unity를 닫고 `Assets/Scripts/Phase1/Data.meta`를 삭제
3. Unity를 다시 열어 Data 폴더의 새 GUID를 생성
4. Git 상태가 `D SampleScene.unity.meta` + `A Data.meta`로 분리되는지 확인
5. Build Settings에서 `INGAME` 씬이 유효하게 등록됐는지 확인

이 항목은 문서 작성은 막지 않지만 **커밋 전 정리 권장도가 가장 높다.**

#### B. Unity `.meta` 및 실제 리소스가 아직 untracked

현재 다음이 `??` 상태다.

- Prefab/Scene/Script/Settings 폴더 및 에셋의 `.meta`
- `Assets/resource/`
- `Assets/TextMesh Pro/`
- `INGAME.unity.meta`
- `Phase1GameConfig.asset.meta`

Unity 프로젝트를 다른 PC에서 정상 복원하려면 실제 에셋과 대응 `.meta`를 함께 커밋해야 한다. 특히 씬의 별 Sprite와 UI Sprite는 `Assets/resource` GUID를 참조하므로, Scene/Script만 선택 커밋하면 Missing Sprite가 발생한다.

#### C. 원시 Diff TXT는 커밋 대상에서 제외

프로젝트 루트에 생성된 다음 파일은 검토용 임시 산출물이다.

- `git_status.txt`
- `git_diff_stat.txt`
- `phase1_scripts_diff.txt`
- `phase1_config_diff.txt`
- `phase1_prefab_diff.txt`
- `ingame_scene_diff.txt`

GitHub 기록용으로 꼭 필요하지 않으면 삭제하거나 `.gitignore`에 넣고, 본 문서 2개만 `Docs/`에 커밋하는 편이 좋다.

#### D. 별도 검토가 필요한 변경

다음 변경의 상세 Diff는 이번 자료에 포함되지 않았다.

- `Assets/Settings/Mobile_RPAsset.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `.vscode/`
- `Assets/Scripts/Test.cs`

Input System, URP 또는 Unity MCP 설치에 필요한 변경일 가능성이 있으나, 커밋 전에 GitHub Desktop에서 의도된 변경인지 확인해야 한다.

### 3.3 코드 Diff에서 확인된 추후 정리 포인트

1. `Phase1TileVisualDefinition` 및 `Phase1GameConfig.visuals`는 현재 Grade별 `Phase1TileGradeVisualSet` 경로에서 사용되지 않는다. 최종 구조 확정 후 제거 또는 용도 통합이 필요하다.
2. `Phase1BoardState.UsedSpriteName`과 `VisualFallbackUsed`는 Grade 배정 시점에 기본값을 저장하고, `Phase1TileView.RefreshSprite()`에서 실제 Sprite를 선택한 뒤 BoardState로 다시 동기화하지 않는다. 현재 모든 Sprite가 null이라 보고 내용이 맞지만, 전용 Sprite 연결 이후 `Print Current Layout`의 Sprite/fallback 로그가 실제 화면과 달라질 수 있다.
3. `Phase1BoardController`의 두 번째 fallback Bag 루프는 성공 후 바깥 `foreach`까지 즉시 종료하지 않아 후속 Bag이 다시 실행될 여지가 있다. 현재 검증은 통과했지만 제어 흐름을 명시적으로 정리하는 것이 좋다.
4. `Generate(bool regenerate)`의 `regenerate` 인자는 현재 사용되지 않는다.
5. `HasThreeInLine`은 실제 직선 정렬 판정보다 연결 체인 판정에 가깝고 `HasThreeConnected`와 기능이 겹친다. 현재 규칙보다 느슨하지는 않지만 명칭과 구현 정리가 필요하다.
6. Controller의 Fixed Seed는 최근 Layout Hash 차단 기능과 함께 사용하면 반복 Regenerate 시 같은 배치가 거부될 수 있다. 생성기 자체의 Fixed Seed 재현은 검증됐으나, 디버그에서 완전 동일 보드를 원하면 `ignoreSessionHistoryForDebug`를 함께 사용해야 한다.
7. 실제 Pointer UX, 모바일 진동, AudioClip 재생, Build Settings는 자동 검증 범위 밖이다.

위 항목들은 현재 프로토타입의 동작 실패를 의미하지 않는다. 전용 리소스와 후속 페이즈를 연결하기 전 정리할 기술 부채다.

---

## 4. 씬 루트 및 UI 계층

```text
Canvas
  Game_UI_General
    Background
    Top_HUD
      Phase_Content
        Phase_panel
        Phase_TitleArea
          Phase_Text
        Phase_Dialog
        Phase_DotArea
          Phase1_Dot
          Phase2_Dot
          Phase3_Dot
      Time_Content
        Time_panel
        Time_Title
        Time_Icon
        Time_Value
      Score_Content
        Score_panel
        Score_Title
        Score_Icon
        Score_Value
      Request_Content
        Request_panel
        Request_Icon
        Request_Text
      Difficulty_Content
        Difficulty_panel
        Difficulty_Text
        Difficulty_StarArea
          Diff01_Star
          Diff02_Star
          Diff03_Star
    Middle_GamePanel
      Game_Panel
      Phase1_FieldRoot
        Phase1_TileContainer
        Phase1_EffectRoot
        Phase1_ScorePopupRoot
    Middle_GameDeck
      Deck_Panel
    Item_Button01
      Item_Icon01
      Item_Value01
    Item_Button02
      Item_Icon02
      Item_Value02
    Item_Button03
      Item_Icon03
      Item_Value03
  Game_UI_Settings
    Settings_Button
EventSystem
Main Camera
Directional Light
```

### 계층 고정 원칙

후속 작업에서 아래 이름과 부모 관계는 계약으로 간주한다.

- `Phase1_FieldRoot`
- `Phase1_TileContainer`
- `Phase1_EffectRoot`
- `Phase1_ScorePopupRoot`
- `Difficulty_StarArea`
- `Diff01_Star`, `Diff02_Star`, `Diff03_Star`
- `Score_Value`
- `Time_Value`
- `Request_Text`
- `Deck_Panel`
- `Item_Button01~03`
- `Settings_Button`

기능 추가를 이유로 기존 RectTransform을 이동하거나 이름을 바꾸지 않는다.

---

## 5. Canvas 및 입력 시스템 설정

| 항목 | 현재 값 |
|---|---|
| Canvas Render Mode | Screen Space - Overlay |
| Pixel Perfect | Off |
| Canvas Scaler | Scale With Screen Size |
| Reference Resolution | 1440×2560 |
| Screen Match Mode | Match Width Or Height |
| Match | 0.5 |
| Reference Pixels Per Unit | 100 |
| GraphicRaycaster | 활성 |
| EventSystem 입력 모듈 | InputSystemUIInputModule |
| UI Actions Asset | 프로젝트 Input System Actions 참조 |

`StandaloneInputModule`이 아닌 New Input System 기반 입력 모듈을 사용한다.

---

## 6. UI RectTransform 전체 기록

아래 값은 공급된 `INGAME.unity` YAML을 기준으로 정리했다. LayoutGroup이 구동하는 자식의 런타임 좌표는 직렬화된 Anchored Position과 다를 수 있다.

| 경로 | Anchor Min | Anchor Max | Anchored Position | Size Delta | Pivot | Local Scale |
|---|---:|---:|---:|---:|---:|---:|
| Canvas | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0, z: 0} |
| Canvas/Game_UI_General | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Background | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button01 | {x: 0, y: 1} | {x: 0, y: 1} | {x: 40, y: -2214} | {x: 330, y: 284} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button01/Item_Icon01 | {x: 0.5, y: 0.5} | {x: 0.5, y: 0.5} | {x: 0, y: 18} | {x: 170, y: 170} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button01/Item_Value01 | {x: 1, y: 0} | {x: 1, y: 0} | {x: -72, y: 72} | {x: 60, y: 60} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button02 | {x: 0, y: 1} | {x: 0, y: 1} | {x: 382, y: -2214} | {x: 330, y: 284} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button02/Item_Icon02 | {x: 0.5, y: 0.5} | {x: 0.5, y: 0.5} | {x: 0, y: 18} | {x: 170, y: 170} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button02/Item_Value02 | {x: 1, y: 0} | {x: 1, y: 0} | {x: -72, y: 72} | {x: 60, y: 60} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button03 | {x: 0, y: 1} | {x: 0, y: 1} | {x: 724, y: -2214} | {x: 330, y: 284} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button03/Item_Icon03 | {x: 0.5, y: 0.5} | {x: 0.5, y: 0.5} | {x: 0, y: 18} | {x: 170, y: 170} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Item_Button03/Item_Value03 | {x: 1, y: 0} | {x: 1, y: 0} | {x: -72, y: 72} | {x: 60, y: 60} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Middle_GameDeck | {x: 0, y: 1} | {x: 0, y: 1} | {x: 39, y: -1926} | {x: 1362, y: 278} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Middle_GameDeck/Deck_Panel | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Middle_GamePanel | {x: 0, y: 1} | {x: 0, y: 1} | {x: 40, y: -660} | {x: 1360, y: 1378} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Middle_GamePanel/Game_Panel | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot | {x: 0.5, y: 0.5} | {x: 0.5, y: 0.5} | {x: -7, y: -1} | {x: 1250, y: 1250} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot/Phase1_EffectRoot | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot/Phase1_ScorePopupRoot | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot/Phase1_TileContainer | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD | {x: 0, y: 1} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 600} | {x: 0.5, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Difficulty_Content | {x: 0, y: 1} | {x: 0, y: 1} | {x: 728, y: -440} | {x: 672, y: 160} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea | {x: 1, y: 0.5} | {x: 1, y: 0.5} | {x: -35, y: 0} | {x: 340, y: 110} | {x: 1, y: 0.5} | {x: 0.9999892, y: 0.9999892, z: 0.9999892} |
| Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff01_Star | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 88, y: 88} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff02_Star | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 88, y: 88} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff03_Star | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 88, y: 88} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_Text | {x: 0, y: 0.5} | {x: 0, y: 0.5} | {x: 150, y: 0.0004272461} | {x: 220, y: 110} | {x: 0.5, y: 0.5} | {x: 0.9999892, y: 0.9999892, z: 0.9999892} |
| Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_panel | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content | {x: 0, y: 1} | {x: 0, y: 1} | {x: 40, y: -72} | {x: 400, y: 346} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_Dialog | {x: 0.5, y: 1} | {x: 0.5, y: 1} | {x: 0, y: -125} | {x: 300, y: 72} | {x: 0.5, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_DotArea | {x: 0.5, y: 1} | {x: 0.5, y: 1} | {x: 0, y: -255} | {x: 180, y: 48} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_DotArea/Phase1_Dot | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 36, y: 36} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_DotArea/Phase2_Dot | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 36, y: 36} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_DotArea/Phase3_Dot | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 36, y: 36} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_TitleArea | {x: 0.5, y: 1} | {x: 0.5, y: 1} | {x: 0, y: -36} | {x: 240, y: 64} | {x: 0.5, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_TitleArea/Phase_Text | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: -4} | {x: -16, y: -8} | {x: 0.5, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_panel | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Request_Content | {x: 0, y: 1} | {x: 0, y: 1} | {x: 40, y: -440} | {x: 672, y: 160} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Request_Content/Request_Icon | {x: 0, y: 0.5} | {x: 0, y: 0.5} | {x: 100, y: 0} | {x: 104, y: 104} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Request_Content/Request_Text | {x: 0, y: 0} | {x: 1, y: 1} | {x: 75, y: 0} | {x: -200, y: -36} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Request_Content/Request_panel | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Score_Content | {x: 0, y: 1} | {x: 0, y: 1} | {x: 1000, y: -72} | {x: 400, y: 346} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Score_Content/Score_Icon | {x: 0, y: 1} | {x: 0, y: 1} | {x: 70, y: -210} | {x: 90, y: 90} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Score_Content/Score_Title | {x: 0.5, y: 1} | {x: 0.5, y: 1} | {x: 0, y: -78} | {x: 220, y: 80} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Score_Content/Score_Value | {x: 0, y: 1} | {x: 0, y: 1} | {x: 130, y: -210} | {x: 250, y: 110} | {x: 0, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Score_Content/Score_panel | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Time_Content | {x: 0, y: 1} | {x: 0, y: 1} | {x: 456, y: -72} | {x: 528, y: 346} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Time_Content/Time_Icon | {x: 0, y: 1} | {x: 0, y: 1} | {x: 145, y: -200} | {x: 108, y: 132} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Time_Content/Time_Title | {x: 0.5, y: 1} | {x: 0.5, y: 1} | {x: 0, y: -78} | {x: 220, y: 80} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Time_Content/Time_Value | {x: 0, y: 1} | {x: 0, y: 1} | {x: 235, y: -200} | {x: 230, y: 130} | {x: 0, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_General/Top_HUD/Time_Content/Time_panel | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_Settings | {x: 0, y: 0} | {x: 1, y: 1} | {x: 0, y: 0} | {x: 0, y: 0} | {x: 0.5, y: 0.5} | {x: 1, y: 1, z: 1} |
| Canvas/Game_UI_Settings/Settings_Button | {x: 0, y: 1} | {x: 0, y: 1} | {x: 1066, y: -2214} | {x: 330, y: 284} | {x: 0, y: 1} | {x: 1, y: 1, z: 1} |

### 6.1 핵심 필드 수치

| 오브젝트 | 설정 |
|---|---|
| `Middle_GamePanel` | Top-Left anchor/pivot, Position (40,-660), Size 1360×1378 |
| `Game_Panel` | 부모 전체 Stretch, Raycast Target false |
| `Phase1_FieldRoot` | Center anchor/pivot, Position (-7,-1), Size 1250×1250 |
| 셀 크기 | `fieldRoot.rect.width / 5 = 250` |
| 실제 패널 여백 | 좌 48, 우 62, 상 65, 하 63로 설계됨 |
| `Difficulty_StarArea` | Position (-35,0), Size 340×110, 우측 중앙 anchor/pivot |
| 난이도 별 | 각 88×88, HorizontalLayoutGroup Spacing 18, Padding 0 |

런타임 검증에서 별의 고정 배치 좌표는 `(64,-55)`, `(170,-55)`, `(276,-55)`로 보고됐다.

---

## 7. Phase 1 Field 구조

`Phase1_FieldRoot`에는 다음 컴포넌트가 연결돼 있다.

- `Phase1BoardController`
- `Phase1InputController`
- `Phase1ScoreController`
- `Phase1HUDPresenter`
- `Phase1FeedbackController`
- `AudioSource`

Inspector 참조:

- Field Root → `Phase1_FieldRoot`
- Tile Container → `Phase1_TileContainer`
- Effect Root → `Phase1_EffectRoot`
- Score Popup Root → `Phase1_ScorePopupRoot`
- Tile Prefab → `Phase1_TilePrefab`
- Config → `Phase1GameConfig.asset`
- Score UI → `Score_Value`
- Difficulty Text → `Difficulty_Text`
- Difficulty Stars → `Diff01_Star`, `Diff02_Star`, `Diff03_Star`
- Filled Star → `Assets/resource/Img_icon_star2.png`
- Empty Star → `Assets/resource/Img_icon_star1.png`

기본 Controller 상태:

- `generateOnStart = true`
- 기본 난이도 `Normal`
- `useFixedSeed = false`
- `fixedSeed = 12345`
- `ignoreSessionHistoryForDebug = false`
- `runSmokeTestOnStart = false`

---

## 8. Phase1_TilePrefab 구조

```text
Phase1_TilePrefab
├─ RectTransform
├─ CanvasRenderer
├─ Image: 완전 투명, Raycast Target true
├─ Phase1TileView
└─ Tile_Visual
   ├─ RectTransform: 기본 Stretch
   ├─ CanvasRenderer
   └─ Image: Raycast Target false
```

금지 컴포넌트:

- Button 없음
- GridLayoutGroup 없음
- LayoutElement 없음
- Collider 없음

루트 Image가 입력 Hit Area를 담당하고, 자식 `Tile_Visual`만 회전·Sprite 변경·색상 fallback·펀치 애니메이션을 담당한다. 세로 직사각형에서도 루트 입력 영역은 축 정렬을 유지하고 `Tile_Visual`만 90도 회전한다.

---

## 9. 코드 파일 구성

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Phase1/Data/Phase1DamageState.cs` | 파손 상태 enum(Normal, Damage1, Damage2, Damage3, Destroyed) |
| `Assets/Scripts/Phase1/Data/Phase1Difficulty.cs` | 난이도 enum(Easy, Normal, Hard) |
| `Assets/Scripts/Phase1/Data/Phase1GameConfig.cs` | 보드, 점수, HP, 피드백, Grade, 검증 설정의 중심 ScriptableObject |
| `Assets/Scripts/Phase1/Data/Phase1TileBagDefinition.cs` | Tile Bag의 규격별 개수와 기대 타일 수·면적·HP 정의 |
| `Assets/Scripts/Phase1/Data/Phase1TileGrade.cs` | Grade enum(Beige, Brown, Gray, Marble) |
| `Assets/Scripts/Phase1/Data/Phase1TileGradeDefinition.cs` | Grade별 Modifier, 난이도 가중치, 최대 등장 수, 활성 상태 |
| `Assets/Scripts/Phase1/Data/Phase1TileGradeVisualSet.cs` | Grade·비율·파손 단계별 Sprite, fallback 색상, Audio override |
| `Assets/Scripts/Phase1/Data/Phase1TileRole.cs` | 타일 역할 enum(Pop, Standard, Anchor, Core) |
| `Assets/Scripts/Phase1/Data/Phase1TileShape.cs` | 타일 Shape enum(1×1, 1×2, 1×3, 2×2, 2×3, 3×3) |
| `Assets/Scripts/Phase1/Data/Phase1TileVisualDefinition.cs` | 구형 Shape별 Sprite 정의. 현재 Grade Visual Set 경로에서는 사실상 미사용 |
| `Assets/Scripts/Phase1/Editor/Phase1PrototypeSetup.cs` | Prefab/Config/Inspector 자동 연결과 120보드 검증용 Editor 도구 |
| `Assets/Scripts/Phase1/Generation/Phase1BoardGenerator.cs` | Tile Bag 기반 배치, 회전, largest-first 랜덤 백트래킹, Grade 연결 |
| `Assets/Scripts/Phase1/Generation/Phase1GradeAssigner.cs` | 최종 HP 최소 2 필터, 난이도 가중치, 최대 수량, 몰림 완화, Modifier 범위 선택 |
| `Assets/Scripts/Phase1/Generation/Phase1LayoutHash.cs` | 기하 배치 전용 SHA-256 Layout Hash |
| `Assets/Scripts/Phase1/Generation/Phase1PlacementValidator.cs` | 경계·중첩·빈칸·Core 인접·같은 Shape 연결 제한 검증 |
| `Assets/Scripts/Phase1/Generation/Phase1ShuffleBag.cs` | 난이도별 A/B/C/D Shuffle Bag 상태 관리 |
| `Assets/Scripts/Phase1/Generation/Phase1VariantHash.cs` | Grade·Modifier·Final HP·Visual Set 기반 SHA-256 Variant Hash |
| `Assets/Scripts/Phase1/Runtime/Phase1BoardController.cs` | 생성, Prefab 배치, 타격, 점수, 클리어, Hash history, 디버그/Smoke Test |
| `Assets/Scripts/Phase1/Runtime/Phase1BoardState.cs` | 보드 및 타일 런타임 상태 구조 |
| `Assets/Scripts/Phase1/Runtime/Phase1FeedbackController.cs` | 사운드 우선순위, Grade override, null-safe 재생, 플랫폼 진동 |
| `Assets/Scripts/Phase1/Runtime/Phase1InputController.cs` | 단일 활성 포인터와 추가 멀티터치 차단 |
| `Assets/Scripts/Phase1/Runtime/Phase1ScoreController.cs` | 점수 상태와 Score_Value 갱신 |
| `Assets/Scripts/Phase1/Runtime/Phase1TileView.cs` | 타일 표시·입력·HP·파손 상태·회전·Sprite/fallback·타격 펀치 |
| `Assets/Scripts/Phase1/UI/Phase1HUDPresenter.cs` | 난이도 문자열 및 고정 위치 별 3개 채움/빈 Sprite 갱신 |

네임스페이스는 런타임 기준 `HATAGONG.Phase1`, Editor 도구는 `HATAGONG.Phase1Editor`다.

---

## 10. Phase1GameConfig 핵심 설정

| 분류 | 값 |
|---|---|
| Board Size | 5 |
| Generate On Start | true |
| Tile Debounce | 0.04초 |
| Current Bag Attempts | 20 |
| Alternative Bag Attempts | 20 |
| Hit Score | 10 |
| Destroy Score | 50 |
| Clear Score | 300 |
| Overall Game Duration | 90초, 기록값만 존재 |
| Recent Hash Capacity | Easy 30 / Normal 50 / Hard 50 |
| Punch Scale | 0.97 |
| Punch Down / Return | 0.04초 / 0.06초 |
| Sound Enabled | true |
| Vibration Enabled | true |
| Normal Hit Vibration | false |
| Damage State Change Vibration | true |
| Destroy Vibration | true |
| Minimum Final Tile HP | 2 |
| Grade Assignment Attempts | 20 |
| Default Fallback Grade | Brown |
| Grade Modifier Total Range | Easy -2~+1 / Normal -1~+2 / Hard 0~+4 |

90초 값은 아직 `Time_Value` 카운트다운에 연결되지 않는다.

---

## 11. Shape, 방향, Role

| Shape Family | 허용 방향 | 면적 | Role |
|---|---|---:|---|
| 1×1 | 1×1 | 1 | Core |
| 1×2 | 2×1 / 1×2 | 2 | Pop |
| 1×3 | 3×1 / 1×3 | 3 | Standard |
| 2×2 | 2×2 | 4 | Standard |
| 2×3 | 3×2 / 2×3 | 6 | Anchor |
| 3×3 | 3×3 | 9 | Anchor |

한 변이 4 이상인 타일과 비정형 타일은 사용하지 않는다.

---

## 12. 난이도·규격별 Base HP

| Shape | Easy | Normal | Hard | Role |
|---|---:|---:|---:|---|
| 1×1 | 금지(0) | 6 | 7 | Core |
| 1×2 / 2×1 | 2 | 2 | 2 | Pop |
| 1×3 / 3×1 | 4 | 5 | 6 | Standard |
| 2×2 | 7 | 8 | 9 | Standard |
| 2×3 / 3×2 | 10 | 10 | 10 | Anchor |
| 3×3 | 13 | 14 | 금지(0) | Anchor |

- Easy에는 1×1 Core가 없다.
- Hard에는 3×3 Anchor가 없다.
- 1×2 계열의 Base HP는 전 난이도 2다.
- Grade 적용 후에도 Final HP는 반드시 2 이상이어야 한다.

---

## 13. Tile Bag 12종

| 난이도 | Bag | 1×1 | 1×2 | 1×3 | 2×2 | 2×3 | 3×3 | 타일 수 | 면적 | Base HP |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Easy | EASY_A | 0 | 0 | 2 | 1 | 1 | 1 | 5 | 25 | 38 |
| Easy | EASY_B | 0 | 1 | 0 | 2 | 1 | 1 | 5 | 25 | 39 |
| Easy | EASY_C | 0 | 1 | 1 | 2 | 2 | 0 | 6 | 25 | 40 |
| Easy | EASY_D | 0 | 2 | 0 | 3 | 0 | 1 | 6 | 25 | 38 |
| Normal | NORMAL_A | 2 | 1 | 1 | 3 | 1 | 0 | 8 | 25 | 53 |
| Normal | NORMAL_B | 2 | 0 | 1 | 2 | 2 | 0 | 7 | 25 | 53 |
| Normal | NORMAL_C | 2 | 1 | 0 | 3 | 0 | 1 | 7 | 25 | 52 |
| Normal | NORMAL_D | 2 | 2 | 1 | 1 | 2 | 0 | 8 | 25 | 49 |
| Hard | HARD_A | 3 | 3 | 2 | 1 | 1 | 0 | 10 | 25 | 58 |
| Hard | HARD_B | 4 | 1 | 1 | 1 | 2 | 0 | 9 | 25 | 65 |
| Hard | HARD_C | 3 | 2 | 4 | 0 | 1 | 0 | 10 | 25 | 59 |
| Hard | HARD_D | 3 | 2 | 0 | 3 | 1 | 0 | 9 | 25 | 62 |

모든 Bag은 25셀을 정확히 채운다.

---

## 14. 배치 생성 알고리즘

1. 난이도에 해당하는 Shuffle Bag의 남은 A/B/C/D 후보 순서를 난수화한다.
2. Shape 사양을 면적 내림차순으로 정렬하고 동률은 Seed 기반 난수화한다.
3. 각 Shape의 가로/세로 방향 후보를 생성한다.
4. Largest-first 랜덤 백트래킹으로 5×5 필드에 배치한다.
5. 다음 조건을 만족하지 않으면 후보를 제외한다.
   - 필드 바깥 금지
   - 중첩 금지
   - 빈칸 금지
   - 1×1 Core끼리 변 인접 금지
   - 같은 Shape 타일 3개 이상 연결 금지
6. 후보 정렬은 같은 Shape의 변 접촉, 대각·근접, 동일 방향 군집을 줄이고 Anchor 간 거리를 늘리는 쪽을 우선한다.
7. 우선 배치 실패 시 덜 제한적인 랜덤 순서로 alternative attempts를 수행한다.
8. 성공 시 Layout Hash를 생성하고 최근 Hash 중복 여부를 확인한다.
9. 위치가 확정된 뒤 Grade를 배정하고 Variant Hash를 생성한다.

Tile GameObject의 실제 크기는 하드코딩하지 않고 다음을 사용한다.

```csharp
cell = fieldRoot.rect.width / config.BoardSize;
```

현재 값은 `1250 / 5 = 250`이다.

---

## 15. Layout Hash 및 Variant Hash

### Layout Hash

포함 항목:

- Difficulty
- Bag ID
- Shape
- Grid X/Y
- Grid Width/Height
- 회전 여부

포함하지 않는 항목:

- Grade
- Sprite
- Modifier
- Final HP

SHA-256 소문자 64자 문자열을 사용한다.

### Variant Hash

포함 항목:

- Layout Hash
- Tile ID
- Grade ID
- Grade Modifier
- Final HP
- Visual Set ID
- Minimum HP Valid

같은 Difficulty, Bag, Seed, Config에서는 Layout과 Variant가 재현돼야 한다.

최근 Layout Hash 기록은 런타임 메모리에만 저장하며 영구 저장하지 않는다.

---

## 16. Grade 시스템

| Grade | Modifier | Easy Weight | Normal Weight | Hard Weight | Easy Max | Normal Max | Hard Max | Fallback RGBA |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Beige | -1 | 40 | 20 | 10 | 무제한 | 무제한 | 무제한 | (0.86, 0.76, 0.59, 1) |
| Brown | 0 | 35 | 40 | 30 | 무제한 | 무제한 | 무제한 | (0.48, 0.29, 0.16, 1) |
| Gray | +1 | 20 | 30 | 40 | 무제한 | 무제한 | 무제한 | (0.48, 0.51, 0.54, 1) |
| Marble | +2 | 5 | 10 | 20 | 1 | 1 | 2 | (0.88, 0.90, 0.92, 1) |

### 16.1 배정 순서

```text
Base HP 확정
→ 활성 Grade 및 최대 수량 필터
→ Base HP + Modifier >= 2 필터
→ 몰림 완화 후보 우선
→ 난이도 가중치 난수 선택
→ Modifier 총합 범위에 가장 가까운 결과 선택
→ Final HP와 Visual Set 확정
```

잘못된 Grade를 선택한 뒤 `Math.Max(2, ...)`로 숨기는 Clamp는 사용하지 않는다.

### 16.2 강제 우선순위

1. Final HP 2 이상
2. 비활성 Grade 선택 금지
3. Grade별 최대 등장 수
4. Grade와 HP Modifier 일치
5. 난이도별 Modifier 총합 범위
6. 동일 Grade 몰림 완화

### 16.3 몰림 완화

- 같은 Grade가 전체 타일의 절반을 초과하지 않도록 우선
- 같은 Grade의 직접 인접을 줄임
- Marble끼리 직접 인접하지 않도록 우선
- 인접한 같은 Shape에는 다른 Grade를 우선

이는 후보가 부족하면 완화되는 선호 조건이며, Final HP와 최대 등장 수 조건은 유지된다.

---

## 17. Grade Sprite 및 임시 색상 정책

현재 모든 Grade Sprite 슬롯은 null이다.

Grade별 준비된 비율군:

- Square: 1×1 / 2×2 / 3×3 공용 Sprite 확대·축소
- Rect 2:1: 1×2 / 2×1
- Rect 3:1: 1×3 / 3×1
- Rect 3:2: 2×3 / 3×2

각 비율군마다 다음 상태 슬롯이 있다.

- Normal
- Damage1
- Damage2
- Damage3

현재 Sprite가 없으므로 Grade fallback Color를 사용한다.

- Beige: `(0.86, 0.76, 0.59, 1)`
- Brown: `(0.48, 0.29, 0.16, 1)`
- Gray: `(0.48, 0.51, 0.54, 1)`
- Marble: `(0.88, 0.90, 0.92, 1)`

동작:

- Sprite 없음 → Grade fallback Color
- Sprite 있음 → `Color.white`, 원본 Sprite 색상 유지
- Damage Sprite 없음 + Normal Sprite 있음 → Normal Sprite fallback
- 세로 타일 → 가로 비율 Sprite를 `Tile_Visual`에서 90도 회전

### 최종 아트 교체 정책

프로토타입에서는 정사각형 Sprite 1장을 1×1, 2×2, 3×3에 확대 사용한다. 최종 품질 단계에서는 필요 시 크기별 전용 Square Sprite로 교체한다. 직사각형은 2:1, 3:1, 3:2 비율별 리소스가 필요하다.

---

## 18. 입력 규칙

- Single Active Attack Pointer
- 한 포인터가 활성인 동안 다른 pointerId 입력 무시
- 같은 포인터의 반복 탭 허용
- Tile별 debounce 0.04초
- 파괴된 타일 입력 금지
- 파괴 시 루트 Raycast Target 비활성화 및 Visual 숨김
- 앱 포커스 상실/컴포넌트 비활성화 시 active pointer 초기화

현재 자동 테스트는 디버그 직접 타격을 사용했으며 실제 마우스·터치 UX는 수동 검증 대상이다.

---

## 19. Damage State

### Max HP = 2

```text
2: Normal
1: Damage2
0: Destroyed
```

### Max HP = 3~5

- 75% 초과: Normal
- 50% 초과~75% 이하: Damage1
- 1~50%: Damage3
- 0: Destroyed

### Max HP = 6 이상

- 75% 초과: Normal
- 50% 초과~75% 이하: Damage1
- 25% 초과~50% 이하: Damage2
- 1~25%: Damage3
- 0: Destroyed

Damage State는 Final Max HP 기준으로 계산한다.

---

## 20. 점수 및 클리어

| 사건 | 점수 |
|---|---:|
| 유효 타격 | +10 |
| 타일 파괴 | +50 |
| Phase 1 전체 클리어 | +300 |

마지막 타일의 마지막 타격은 타격 + 파괴 + 클리어를 모두 합산한다.

```text
10 + 50 + 300 = 360
```

`remaining == 0`이 되면 `Phase1Cleared(Phase1BoardState)` 이벤트를 발생시킨다. 현재 Phase 2 리스너는 연결되지 않았다.

---

## 21. 사운드·진동·타격 반동

### 사운드 우선순위

유효 타격 1회에 다음 중 하나만 선택한다.

1. Destroy 전용 또는 공용 사운드
2. Damage State Change 전용 또는 공용 사운드
3. Grade Hit override
4. Core 전용 Hit 또는 일반 Hit

AudioClip이 null이면 조용히 넘어가며 예외를 발생시키지 않는다.

현재 연결된 Clip:

- Normal Hit: 없음
- Damage State Change: 없음
- Destroy: 없음
- Core Hit: 없음
- Grade override: 모두 없음

### 진동

- Editor에서는 실행하지 않음
- Android/iOS 빌드에서만 `Handheld.Vibrate()`
- Normal Hit: 기본 false
- Damage State Change: true
- Destroy: true

### 시각 반동

- Scale 1 → 0.97: 0.04초
- Scale 0.97 → 1: 0.06초
- `Time.unscaledDeltaTime` 사용

---

## 22. 난이도 HUD 별 표시

| Difficulty | Diff01 | Diff02 | Diff03 |
|---|---|---|---|
| Easy | Filled/노란색 | Empty/흰색 | Empty/흰색 |
| Normal | Filled/노란색 | Filled/노란색 | Empty/흰색 |
| Hard | Filled/노란색 | Filled/노란색 | Filled/노란색 |

구현 원칙:

- 세 GameObject 모두 항상 Active
- 세 Image 모두 항상 Enabled
- Alpha 1 유지
- SetActive(false)로 빈 별을 숨기지 않음
- 위치·크기·간격·Sibling 순서 고정
- `Img_icon_star2` = Filled
- `Img_icon_star1` = Empty

`Difficulty_StarArea`의 HorizontalLayoutGroup:

- Spacing 18
- Padding 0
- Child Control/Force Expand false
- Runtime 검증 위치: `(64,-55)`, `(170,-55)`, `(276,-55)`

---

## 23. 자동 검증 결과

### 23.1 초기 코어 검증

- 12 Bag × 3회 = 36/36 성공
- Normal / NORMAL_C / Seed 119453781
- 총 타격 52
- 예상 점수 1170
- 실제 점수 1170
- Layout Hash `7f3c26799317126770bcc58cd2e35fb57d97f9d551cf20191aa23212efd4a556`

### 23.2 Grade 확장 검증

- 12 Bag × 10 Seed = 120보드
- 총 900타일
- Final HP 2 미만 위반: 0
- `Final HP = Base HP + Modifier` 불일치: 0
- Easy/Normal/Hard Fixed Seed Layout Hash 재현 성공
- Easy/Normal/Hard Fixed Seed Variant Hash 재현 성공

샘플 Modifier 총합:

- Easy: -1, 허용 범위 [-2,+1]
- Normal: +2, 허용 범위 [-1,+2]
- Hard: +4, 허용 범위 [0,+4]

Normal 샘플:

- Base Total HP 53
- Final Total HP 55
- 55회 타격
- 예상/실제 점수 1200

### 23.3 Fallback Color 검증

- Beige/Brown/Gray/Marble 실제 `Tile_Visual.Image.color` 적용 확인
- Sprite가 없을 때만 fallback Color 사용 확인
- 컴파일 오류 0
- Console 오류/경고 0
- Play Mode 종료
- Scene 저장
- `isDirty = false`

---

## 24. 수동 검증이 필요한 항목

1. 실제 마우스 연타와 모바일 터치 반응
2. 멀티터치 중 두 번째 손가락 무시 동작
3. 세로 타일의 표시 방향과 입력 영역 일치
4. 타격 debounce 0.04초의 체감
5. Grade fallback 색상 구분성
6. 실제 AudioClip 연결 후 우선순위와 볼륨
7. Android/iOS 실제 진동
8. 다양한 해상도와 Safe Area
9. `INGAME` Build Settings 등록
10. 최종 Sprite 연결 후 BoardState 디버그 로그 동기화

---

## 25. 아직 구현하지 않은 기능과 연계 계약

### 25.1 실제 타이머

- Config의 `overallGameDurationSeconds = 90`을 사용
- `Time_Value`와 연결
- Phase 전체 시간인지 각 Phase 시간인지 확정 필요
- 현재 코어의 `Time record only` 설정을 실제 Timer Controller로 분리 권장

### 25.2 Phase 2/3

- `Phase1Cleared` 이벤트를 전환 Controller가 구독
- `Phase1_TileContainer`를 정리
- Phase Dot 상태 변경
- Deck/Request 갱신
- Phase 2 Root를 별도 생성
- Phase 1 코드에 Phase 2 로직을 직접 혼합하지 않음

### 25.3 Request

- `Request_Text`, `Request_Icon`을 별도 Presenter로 연결
- 현재 Phase 1 보드 생성 규칙과 독립적인 목표 데이터로 관리

### 25.4 Deck

- `Deck_Panel`은 현재 표시용 예약 영역
- Phase 전환과 카드/도구 선택 로직을 별도 Controller로 구성

### 25.5 Item Buttons

- `Item_Button01~03`의 UI 위치 유지
- 아이템 활성/개수/쿨타임/사용 결과를 별도 시스템으로 구현
- Phase1TileView에 아이템 종류별 분기를 누적하지 않음

### 25.6 설정 화면

- 현재 `Settings_Button`만 있음
- BGM/SFX/진동/언어/접근성 옵션을 Config 및 저장 데이터와 연계

### 25.7 점수 팝업·파티클

- `Phase1_ScorePopupRoot` 사용
- `Phase1_EffectRoot` 사용
- 타일 루트 또는 필드 RectTransform 변경 금지

---

## 26. 앞으로의 권장 작업 순서

### 0단계 — Git 커밋 전 정리

1. Data.meta GUID 재생성
2. SampleScene 삭제 의도와 Build Settings 확인
3. 모든 Unity `.meta` 포함 확인
4. `Assets/resource`, `Assets/TextMesh Pro`, Package 변경 포함 여부 확인
5. 원시 Diff TXT 제외
6. 본 문서 2개를 `Docs/`에 배치
7. Unity 실행·컴파일·Play Mode 간단 검증
8. GitHub Desktop에서 커밋

### 1단계 — 실제 입력 UX 검증

- PC 마우스
- Android 터치
- 멀티터치
- 빠른 반복 탭
- 세로 타일
- 파괴 직후 입력 차단

### 2단계 — 최종 Tile Sprite 연동

- Grade별 Square/2:1/3:1/3:2
- Normal/Damage1/Damage2/Damage3
- 실제 Sprite 연결 후 `Color.white`
- BoardState Sprite 로그 동기화 수정

### 3단계 — Audio 및 진동

- 공용 Hit/State/Destroy/Core Clip
- Grade별 override 선택적 연결
- 실기기 볼륨과 진동 체감 확인

### 4단계 — 90초 Timer

- Timer Controller
- `Time_Value`
- Pause/Settings와 연계
- 시간 종료 이벤트 정의

### 5단계 — Phase 전환 기반

- Phase Flow Controller
- Dot/Title/Dialog 갱신
- Phase1Cleared 구독
- Phase 2 진입/복귀 정책

### 6단계 — Request, Deck, Item

기능별 독립 Controller와 Data를 만든 뒤 UI에 연결한다.

### 7단계 — 설정 및 저장

- 오디오/진동
- 언어
- 게임 진행/최고 점수
- 접근성

### 8단계 — 연출 및 최종 QA

- 점수 팝업
- 파티클
- 화면 전환
- 해상도/Safe Area
- 모바일 빌드
- 성능·GC·메모리 점검

---

## 27. 후속 개발 시 변경 금지 계약

- 기존 UI 계층 이름 임의 변경 금지
- `Phase1_FieldRoot` RectTransform 변경 금지
- `Game_Panel` Raycast Target 활성화 금지
- Tile 루트 회전 금지
- Tile_Visual만 회전
- 5×5 논리 크기 하드코딩 금지, Field Root 폭 기준 계산 유지
- Tile Bag의 25셀 완전 충전 유지
- Final HP 2 미만 타일 생성 금지
- Grade 선택 후 HP Clamp로 규칙 위반 은폐 금지
- Layout Hash에 Grade 정보 추가 금지
- 실제 Sprite 없이 임의 외부 리소스 다운로드 금지
- 사운드 슬롯 null에서 예외 발생 금지
- 별 GameObject 비활성화 방식 금지
- Timer/Phase 2/Item/Settings를 Phase 1 코드에 임시로 혼합 금지

---

## 28. Git 커밋 체크리스트

- [ ] `Assets/Scripts/Phase1/Data.meta` 새 GUID 생성
- [ ] `SampleScene` 삭제 여부 확정
- [ ] Build Settings에서 `INGAME` 확인
- [ ] Scene, Prefab, ScriptableObject와 모든 `.meta` 선택
- [ ] `Assets/resource` 선택
- [ ] `Assets/TextMesh Pro` 선택 여부 확인
- [ ] `Packages/manifest.json`, `packages-lock.json` 변경 검토
- [ ] `Mobile_RPAsset.asset` 변경 검토
- [ ] `.vscode` 포함/제외 결정
- [ ] `Test.cs` 포함/삭제 결정
- [ ] 원시 Diff TXT 제외
- [ ] `Docs/INGAME_UI_PHASE1_FULL_REPORT.md` 추가
- [ ] `Docs/INGAME_UI_PHASE1_SUMMARY.md` 추가
- [ ] Unity 컴파일 오류 0
- [ ] Console 게임 코드 오류 0
- [ ] Play Mode 종료
- [ ] Scene `isDirty = false`

권장 커밋 메시지:

```text
Implement in-game UI and Phase 1 demolition prototype
```

---

# 부록 A. 초기 Phase 1 구현 완료 보고서 원문

```text
구현·연결·검증·저장을 완료했습니다.
Unity MCP 연결 상태
HATAGONG_DES_11@ece79e8b1c43895c, Unity 6000.3.19f1, 활성 씬 INGAME.

생성한 코드 파일
요청된 18개 스크립트와 검증/설정용 Phase1PrototypeSetup.cs을 생성했습니다. 추가 파일은 Unity MCP에서 Prefab·Config·Inspector 연결 및 12 Bag 검증을 수행하기 위해 필요했습니다.

수정한 파일
기존 코드 수정 없음. Test.cs도 변경하지 않았습니다.
Unity MCP로 INGAME.unity을 수정·저장했습니다.

생성한 Prefab
Phase1_TilePrefab.prefab
루트: RectTransform, CanvasRenderer, 투명 입력 Image, Phase1TileView
Tile_Visual: 전체 Stretch, Image Raycast Target false
Button/GridLayoutGroup/LayoutElement/Collider 없음

생성한 ScriptableObject
Phase1GameConfig.asset

Phase1_FieldRoot 추가 컴포넌트
Phase1BoardController
Phase1InputController
Phase1ScoreController
Phase1HUDPresenter
Phase1FeedbackController
AudioSource

Inspector 연결
Field/Tile/Effect/ScorePopup Root, Prefab, Config, 각 Controller, Score_Value, Difficulty_Text, 별 3개가 모두 연결됐습니다. 금지된 UI 참조는 연결하지 않았습니다.

AudioSource 설정
Play On Awake false, Loop false, Spatial Blend 0, Volume 1.

연결된 AudioClip
없음. 프로젝트 소유 AudioClip이 Unity MCP 검색에서 발견되지 않았습니다.

누락 AudioClip 슬롯
Normal Hit, Damage State Change, Destroy, Core Hit 모두 비어 있습니다. Null-safe 재생 구조는 검증됐습니다.

Sprite 매핑
Img_tiles* 에셋이 실제 프로젝트에 존재하지 않아 모든 규격군의 Damage Sprite 슬롯을 비워 두었습니다.

Sprite fallback
임의 Sprite를 사용하지 않았습니다. Prefab은 Sprite가 없는 색상 기반 프로토타입 Image로 표시됩니다.

Tile Bag 검증
12개 Bag 모두 명세의 타일 수·면적·HP와 일치했습니다.
Easy: 5/25/38, 5/25/39, 6/25/40, 6/25/38
Normal: 8/25/53, 7/25/53, 7/25/52, 8/25/49
Hard: 10/25/58, 9/25/65, 10/25/59, 9/25/62

생성 매트릭스 테스트
Easy/Normal/Hard 전체 12 Bag × 3회 = 36/36 성공했습니다.

Smoke Test
난이도 Normal, Bag NORMAL_C, Seed 119453781.

Smoke Test 타일 목록
0: (2,1), 1×1, Core, HP 6
1: (0,0), 1×1, Core, HP 6
2: (1,0), 2×1, Pop, HP 2
3: (0,1), 2×2, Standard, HP 8
4: (0,3), 2×2, Standard, HP 8
5: (3,0), 2×2, Standard, HP 8
6: (2,2), 3×3, Anchor, HP 14

Layout Hash
7f3c26799317126770bcc58cd2e35fb57d97f9d551cf20191aa23212efd4a556

점수 테스트
52회 타격 기준 예상/실제 점수 모두 1170입니다.

Damage State 테스트
Normal → Damage1 → Damage2 → Damage3 → Destroyed 전환과 파괴 후 입력 차단을 확인했습니다.

사운드 테스트
모든 슬롯이 null인 상태에서 예외 없이 클리어까지 진행됐습니다.

진동 코드
Editor에서는 호출하지 않고 Android/iOS 빌드에서만 Handheld.Vibrate를 호출하도록 컴파일 조건을 적용했습니다.

Console 오류
최종 컴파일 및 일반 Play Mode에서 구현 코드 오류 0.
컴포넌트 직렬화 조회 중 MCP 패키지 자체 AudioSource 직렬화 오류가 일시적으로 발생했으나 게임 코드에서 발생한 오류는 아닙니다.

Console 경고
구현 경고 0. MCP WebSocket 미초기화 경고 1건이 남았습니다.

Play Mode 테스트 방식
MCP의 실제 포인터 시뮬레이션은 사용하지 않았습니다. 디버그 타격으로 HP, 점수, 상태, 파괴, 클리어를 검증했습니다. 실제 터치/클릭 UX는 수동 확인이 필요합니다.

기존 UI 변경 여부
기존 계층·이름·Sprite·RectTransform 변경 없음. Game_Panel Raycast Target도 false로 유지됐습니다.

저장 상태
Play Mode 종료, 씬 저장 완료, 최종 isDirty: false.

후속 작업
실제 타이머, 2페이즈 전환, Request/Deck/아이템/설정 기능, 파티클, 점수 팝업, 정밀 햅틱, 실제 AudioClip 및 전용 타일 Sprite 연결은 구현하지 않았습니다.
```

---

# 부록 B. Grade 시스템 구현 완료 보고서 원문

```text
구현·검증·저장을 완료했습니다.
Unity MCP 연결 상태
HATAGONG_DES_11@ece79e8b1c43895c, Unity 6000.3.19f1, 활성 씬 INGAME.

생성한 파일
Assets/Scripts/Phase1/Data/Phase1TileGrade.cs — 생성 — Beige/Brown/Gray/Marble enum.
Assets/Scripts/Phase1/Data/Phase1TileGradeDefinition.cs — 생성 — Modifier·가중치·최대 등장 수 정의.
Assets/Scripts/Phase1/Data/Phase1TileGradeVisualSet.cs — 생성 — Grade별 Sprite 및 Audio override 슬롯.
Assets/Scripts/Phase1/Generation/Phase1GradeAssigner.cs — 생성 — 최소 HP 필터와 가중치 기반 Grade 배정.
Assets/Scripts/Phase1/Generation/Phase1VariantHash.cs — 생성 — Grade·Final HP 기반 SHA-256 Variant Hash.

수정한 파일
Phase1GameConfig.cs — 수정 — Grade 설정, 최소 HP, Modifier 범위, 자동 검증 추가.
Phase1BoardState.cs — 수정 — Grade/Base HP/Modifier/Variant 데이터 추가.
Phase1BoardGenerator.cs — 수정 — 위치 확정 후 Grade 배정 연결.
Phase1BoardController.cs — 수정 — Variant Hash, 확장 로그, Final HP 기반 실행.
Phase1TileView.cs — 수정 — Grade 런타임 정보, Grade Sprite, 세로 Visual 회전.
Phase1FeedbackController.cs — 수정 — Grade Audio override와 공용 사운드 fallback.
Phase1HUDPresenter.cs — 수정 — 별 비활성화 제거, 채움/빈 Sprite 교체.
Phase1PrototypeSetup.cs — 수정 — Config 마이그레이션, 별 Sprite 연결, 120보드 검증.
Phase1GameConfig.asset — 수정 — Grade 기본값 4종 및 Visual Set 4종 저장.
INGAME.unity — 수정 — HUD Presenter의 채움/빈 별 Sprite 참조 저장.

데이터 구조
규격과 Grade를 분리했으며 BaseHp → Grade 필터 → Grade 선택 → Modifier 적용 → FinalHp 순서를 사용합니다.

Grade Modifier
Beige -1
Brown 0
Gray +1
Marble +2

Minimum Final Tile HP
2

후보 필터
enabled && BaseHp + HpModifier >= 2 && 최대 등장 수 미초과
잘못 선택한 후 Clamp하는 처리는 없습니다.

난이도별 가중치
Easy: 40 / 35 / 20 / 5
Normal: 20 / 40 / 30 / 10
Hard: 10 / 30 / 40 / 20
순서: Beige/Brown/Gray/Marble

최대 등장 수
Beige/Brown/Gray는 0=무제한. Marble은 Easy 1, Normal 1, Hard 2.

연결한 Grade Sprite
없음. 프로젝트에 확인 가능한 Img_tiles* 또는 Grade 전용 Sprite가 없습니다.

누락 Sprite
네 Grade의 square, 2:1, 3:1, 3:2 Normal/Damage1/2/3 슬롯 전체가 비어 있습니다.

fallback
기존 색상 기반 Tile_Visual 표시를 유지합니다. 모든 테스트 타일에서 visualFallbackUsed=true였으며 임의 Sprite는 사용하지 않았습니다.

테스트 Difficulty/Bag/Seed
Easy: EASY_B, 31001
Normal: NORMAL_B, 31001
Hard: HARD_B, 31001
자동 검증: 12 Bag × 10 Seed = 120보드

Normal 샘플 타일
0: Brown, Base 6, Mod 0, Final 6
1: Beige, Base 6, Mod -1, Final 5
2: Gray, Base 5, Mod +1, Final 6
3: Gray, Base 8, Mod +1, Final 9
4: Marble, Base 8, Mod +2, Final 10
5: Brown, Base 10, Mod 0, Final 10
6: Beige, Base 10, Mod -1, Final 9

검사한 전체 타일 수
900개 — 120보드 전체.

Final HP 2 미만 위반
0

Final HP 계산 불일치
0

샘플 Modifier 총합
Easy -1
Normal +2
Hard +4

Normal Base Total HP
53

Normal Final Total HP
55

목표 범위/실제 결과
Easy [-2,+1] / -1
Normal [-1,+2] / +2
Hard [0,+4] / +4

Layout Hash
Easy: 337ae920...58680454f
Normal: 124719cd...94378edb64
Hard: 4f64f982...454073b8c1

Variant Hash
Easy: b75913e7...a0233e51
Normal: d7637c0e...02e4fbf8
Hard: 8c3e0cf4...64585e2cd

Fixed Seed 재현
Easy/Normal/Hard 모두 Layout Hash와 Variant Hash가 동일하게 재현됐습니다.

Easy 별 표시
노란색, 흰색, 흰색.

Normal 별 표시
노란색, 노란색, 흰색.

Hard 별 표시
노란색, 노란색, 노란색.

별 상태
세 별 모두 Active Self true, Image Enabled true, Color (1,1,1,1).
채움: Img_icon_star2
빈 별: Img_icon_star1

별/StarArea RectTransform
변경 없음. 별 위치 (64,-55), (170,-55), (276,-55), 크기 88×88 유지.

HorizontalLayoutGroup
변경 없음. Spacing 18, Padding 0, 정렬 및 Control/Expand 설정 유지.

컴파일 오류
최종 0.

Console 경고
구현 경고 0. MCP WebSocket 미초기화 경고 1건이 남았습니다.

회귀 검증
5×5 배치, 12 Bag, Hash, Final HP, 점수, Damage State, null-safe 사운드, 진동 조건, 반동, Phase1Cleared 유지. Normal Smoke Test는 55회 타격·예상/실제 점수 1200으로 일치했습니다.

기존 UI 계층/RectTransform
변경 없음. Phase1_FieldRoot도 기존 1250×1250, 위치 (-7,-1) 유지.

저장 상태
Play Mode 종료, Config·씬 저장 완료, isDirty=false.

수동 확인 필요
실제 포인터 터치/클릭 UX
Grade별 전용 Sprite가 추가된 후 시각적 Grade 일치
실제 모바일 기기의 진동
실제 AudioClip 연결 후 청각 확인

Marble 추가 연결 슬롯
Square/2:1/3:1/3:2의 Normal·Damage1·Damage2·Damage3 및 선택적 Hit/Damage/Destroy Audio override.

최종 리소스 교체 슬롯
Beige/Brown/Gray/Marble 각각의 Square, Rect2x1, Rect3x1, Rect3x2 Normal·Damage1·Damage2·Damage3 슬롯입니다.
```

---

# 부록 C. Grade fallback 색상 적용 보고서 원문

```text
Sprite가 없을 때 적용되는 Grade별 색상:
Beige: (0.86, 0.76, 0.59, 1)
Brown: (0.48, 0.29, 0.16, 1)
Gray: (0.48, 0.51, 0.54, 1)
Marble: (0.88, 0.90, 0.92, 1)

동작 방식:
Sprite 없음 → Grade별 fallback 색상 적용
Sprite 연결됨 → Color.white로 원본 Sprite 색상 유지
색상은 Phase1GameConfig의 Grade Visual Set에서 수정 가능
Unity MCP Play Mode에서 Beige/Brown/Gray/Marble 네 등급의 실제 Tile_Visual.Image.color 적용을 확인했습니다.

최종 상태:
컴파일 오류: 0
Console 오류/경고: 0
Play Mode 종료
씬 저장 완료
isDirty: false
기존 UI 및 RectTransform 변경 없음
```

---

# 부록 D. 현재 핵심 산출물

```text
Assets/Scenes/INGAME.unity
Assets/Prefabs/Phase1/Phase1_TilePrefab.prefab
Assets/Settings/Phase1/Phase1GameConfig.asset
Assets/Scripts/Phase1/
Assets/resource/
Assets/TextMesh Pro/
Docs/INGAME_UI_PHASE1_FULL_REPORT.md
Docs/INGAME_UI_PHASE1_SUMMARY.md
```
