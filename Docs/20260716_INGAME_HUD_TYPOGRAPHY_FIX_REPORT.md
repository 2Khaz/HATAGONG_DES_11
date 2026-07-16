# 인게임 HUD 표기·폰트 복구 보고서

## 적용 내용

- `Score_Title`: `Score` → `점 수`
- `Time_Value`의 숫자 표기(`99`부터): `Jua-Regular SDF`
- 그 외 인게임 상단 HUD 텍스트: `Hakgyoansim_JayusiganR SDF`
- `Time_Title`, `Score_Title` Y 위치: 모두 `-82`
- `Request_Text`, `Difficulty_Text`: 72pt, 자동 크기 조절 비활성화
- `Score_Title`: 80pt
- `Item_Button01`~`03`: 배경 Sprite를 `Img_button_item`으로 통일
- `Settings_Button`: 배경 Sprite를 `Img_button_option 1`로 변경
- `Settings_Button`: 플레이 중 Sprite가 덮어써져도 매 프레임 지정 Sprite로 복구하는 `FixedImageSprite` 보호 적용
- 상단 페이즈 제목: `PHASE 1`~`PHASE 3` 영문 유지
- 페이즈 구슬: 색상 틴트가 아니라 `Img_icon_phaseOn`/`Img_icon_phaseOff` Sprite를 직접 교체하여 `●○○ / ○●○ / ○○●` 보장

## 페이즈 구슬 오표기 원인

씬 초기 상태에서 1번 구슬만 `Img_icon_phaseOn`, 2·3번 구슬은 `Img_icon_phaseOff`를 참조하고 있었다. 기존 `PhaseHUDPresenter`는 페이즈 변경 시 Sprite는 그대로 둔 채 색상만 변경했기 때문에, 2·3페이즈에서도 켜짐 모양이 올바른 위치로 이동하지 않았다. 현재는 페이즈가 바뀔 때 세 구슬 모두의 On/Off Sprite를 다시 지정한다.

## Play Mode 변경 유실 방지

Play Mode에서 Inspector로 변경한 씬 값은 Play Mode 종료 시 Unity가 진입 전 상태로 복원하므로 저장되지 않는다. `IngameHudPersistenceFix`를 추가해 스크립트 리로드 및 Play Mode 종료 후 Edit Mode에서 확정 HUD 값을 다시 적용하고 `INGAME.unity`를 명시적으로 저장하도록 했다. 동일 작업은 `Tools/HATAGONG/Game Flow/Apply and Save Ingame HUD Fix` 메뉴로도 실행할 수 있다.

## 기존 수정값이 돌아간 이유

Git 이력과 현재 씬 직렬화 값을 확인한 결과, `Score_Title`의 `Score`, Y 위치 `-78`, 60pt 및 기존 폰트 값은 `69cd9c9`(2026-07-12) 커밋부터 현재 기준 씬까지 그대로였다. 즉, 사용자가 Inspector에서 변경한 값이 Git 커밋이나 현재 작업 트리의 `INGAME.unity`에 저장된 흔적은 없었다.

Unity의 Inspector 수정은 씬이 dirty 상태일 때 씬 파일을 저장해야 유지된다. 저장되지 않은 상태에서 씬 재열기, 스크립트 리로드, 브랜치/커밋 전환 등이 발생하면 디스크에 있던 이전 직렬화 값으로 복원된다.

또한 `Phase1PrototypeSetup`과 `PrePhase2Setup`은 HUD 오브젝트를 다시 연결하고 씬을 저장하는 Editor 설정 메뉴지만, HUD 폰트·크기·타이틀 위치를 원하는 값으로 강제하거나 검증하지 않는다. 따라서 저장되지 않은 수동 Inspector 변경을 복구해 주지 못한다. 이번 수정은 `Assets/Scenes/INGAME.unity`에 직접 직렬화해 씬 파일 기준값으로 남겼다.
