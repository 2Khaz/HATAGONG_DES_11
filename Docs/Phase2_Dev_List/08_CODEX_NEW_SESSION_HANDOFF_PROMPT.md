# 새 PC·새 대화용 Codex 초기 인수인계 프롬프트

아래 블록은 현재 완료 Commit과 문서 정본 경로가 반영된 상태이므로 새 Codex 세션에 그대로 복사한다.

```text
HATAGONG_DES_11 Unity 프로젝트 작업을 이어간다.

Repository:
https://github.com/2Khaz/HATAGONG_DES_11

Project path:
현재 PC의 실제 경로를 먼저 확인할 것.

Unity:
6000.3.19f1

URP:
17.3.0

Expected branch:
Sub_Phase2LogicCore

Expected Phase 2 completion commit:
8d658ed1f2719d3716b8372610ec52cd1ba765b4

Expected latest commit title:
[ADD]260713 7번째 커밋_이정현

Handoff canonical path:
Docs/Phase2_Dev_List/

작업 전에 다음 문서를 순서대로 읽어라.

1. Docs/Phase2_Dev_List/00_HANDOFF_INDEX.md
2. Docs/Phase2_Dev_List/01_PHASE2_GPT_FULL_SPEC.md
3. Docs/Phase2_Dev_List/06_VALIDATION_EVIDENCE_AND_REGRESSION_RISKS.md
4. 현재 작업 목적에 따라:
   - Docs/Phase2_Dev_List/03_PHASE2_DEFERRED_INGAME_WORK.md
   - Docs/Phase2_Dev_List/04_PHASE3_PREIMPLEMENTATION_PLAN.md
   - Docs/Phase2_Dev_List/05_FULL_GAME_AUTOMATED_VALIDATION_PLAN.md

현재 Phase 2 기준선:

- Stage 5B: 157/157
- Stage 5A integrated: 118/118
- Logic: 36/36
- Visual: 34/34
- Runtime Orchestration: 69/69
- Pre-Phase2 Models: 11/11
- Timer: 31/31
- Request Icons: 12/12
- Risk Boundaries: 12/12
- Final Safety: 8/8
- Phase1 Matrix: 120/120
- Phase1 Stress: 1200/1200

사용자 실제 Play Mode 검수:

- Phase1→Phase2 정상
- 좌클릭 유지 상태에서 Board 밖 이동 후 재진입 Stroke 유지
- 99% Clear
- Phase2 Score 1450
- 0.4초 자동 마감
- 자동 마감 중 Logic/Score 불변
- Phase3 미등록 안전 잠금 정상

중요 Production 계약:

- Logic 128×128
- Visual Mask 1024×1024
- Clear 16221/16384
- Easy/Normal/Hard Radius 0.085/0.075/0.065
- Interpolation radius×0.4
- Phase2 max score 1450
- PointerExit는 Stroke를 취소하지 않음
- Deactivate는 Orchestrator/Mask/Progress/Score를 보존
- Presenter는 외부 Mask RT를 Release하지 않음
- Auto Completion은 Shader property만 0→1
- Auto Completion 후에만 PhaseExitReady
- Shared Material Runtime 직접 변경 금지
- Setup 재실행 금지
- 기존 UI/RectTransform 임의 변경 금지

다음 작업 순서:

Phase 3 Production 작업에 들어가기 전에
05_FULL_GAME_AUTOMATED_VALIDATION_PLAN.md의 자동검수 시스템을 먼저 구축한다.

Phase 3의 실제 메커니즘은 아직 확정되지 않았다.
04_PHASE3_PREIMPLEMENTATION_PLAN.md의 미확정 질문을 사용자가 결정하기 전
Phase3Root, Adapter, Logic, Scene을 임의로 만들지 마라.

작업 규칙:

- 먼저 git branch --show-current
- git rev-parse HEAD
- git status --short --untracked-files=all
- 문서와 실제 코드가 다르면 코드가 우선이지만 차이를 보고
- Unity batchmode는 기본 사용 금지
- 열린 Unity Editor와 MCP 기반 검증 우선
- Scene Setup 재실행 금지
- Branch 생성·전환 금지
- reset/clean/rebase/amend/force 금지
- 사용자 승인 전 git add/commit/push 금지
- 테스트 Assertion 삭제·완화 금지
- 사용량 절약보다 정밀도와 검증 강도 우선
- 추가 지시가 생기면 기존 명령과 합친 완전한 통합 프롬프트로 제공
- 각 중간 단계별 별도 보고서를 만들지 말고 해당 작업 전체 종료 후 최종 보고
- 파일을 수정하기 전에 허용 범위와 예상 변경 파일을 먼저 보고

첫 응답에서는 어떤 파일도 수정하지 말고 다음만 보고하라.

1. 현재 Branch
2. 현재 HEAD
3. Working tree 상태
4. Phase 2 completion commit 일치 여부
5. 문서 존재 여부
6. Unity version
7. Compile 상태
8. 다음 작업의 정확한 범위
9. 예상 수정 파일
10. 위험과 금지 범위
```
