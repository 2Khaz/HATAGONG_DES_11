# Request 아이콘 참조 오류 수정 보고서

## 1. 최초 증상

NORMAL REQUEST 문구는 정상이지만 Request_Icon Image의 Sprite가 null이라 흰색 Simple Image 사각형이 표시됐다.

## 2. 직접 원인

Scene의 Request_Icon.m_Sprite, RequestPresenter.normalIcon, suddenIcon이 모두 null이었다. Image Color는 white/alpha 1, Type은 Simple이므로 null Sprite의 기본 흰 사각형이 그대로 보였다.

## 3. 근본 원인

PrePhase2Setup이 전용 Sprite 슬롯을 연결하지 않았다. 기존 Presenter는 Awake에서 당시 Request_Icon Sprite를 fallback으로 저장했지만 Setup 실행 중 이미 null인 값을 저장했고, Present가 다시 null을 적용했다. 초기 Git 기준 Scene에는 Sudden GUID가 Request_Icon에 있었으나 Setup의 null fallback 흐름에서 손실됐다.

## 4. 기존 Sprite 조사

두 PNG를 직접 확인했다. Normal은 주황색 일반 과녁, Sudden은 붉은색 특수 과녁이다. 둘 다 TextureImporter textureType=Sprite, spriteMode=Single, alphaIsTransparency=1이며 .meta가 존재한다. 새 리소스는 만들지 않았다.

## 5. Normal 아이콘

- 파일: Assets/resource/Img_icon_normal.png
- Sprite: Img_icon_normal
- GUID: ecc57deb5af69204da79d9fce9fe8b9d

## 6. Sudden 아이콘

- 파일: Assets/resource/Img_icon_sudden.png
- Sprite: Img_icon_sudden
- GUID: 3bc53aec751c8d84baf4dd7fca1b9d5e

## 7. RequestPresenter 수정

Normal/Sudden 상태에서 해당 전용 슬롯이 존재할 때만 Image.sprite를 교체한다. null 슬롯이 현재 Sprite를 null로 덮어쓰지 않는다. 유효 Sprite 적용 시 Color를 white/alpha 1로 정규화한다. 검증용 읽기 전용 Sprite 속성을 추가했다.

## 8. Setup 수정

두 기존 Sprite를 AssetDatabase에서 명시적으로 로드해 normalIcon/suddenIcon에 Serialized Reference로 저장한다. 어느 하나라도 없으면 Setup이 명확히 실패한다. Setup을 연속 2회 실행한 뒤 참조 유지와 Presenter 단일 인스턴스를 확인했다.

## 9. Scene 참조

- requestIcon: 기존 Request_Icon Image fileID
- normalIcon: GUID ecc57deb5af69204da79d9fce9fe8b9d
- suddenIcon: GUID 3bc53aec751c8d84baf4dd7fca1b9d5e
- 기본 Request_Icon.m_Sprite: Img_icon_normal
- Scene 저장 완료, isDirty=false

## 10. Play Mode 실제 Sprite

- Normal: NORMAL REQUEST / Img_icon_normal
- Sudden: SUDDEN REQUEST / Img_icon_sudden
- Image Color: RGBA(1,1,1,1)
- Image Type: Simple
- Sprite null: false
- 흰색 사각형: 발생 조건 제거

## 11. 반복 전환

Normal→Sudden→Normal→Sudden→Normal의 실제 Sprite 이름이 모두 상태와 일치했다. Sudden 상태에서 Presenter disable/enable 후에도 SUDDEN REQUEST / Img_icon_sudden이 복원됐다.

## 12. 자동 검증

Request 아이콘 검증 12/12 PASS. 두 슬롯 non-null, 서로 다른 Sprite, 텍스트/아이콘 일치, 5회 반복 전환, 재활성화, 정확한 Sprite 이름을 검증한다.

## 13. 전체 회귀

- Session 모델: 11/11
- Timer: 31/31
- Matrix/Stress: 1320/1320 보드, 9900 tiles
- HP 위반/불일치: 0/0
- Damage State: 30/30
- Easy/Normal/Hard Smoke: 3/3, clear event 각 1회
- Console Error/Warning: 0/0
- Missing Script/Broken Prefab: 0/0
- INGAME build index 0

## 14. 변경 파일

- RequestPresenter.cs: null fallback 제거와 명시적 상태 Sprite 적용
- PrePhase2Setup.cs: 두 Sprite Serialized Reference 연결
- PrePhase2Validation.cs: Request 아이콘 12개 자동 검사
- INGAME.unity: Normal/Sudden 슬롯 및 기본 Normal Sprite 저장
- PRE_PHASE2_FRAMEWORK_IMPLEMENTATION_REPORT.md: 이전 null fallback 기록 정정
- 본 보고서

## 15. 남은 위험

현재 두 아이콘은 의미와 파일명이 일치하며 모두 연결됐다. 향후 Request 명칭이나 아이콘 정책 변경 시 Serialized Slot을 교체해야 한다.

## 16. 최종 판정

**PASS**
