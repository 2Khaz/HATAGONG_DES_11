# OUTGAME LOBBY UI 배치 보고서

- 작성일: 2026-07-15 KST
- Scene: `Assets/Scenes/OUTGAME_LOBBY.unity`
- 기준 해상도: 1440×2560, 세로 화면
- Canvas: Screen Space Overlay
- Canvas Scaler: Scale With Screen Size, Match 0.5
- 작업 범위: OutGame Lobby Scene, Outgame 리소스, Outgame 전용 Setup Script

## 1. 명칭 정리

| 변경 전 | 변경 후 | 비고 |
|---|---|---|
| `OUTGAME_ROBBY.unity` | `OUTGAME_LOBBY.unity` | Scene GUID 유지 |
| `Img_BG_roby.png` | `Img_BG_lobby.png` | Texture GUID 유지 |

## 2. 참고 기준

- 위치 기준: 제공된 완성 Lobby 이미지
- 구성 기준: 제공된 UI 배치 스케치
- 실제 리소스가 없는 UI는 흰색 사각형 Placeholder로 표시
- 배경, NPC, Quest Indicator만 현재 제공된 실제 이미지 사용

## 3. Scene Root 구조

| 순서 | Root | 구성 | 상태 |
|---:|---|---|---|
| 1 | Main Camera | Camera, AudioListener | 기존 기본 Root 보존 |
| 2 | Directional Light | URP Light | 기존 기본 Root 보존 |
| 3 | LobbyCanvas | Canvas, CanvasScaler, GraphicRaycaster | 신규 UI Root |
| 4 | EventSystem | EventSystem, InputSystemUIInputModule | 신규 입력 Root |

`LobbyCanvas` 아래에는 `Outgame_UI_General`을 두고 Background, Content, HUD, Dialog Layer를 분리했다.

## 4. 레이어 구조

| Sibling | Layer | 역할 |
|---:|---|---|
| 0 | BackgroundLayer | 전체 Lobby 배경 |
| 1 | ContentLayer | NPC와 Quest Indicator |
| 2 | HUDLayer | 프로필, 레벨, 상단 패널, 설정, 상점 |
| 3 | DialogLayer | NPC 이름표, 대화창, 진행 표시 |

## 5. 1440×2560 배치표

좌표는 Canvas 좌측 상단을 `(0, 0)`으로 한 기준값이다.

| UI 이름 | X | Y | Width | Height | 표시 내용 | 입력 |
|---|---:|---:|---:|---:|---|---|
| LobbyBackground | 0 | 0 | 1440 | 2560 | `Img_BG_lobby` | 없음 |
| LobbyNPC | 760 | 300 | 560 | 1370 | `Img_NPC` | 없음 |
| QuestIndicator | 575 | 300 | 204 | 210 | `Img_icon_quest` | 없음 |
| ProfileImagePlaceholder | 24 | 30 | 180 | 180 | 흰색 프로필 Placeholder | 없음 |
| RankBadgePlaceholder | 152 | 158 | 62 | 62 | 흰색 등급 Placeholder | 없음 |
| LevelText | 210 | 64 | 140 | 86 | `LV.12` | 없음 |
| PlayerNamePanel | 350 | 52 | 390 | 104 | 흰색 이름 Placeholder | 없음 |
| TopResourcePanelGroup | 760 | 54 | 430 | 92 | 상단 소형 패널 그룹 | 없음 |
| TopResourcePanel_01 | 760 | 54 | 130 | 92 | 흰색 Placeholder 1 | 없음 |
| TopResourcePanel_02 | 910 | 54 | 130 | 92 | 흰색 Placeholder 2 | 없음 |
| TopResourcePanel_03 | 1060 | 54 | 130 | 92 | 흰색 Placeholder 3 | 없음 |
| SettingsButtonPlaceholder | 1220 | 30 | 180 | 180 | 흰색 설정 Placeholder | Button |
| StoreButtonPlaceholder | 1230 | 1490 | 160 | 160 | 흰색 상점 Placeholder | Button |
| DialogueNameplate | 70 | 1580 | 500 | 130 | 흰색 NPC 이름표 Panel | 없음 |
| DialoguePanel | 40 | 1690 | 1360 | 810 | 흰색 대화 Panel | Panel |
| DialogueAdvancePlaceholder | 1290 | 2380 | 70 | 70 | 흰색 진행 Placeholder | 없음 |

## 6. 리소스 적용표

| 리소스 | Import Type | Mipmap | Wrap | Max Size | 적용 대상 |
|---|---|---:|---|---:|---|
| `Img_BG_lobby.png` | Sprite/Single | OFF | Clamp | 4096 | LobbyBackground |
| `Img_NPC.png` | Sprite/Single | OFF | Clamp | 2048 | LobbyNPC |
| `Img_icon_quest.png` | Sprite/Single | OFF | Clamp | 512 | QuestIndicator |

NPC와 Quest 이미지는 Alpha Is Transparency를 사용한다. 배경은 원본 1440×2560 해상도를 유지할 수 있도록 Max Size를 4096으로 설정했다.

## 7. 현재 Placeholder와 교체 대상

| Placeholder | 필요한 후속 리소스 | 교체 원칙 |
|---|---|---|
| ProfileImagePlaceholder | 주인공 프로필 이미지 | 동일 RectTransform 유지 |
| RankBadgePlaceholder | 등급별 이미지 | 등급 데이터에 따라 Sprite 교체 |
| PlayerNamePanel | 이름/경험치 Panel 리소스 | Text와 Panel 분리 유지 |
| TopResourcePanel 3개 | 재화·상태 아이콘/Panel | 각 Panel별 Sprite 교체 |
| SettingsButtonPlaceholder | 설정 아이콘 | Button 영역 유지 |
| StoreButtonPlaceholder | 상점 아이콘 | Button 영역 유지 |
| DialogueNameplate | 이름표 Panel | 내부 NPC Name Text 유지 |
| DialoguePanel | 대화창 Panel | 내부 Dialogue Text 유지 |
| DialogueAdvancePlaceholder | 다음 대화 아이콘 | 우측 하단 Anchor 유지 |

## 8. OutGame 전용 Setup

- Script: `Assets/Scripts/Outgame/Editor/OutgameLobbySceneSetup.cs`
- Menu: `Tools/HATAGONG/Outgame/Build Lobby Layout`
- 재실행 시 `LobbyCanvas`와 `EventSystem`만 다시 생성
- Main Camera와 Directional Light는 보존
- INGAME Scene이나 INGAME Script/Resource는 수정하지 않음

## 9. 확인 결과

- 실제 Lobby 배경 표시 확인
- NPC 우측 중앙 표시 확인
- Quest Indicator NPC 상단 표시 확인
- 상단 HUD와 설정 Placeholder 표시 확인
- 상점 Placeholder 표시 확인
- 이름표와 대화창 표시 확인
- Console Error/Exception 0
- 확인 후 Play Mode 종료
- Scene 저장 후 Dirty `false`

## 10. 아직 수행하지 않은 작업

- 실제 프로필/등급/설정/상점/대화 UI 리소스 적용
- 버튼 동작 연결
- NPC 대화 데이터 연결
- Outgame Runtime Presenter 구현
- Build Settings 등록
- INGAME 전환 연결
