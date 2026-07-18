# INGAME 설정 메뉴 구현 보고서

- 작업일: 2026-07-18
- Branch: `OutGame_Contec`
- 기준 HEAD: `b577eea41d68c996eaeaeb8e501cdffd2eb930cd`
- 판정: `STATIC READY / INGAME SETTINGS MENU PLAY MODE VALIDATION REQUIRED`

## 1. 구현 범위

- 기존 `Canvas/Game_UI_Settings/Settings_Button`에 `IngameSettingsMenuController`를 연결했다.
- 기본 상태에서는 Settings 버튼만 보이며, 열림 애니메이션은 Settings 중심에서 `Exit → BGM → SFX → Vibration` 순으로 위쪽에 전개된다.
- 닫힘 애니메이션은 `Vibration → SFX → BGM → Exit` 역순이다.
- 항목당 이동 시간은 0.22초, 항목 간 지연은 0.06초이며 전체 전개/수납 시간은 약 0.40초다.
- 모든 애니메이션은 `Time.unscaledDeltaTime`을 사용한다.
- Phase 시작 전 Overlay와 같은 검정 50% 알파의 `InputBlocker`가 배경을 어둡게 하고 게임 영역 클릭을 차단한다.
- 화면 중앙에는 READY와 같은 KERISKEDU_B 폰트·120 크기로 `- PAUSE -`를 표시한다. Font Asset의 기본 무외곽선 Material을 사용하며 색상은 단일 검정이다.

## 2. Runtime UI 계층과 좌표

Scene 직렬화 상태의 기존 버튼 위치는 유지했다.

| 항목 | Anchor | Pivot | Size | 닫힘 좌표 | 열림 좌표 |
|---|---|---|---:|---:|---:|
| Settings | Top-Left `(0,1)` | `(0,1)` | `330×284` | `(1066,-2214)` | 동일 |
| Exit | Settings 복제 | Settings 복제 | `330×284` | `(1066,-2214)` | `(1066,-1901.6)` |
| BGM | Settings 복제 | Settings 복제 | `330×284` | `(1066,-2214)` | `(1066,-1589.2)` |
| SFX | Settings 복제 | Settings 복제 | `330×284` | `(1066,-2214)` | `(1066,-1276.8)` |
| Vibration | Settings 복제 | Settings 복제 | `330×284` | `(1066,-2214)` | `(1066,-964.4)` |

Runtime 생성 계층:

```text
Canvas/Game_UI_Settings
├─ SettingsMenuRuntimeRoot
│  ├─ InputBlocker
│  ├─ PauseMessage
│  ├─ ExitButton
│  │  ├─ Background
│  │  ├─ Icon
│  │  └─ OffMark
│  ├─ BgmButton
│  │  ├─ Background
│  │  ├─ Icon
│  │  └─ OffMark
│  ├─ SfxButton
│  │  ├─ Background
│  │  ├─ Icon
│  │  └─ OffMark
│  └─ VibrationButton
│     ├─ Background
│     ├─ Icon
│     └─ OffMark
└─ SettingsButton
   ├─ Background
   └─ Icon
```

- 버튼 배경은 전부 `Img_button_option 1.png`를 사용한다.
- 각 Icon은 `preserveAspect=true`이며 원본 비율을 유지한다.
- `OffMark`는 해당 옵션이 OFF일 때만 활성화되고 Exit에는 표시되지 않는다.
- `PauseMessage`는 설정 Pause가 시작될 때 활성화되고 닫힘 애니메이션과 Session Resume가 모두 완료된 뒤 비활성화된다.
- `Game_UI_Settings`의 기존 Canvas 정렬 계층을 유지했으며 결과/전환 UI의 최상위 순서를 변경하지 않았다.

## 3. 사용 Sprite와 직렬화 참조

| 용도 | 경로 | GUID |
|---|---|---|
| 공통 배경 | `Assets/Resources/Ingame/UI/Button/Img_button_option 1.png` | `209c5b5e46e6d194dac61fdac72ecd10` |
| Settings | `Assets/Resources/Ingame/ICON/Img_icon_option.png` | `f61f3deb50fe7f042b2fd3ecb557a299` |
| Exit | `Assets/Resources/Ingame/ICON/Img_icon_option_out.png` | `0946d093f8e6b2f4896cce576f30393e` |
| BGM | `Assets/Resources/Ingame/ICON/Img_icon_option_bgm.png` | `ea021668919fe324691c7a3e2c24769a` |
| SFX | `Assets/Resources/Ingame/ICON/Img_icon_option_se.png` | `f1e59965c2aedf648867b35b6ae790c7` |
| Vibration | `Assets/Resources/Ingame/ICON/Img_icon_option_zindong.png` | `5d47ba3935a4c21469530f3c4a576bd6` |
| OFF X | `Assets/Resources/Ingame/ICON/Img_icon_opuix.png` | `e582a6b9838e9c740ae228275e249a22` |

7개 참조는 `INGAME.unity`의 Controller에 GUID로 직렬화했으며, PNG와 기존 `.meta` 내용은 수정하지 않았다.

## 4. 일시정지·입력 계약

메뉴 열기 허용 조건:

- Session 상태가 `Playing`
- 결과/Scene Load 요청 없음
- 현재 Phase가 Prepared 및 Running
- 현재 Phase가 Clear 또는 ExitReady가 아님

열기 순서:

1. `GameSessionController.TryPauseForSettings()` 성공 확인
2. Timer `PauseTimer()`
3. 현재 Phase 입력 OFF
4. Phase 3 활성 Drag가 있으면 Snap·점수 없이 직전 안정 Loose 위치 또는 원래 Deck 슬롯으로 복귀
5. 유휴 안내 외부 UI 차단
6. 원래 `Time.timeScale` 기록 후 0 적용
7. 검정 50% Dim Blocker와 중앙 PauseMessage 활성화 및 메뉴 전개

닫기 순서:

1. 메뉴 역순 수납 완료
2. Session 상태와 현재 Phase 유효성 재확인
3. Timer Resume 및 현재 Phase 입력 ON
4. 원래 `Time.timeScale` 복원
5. 유휴 안내 차단 해제 및 Blocker 비활성화

설정 중에는 `CanAcceptGameplayInput`과 `CanAddScore`가 모두 false이며, 패배/Phase 3 최종 Clear commit도 거부한다.

## 5. 옵션 저장과 실제 출력 경로

- BGM, SFX, Vibration은 서로 독립적인 PlayerPrefs key를 사용한다.
- 저장값이 없으면 모두 ON이다.
- 값은 0/1로 저장하고 변경 즉시 `PlayerPrefs.Save()`를 호출한다.
- 비정상 저장값은 안전하게 ON으로 정규화한다.
- Phase 1의 실제 `PlayOneShot`과 `Handheld.Vibrate()` 호출은 각각 SFX/Vibration 옵션을 통과한다.
- 현재 프로젝트에는 명시적인 BGM Manager가 없다. Controller는 INGAME에서 기존 loop AudioSource를 발견해 BGM 옵션의 mute 상태를 적용한다. 실제 BGM AudioSource 존재 여부와 음소거 청감은 Play Mode 최종 확인 대상이다.

## 6. Exit 계약

- Exit는 메뉴가 완전히 열린 상태에서만 받는다.
- 세션의 기존 단일 Scene Load guard를 재사용해 `OUTGAME_LOBBY` 로드를 정확히 한 번만 요청한다.
- 성공/실패/보상/Retry 상태를 commit하지 않는다.
- Load 요청 전에 현재 Scene의 `Time.timeScale`만 정상값으로 복원하고, Timer와 Phase 입력은 계속 잠근다.
- Load 요청이 시작된 뒤 Pending/Active 의뢰 선택 저장소를 정리한다.
- Load 시작이 거부되면 설정 메뉴를 다시 열린 잠금 상태로 유지한다.

## 7. 변경 파일

이번 설정 메뉴 작업에서 변경 또는 생성한 파일:

- `Assets/Scenes/INGAME.unity`
- `Assets/Scripts/GameFlow/Runtime/GameSessionController.cs`
- `Assets/Scripts/GameFlow/Runtime/IngameOptionPreferences.cs` 및 `.meta`
- `Assets/Scripts/GameFlow/UI/IngameSettingsMenuController.cs` 및 `.meta`
- `Assets/Scripts/Phase1/Runtime/Phase1FeedbackController.cs`
- `Assets/Scripts/Phase3Tangram/Phase3TangramManager.cs`
- `Docs/20260718_INGAME_SETTINGS_MENU_REPORT.md`

사용자가 추가한 option Icon PNG/`.meta`는 참조만 했고 수정하지 않았다. 기존 작업 트리의 다른 수정·Untracked 파일도 보존했다.

## 8. 정적 검증

- Runtime Roslyn: exit 0, warning 0, error 0
- Editor Roslyn: exit 0, warning 0, error 0
- `git diff --check`: PASS
- `INGAME.unity` YAML document ID: 298개 / unique 298개
- 7개 Sprite Scene 직렬화 GUID: 모두 실제 `.meta`와 일치
- `OUTGAME_LOBBY.unity`: 수정 없음
- Packages/ProjectSettings/Prefab: 수정 없음
- Git add/commit/push: 수행하지 않음
- Codex가 Unity Editor/Play Mode/Validation 메뉴를 실행한 횟수: 0
- 최종 환경 확인 시 사용자가 이미 열어 둔 Unity 6000.3.19f1 프로세스 2개가 존재했다. Editor.log는 option Icon Import 성공만 읽기 전용으로 확인했으며 Play Mode나 Scene Save는 호출하지 않았다.

Unity 자동 Scene 저장 경로가 프로젝트 Editor 코드에 존재하므로 현재 dirty 작업을 보호하기 위해 열린 Editor를 조작하거나 Play Mode를 실행하지 않았다.

## 9. Play Mode 필수 확인 항목

- Phase 1/2/3 각각 Playing 중 Open/Close
- Timer 숫자 완전 정지 및 닫힘 완료 뒤 재개
- 각 Phase 입력 완전 차단
- Phase 3 Drag 중 Open 시 안정 위치/Deck 복귀 및 점수·Snap 0
- 전개/수납 순서와 약 0.40초 unscaled 애니메이션
- 검정 50% Dim Blocker가 게임 영역 입력만 차단하고 옵션 버튼은 정상 입력
- 중앙 `- PAUSE -`가 KERISKEDU_B·120·검정 단색·외곽선 0으로 표시
- BGM/SFX/Vibration 독립 ON/OFF, X 표시, Scene 재진입 후 유지
- Exit 연타 시 OUTGAME_LOBBY Load 요청 1회
- Ready/Transition/Clear/Defeat/Success 상태에서 Settings Open 거부
- Console Error/Warning 0

최종 상태: `STATIC READY / INGAME SETTINGS MENU PLAY MODE VALIDATION REQUIRED`
