# HATAGONG_DES_11 새 PC 이전 체크리스트

- 기준 Branch: `Sub_Phase3LogicCore`
- 문서 작성 시 커밋 전 기준 HEAD: `6f7d2cc426ddf78d6d66aac18ff241f19c1c8dc9`
- Unity: `6000.3.19f1`
- URP: `17.3.0`

## 1. 이전 PC에서 종료 전

- [ ] 사용자 Play Mode 확인 결과를 문서와 대조
- [ ] 미검증 모바일 항목을 완료로 표시하지 않음
- [ ] 실제 Puzzle Data/Generator/Source가 후속 범위인지 확인
- [ ] `git status --short --untracked-files=all` 기록
- [ ] `git branch --show-current` 기록
- [ ] `git rev-parse HEAD` 기록
- [ ] staged 파일 확인
- [ ] 커밋 대상과 제외 대상을 사용자와 확정
- [ ] 사용자 승인 전 add/commit/push 금지

## 2. 이전해야 할 작업 범위

- [ ] `Assets/Scenes/INGAME.unity`
- [ ] Phase별 Deck Sprite Adapter 변경
- [ ] `Assets/Scripts/Phase3/` 전체 Production/Core 파일과 Meta
- [ ] P3-1/P3-2 기존 Editor 검사 파일과 Meta
- [ ] `Docs/Phase3_Dev_List/` 문서
- [ ] 삭제된 P3-3 대형 Editor 검증 파일이 포함되지 않았는지 확인

## 3. 기준선

- [ ] 기존 P3-1/P3-2 대상 파일 51개
- [ ] Manifest fingerprint 일치:
  `9169091e5b7fe0c7df6ad360346bc467997e1fb977aa97f5300223450571437`
- [ ] P3-3 대형 Editor 검증기 없음
- [ ] 새 대형 Harness 없음
- [ ] Packages/ProjectSettings 비의도 변경 없음

## 4. 새 PC에서 최초 확인

다음은 읽기 전용으로 먼저 확인한다.

```text
git branch --show-current
git rev-parse HEAD
git status --short --untracked-files=all
git log -1 --oneline --decorate
git branch -vv
```

- [ ] Branch가 예상 Branch와 일치
- [ ] HEAD가 이전 PC 기록과 일치
- [ ] Working Tree 변경 목록이 이전 기록과 일치
- [ ] staged 상태가 예상과 일치
- [ ] Unity Hub에 정확한 Unity 버전 설치
- [ ] 프로젝트를 다른 Unity 버전으로 업그레이드하지 않음

차이가 있으면 Unity를 열거나 파일을 수정하기 전에 차이만 보고한다.

## 5. 새 PC 정적 점검

- [ ] Runtime/Editor Roslyn warning 0, error 0
- [ ] `git diff --check` whitespace 오류 0
- [ ] 기존 51개 Manifest 불일치 0
- [ ] Phase 1·2 비의도 게임 로직 변경 0
- [ ] Prefab/Packages/ProjectSettings 비의도 변경 0
- [ ] 삭제된 대형 Editor 검증기 복구 0

## 6. 새 PC 최소 Play Mode 확인

정적 상태가 일치하고 사용자가 실행을 승인한 뒤에만 수행한다.

- [ ] Phase 1 `Img_deck1`
- [ ] Phase 2 `Img_deck2`
- [ ] Phase 3 `Img_deck3`
- [ ] Deck Piece 클릭만 하면 상태 불변
- [ ] Drag Threshold 이후 Drag 시작
- [ ] Deck 위 Drag Piece 표시
- [ ] Press Offset 유지
- [ ] RMB 회전 후 Drag 유지
- [ ] Empty Field → Loose
- [ ] Valid Snap → Placed
- [ ] Deck → 원래 Slot 복귀
- [ ] Space 무반응
- [ ] Console Error/Exception 0

## 7. 새 PC에서 이어갈 순서

1. P3-3 기준선과 문서 확인
2. 사용자 승인
3. P3-4 모바일 입력 실제 확인
4. 사용자 명세 확정 후 P3-5 실제 Puzzle Data/Generator
5. P3-6 실제 `IPhase3SessionSource` 통합
6. P3-7 최종 게임 흐름과 전체 확인

각 단계가 끝날 때 다음 단계와 Git 작업은 다시 사용자 승인을 받는다.
