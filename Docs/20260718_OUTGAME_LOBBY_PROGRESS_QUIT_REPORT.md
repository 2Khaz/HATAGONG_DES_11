# OUTGAME 로비 진행도·종료 UI 작업 보고서

## 이번 단계 핵심 요약

- 최종 판정: `STATIC READY / OUTGAME LOBBY PROGRESS AND QUIT PLAY MODE VALIDATION REQUIRED`
- Branch: `OutGame_Contec`
- HEAD: `b577eea41d68c996eaeaeb8e501cdffd2eb930cd`
- Unity 실행: 새로 실행하지 않음. 작업 전부터 Unity 프로세스 2개가 존재했으나 제어·Scene 저장하지 않음.
- Play Mode 실행: 하지 않음. `InitializeOnLoadMethod`, `EditorApplication.delayCall`, `EditorSceneManager.SaveScene` 자동 훅이 있어 열린 dirty Scene 덮어쓰기 위험을 확인함.
- 기존 Staged: 0개, 작업 후에도 0개
- 신규 파일: 진행도 Data/Repository/Rank 계산, 로비 진행도·종료 Controller 및 각 `.meta`, 본 보고서
- 수정 파일: `Assets/Scenes/OUTGAME_LOBBY.unity`, `Assets/Scripts/GameFlow/Runtime/GameSessionController.cs`
- 삭제 파일: 0개
- 예상 밖 변경: 0개
- Commit/Push: 수행하지 않음

작업 전 Scene SHA256:

- OUTGAME: `4DEC792FC35C24E070732D6C86FA8448E292E366DB3036E811039B1B8B6A4B3C`
- INGAME: `CDF6F48613D77AECCAB24FC7C0D1DDF2D10EA17A23F8FAEA5449AE04A8C2B586`

작업 후 Scene SHA256:

- OUTGAME: `13EAA5D4A6BA6C166BAB8DD7E35A2B78E4E3A441BCE9E751860DD661DE1AE1D8`
- INGAME: `CDF6F48613D77AECCAB24FC7C0D1DDF2D10EA17A23F8FAEA5449AE04A8C2B586` (이번 작업 중 불변)

## 리소스

| 용도 | Asset 이름 | 실제 경로 | GUID | 원본 크기 | Aspect Ratio | Git 추적 | Scene 참조 |
|---|---|---|---|---:|---:|---|---|
| 진행도 배경 | Img_topui | `Assets/Resources/Outgame/Img_topui.png` | `6ef06d65e929e964e9c93d777c44736c` | 1774×887 | 2.000000 | Untracked | Controller 직렬화 참조 1개 |
| 종료 아이콘 | Img_icon_logout | `Assets/Resources/Outgame/Img_icon_logout.png` | `f43071297924b4c43bb60097a0b21cf8` | 171×172 | 0.994186 | Untracked | Controller 직렬화 참조 1개 |
| 종료 확인창 | Img_logoutyesno | `Assets/Resources/Outgame/Img_logoutyesno.png` | `4bfa8570685a39f4788f02d52d8c7e87` | 1410×645 | 2.186047 | Untracked | Controller 직렬화 참조 1개 |

- 세 PNG 모두 실제 PNG이며 Git LFS 포인터가 아니다.
- 동일 이름 Asset은 각각 1개이다.
- 세 PNG와 기존 `.meta`는 수정·재인코딩·재생성하지 않았다.
- 세 Asset은 작업 전부터 Untracked였으며 이번 작업에서도 Stage하지 않았다. 사용자 Commit 시 PNG와 `.meta`를 반드시 쌍으로 검토해야 한다.
- Import는 Single Sprite(`spriteMode=1`, `textureType=8`), Full Rect, PPU 100, Alpha Transparency, Bilinear 상태이다.

## 학교안심 폰트

- 원본 Font: `Assets/Resources/Fonts/Hakgyoansim_JayusiganR.ttf`
- 원본 Font GUID: `691ab052673dbb642b668b67b9f98013`
- TMP Font Asset: `Assets/Resources/Fonts/Hakgyoansim_JayusiganR SDF.asset`
- TMP Font GUID: `2e47d870c7ea16a409eb9416dc2d1dee`
- Family: `학교안심 자유시간 R` / `Hakgyoansim Jayusigan R`
- Source Font File: 위 TTF GUID와 일치
- Material: Font Asset 내부 Material 참조 정상
- Atlas: Dynamic(`m_AtlasPopulationMode=1`), Source Font 연결 정상, Atlas index 3까지 존재
- Source TTF 글리프 검사: `사원/대리/과장/팀장/이사/사장/종료하시겠습니까?/예/아니오/MAX/0123456789` 및 `/` 누락 0
- 일부 한글은 현재 직렬화된 Dynamic atlas cache에 아직 없지만 Source TTF에서 모두 확인되며 동적 추가 가능한 구조이다. 실제 렌더링은 Play Mode 검증 대상으로 남긴다.
- RankText, StageProgressText, MessageText, YesText, NoText는 모두 동일한 `lobbyFont` Factory를 통해 위 Font Asset을 사용한다.
- Font Asset, Atlas, 공용 Material 수정: 0개

## Safe Area·반응형 레이아웃

- Canvas 경로: `LobbyCanvas/Outgame_UI_General`
- Canvas: Screen Space Overlay
- Canvas Scaler: Scale With Screen Size, Reference Resolution 1440×2560, Match 0.5 (불변)
- 기존 SafeArea Root: 없음
- 신규 런타임 Root: `LobbyCanvas/LobbySafeAreaRoot`; Scene에 중복 직렬화하지 않고 Controller가 Scene 로드마다 정확히 1회 생성
- `QuitInputBlocker`는 Canvas 전체 Stretch이며 기존 로비 콘텐츠보다 위, SafeAreaRoot보다 아래에 둔다.
- Safe Area Root anchor는 `Screen.safeArea / Screen.width,height`로 계산한다.
- 해상도 또는 Safe Area가 실제로 변경될 때만 재계산한다. 매 프레임 레이아웃 값을 재설정하지 않는다.
- LobbyProgressRoot Anchor/Pivot: `(0,1)/(0,1)`
- TopUiBackground Anchor/Pivot: `(0,1)/(0,1)`, `Image.Type=Simple`, Preserve Aspect, Raycast off
- Top UI: `width=safeWidth×0.54`, `height=width/2`; 높이가 `safeHeight×0.18`을 넘으면 높이 기준 재계산
- 진행도 위치: `(safeWidth×0.03, -safeHeight×0.025)`
- LobbyQuitRoot Anchor/Pivot: `(1,0)/(1,0)`
- Logout: `min(safeWidth×0.11, safeHeight×0.08)`, 위치 `(-safeWidth×0.035, safeHeight×0.025)`
- QuitConfirmRoot Anchor/Pivot: `(0.5,0.5)/(0.5,0.5)`
- Popup: `safeWidth×0.76`로 시작하고 `safeHeight×0.40`을 넘으면 높이 기준 Fit
- 모든 Rect는 회전 0, Scale 1이며 실제 디바이스 픽셀 고정 좌표가 없다.

## 진행도 저장

- 저장 경로: `Application.persistentDataPath/hatagong-player-progress-v1.json`
- 형식/Version: UTF-8 JSON / Version 1
- 데이터: `Version`, `ClearedStageCount`만 저장
- 기본값: 0
- 음수: Load 시 0으로 정규화
- 손상/빈 JSON/지원하지 않는 Version: 예외를 전파하지 않고 0으로 복구하며 Warning 기록
- 저장: `.tmp` 작성 후 `File.Replace`; 미지원/IO fallback은 overwrite copy, 마지막에 임시 파일 정리
- 저장 실패: Error 기록 후 무한 재시도하지 않음
- PlayerPrefs와 중복 저장하지 않음
- 로비 Scene의 Controller Awake/Enable에서 Repository 최신값을 다시 Load

## 등급 규칙

단일 순수 계산 `PlayerRankProgress.Evaluate`가 등급과 표시 문자열을 동시에 결정한다.

| 누적 | 등급 | 표시 |
|---:|---|---|
| 0–99 | 사원 | N/100 |
| 100–249 | 대리 | N/250 |
| 250–399 | 과장 | N/400 |
| 400–599 | 팀장 | N/600 |
| 600–849 | 이사 | N/850 |
| 850+ | 사장 | N/MAX |

경계 12건(0, 99, 100, 249, 250, 399, 400, 599, 600, 849, 850, 1000)을 실제 순수 C# 실행으로 검사했고 실패 0건이다.

## 성공 연결

- Terminal Success Commit: `GameSessionController.TryCommitTerminalPhaseClear`
- Phase3의 모든 Piece 배치 확인 후 위 Commit이 성공해야만 기존 completion image/shine/score/ExitReady가 진행된다.
- 기록 순서: Terminal gate 최초 Commit → `_stageClearRecorded` 확인/설정 → `RecordStageClear` 즉시 저장 → 기존 Phase3 completion image/shine → ExitReady → 성공 정산 UI
- 중복 콜백: gate의 기존 `IsCommitted` 상태와 세션 `_stageClearRecorded` 이중 Guard로 추가 저장 0
- Guard 초기화: 새 `GameSessionController.Start`에서만 수행
- Phase1/2 Clear, 전환, 실패, Retry, 설정 Exit, 결과 UI 클릭 경로에는 Repository 호출이 없다.
- 저장 실패 시 성공 게임 흐름은 유지하지만 Error를 기록하며 같은 세션에서 반복 기록하지 않는다.

## 로비 UI

런타임 계층:

```text
LobbyCanvas
├─ Outgame_UI_General (기존)
├─ QuitInputBlocker
└─ LobbySafeAreaRoot
   ├─ LobbyProgressRoot
   │  └─ TopUiBackground
   │     ├─ RankText
   │     └─ StageProgressText
   ├─ LobbyQuitRoot
   │  └─ QuitIcon
   └─ QuitConfirmRoot
      └─ ConfirmPanel
         ├─ MessageText
         ├─ YesButton/YesText
         └─ NoButton/NoText
```

- RankText anchor: `(0.25,0.55)–(0.60,0.92)`
- StageProgressText anchor: `(0.31,0.07)–(0.94,0.52)`
- 두 TMP는 Center, Auto Size, No Wrap, Raycast off이다.
- 표시 예: `사원 / 0/100`, `대리 / 100/250`, `사장 / 850/MAX`

## 종료 UI

- Quit 기본 Alpha 0.45; Pointer Enter/Down 1.0; Exit/Up은 popup 닫힘 0.45, 열림 1.0
- QuitButton만 입력을 받고 QuitIcon은 Preserve Aspect/Raycast off이다.
- ConfirmPanel은 Preserve Aspect/Raycast off이다.
- MessageText: `(0.10,0.58)–(0.90,0.88)`, `종료하시겠습니까?`
- YesButton: `(0.07,0.09)–(0.48,0.45)`, `예`
- NoButton: `(0.52,0.09)–(0.93,0.45)`, `아니오`
- Yes/No 영역은 0.04 normalized gap으로 분리되며 겹침 0이다.
- popup open과 Yes/No 처리에 별도 Guard가 있어 연타·멀티터치의 첫 유효 입력만 처리한다.
- blocker 바깥 클릭은 아무 동작도 하지 않는다.
- No: popup/blocker만 닫고 Scene/의뢰/carousel 상태를 유지하며 Alpha 0.45 복구
- Yes: 입력을 먼저 잠그고 Editor는 `EditorApplication.isPlaying=false`, Build는 `Application.Quit()` 1회 요청

## 화면비 수식 검증

아래 값은 Safe Area가 전체 화면인 경우의 최종 렌더 픽셀 환산값이다. 노치/홈 인디케이터가 있는 실제 기기는 같은 수식을 축소된 Safe Area에 적용한다.

| 해상도 | Top UI 크기/위치 | Logout 크기/위치 | Popup 크기 | 정적 Fit |
|---|---|---|---|---|
| 1080×1920 | 583.2×291.6 / (32.4,-48.0) | 118.8 / (-37.8,48.0) | 820.8×375.5 | 범위 내 |
| 720×1280 | 388.8×194.4 / (21.6,-32.0) | 79.2 / (-25.2,32.0) | 547.2×250.3 | 범위 내 |
| 1080×2340 | 583.2×291.6 / (32.4,-58.5) | 118.8 / (-37.8,58.5) | 820.8×375.5 | 범위 내 |
| 1440×3120 | 777.6×388.8 / (43.2,-78.0) | 158.4 / (-50.4,78.0) | 1094.4×500.6 | 범위 내 |
| 1536×2048 | 737.3×368.6 / (46.1,-51.2) | 163.8 / (-53.8,51.2) | 1167.4×534.0 | 범위 내 |
| 1600×2560 | 864.0×432.0 / (48.0,-64.0) | 176.0 / (-56.0,64.0) | 1216.0×556.3 | 범위 내 |

Sprite 비율, 계산상 화면 경계, anchor 겹침은 정적으로 통과했다. 실제 노치, TMP 렌더 잘림, 터치 hit, 가로 화면은 Play Mode/기기 검증 전이므로 PASS로 기록하지 않는다.

## 검증

- 신규 4개 Runtime source Roslyn: exit 0, warning 0, error 0
- 수정 GameSessionController 표적 Roslyn: exit 0, 신규 warning/error 0 (기존 serialized-field 경고와 self-reference check 경고만 검사 명령에서 명시 제외)
- 순수 Rank 경계 실행: 12/12, failures 0
- Scene YAML document ID: 77/77 unique
- 신규 Script/Sprite/Font GUID: Scene 참조 각 1, `.meta` 선언 각 1
- Duplicate GUID: 검사 대상 0
- Missing `.meta`: 검사 대상 0
- `git diff --check`: PASS
- Missing Script/Reference, Console, 실제 저장 Reload, 실제 Quit, 실제 Safe Area/TMP 렌더: Unity 미실행으로 미검증
- Editor full compile: Unity 자동 저장 위험 때문에 미실행
- PNG/학교안심 Font/Atlas/Material/ProjectSettings/Canvas Scaler 수정: 0
- Phase1/2/3 핵심 규칙, 설정 메뉴, Request 후보, Phase3 이미지 테이블 수정: 0
- INGAME Scene 이번 작업 중 변경: 0
- Git add/commit/push: 0

## 최종 결론

- Terminal Success 최초 Commit 직후에만 누적 수를 1 증가시키는 코드 계약을 구성했다.
- 실패·Retry·설정 Exit·Phase1/2 Clear 경로에는 증가 호출이 없다.
- Versioned JSON, 손상/음수 복구, temp 교체, 즉시 저장 경로를 구성했다.
- 등급 경계와 `MAX` 표시는 순수 실행 검증을 통과했다.
- 신규 TMP는 모두 `Hakgyoansim_JayusiganR SDF`를 공유하며 Source TTF 요구 글리프 누락은 0이다.
- Img_topui/Logout/Popup은 Safe Area 비율, anchor/pivot, 원본 aspect 기반으로 계산된다.
- Quit popup 입력 차단, Yes/No 분리, 중복 Quit Guard를 구성했다.
- 자동 Scene 저장 훅 때문에 Unity/Play Mode를 강행하지 않았다. 실제 Scene 렌더, Dynamic atlas 글리프 생성, 영구 저장 round-trip, 기기 Safe Area·멀티터치, Build Quit은 반드시 후속 검증해야 한다.

`STATIC READY / OUTGAME LOBBY PROGRESS AND QUIT PLAY MODE VALIDATION REQUIRED`
