# HATAGONG_DES_11 Phase 2 완료 및 Phase 3 착수 전 인수인계 패키지

- 작성일: 2026-07-13
- 프로젝트: `HATAGONG_DES_11`
- 저장소: `https://github.com/2Khaz/HATAGONG_DES_11`
- Unity: `6000.3.19f1`
- URP: `17.3.0`
- 현재 기준 브랜치: `Sub_Phase2LogicCore`
- 현재 기준 HEAD: `8d658ed1f2719d3716b8372610ec52cd1ba765b4`
- 실제 최신 Commit 제목: `[ADD]260713 7번째 커밋_이정현`
- 상태: **Phase 2 Stage 5B 구현 18개 파일과 인수인계 Markdown 10개가 동일 Commit에 포함되어 `origin/Sub_Phase2LogicCore`에 반영 완료**
- Phase 2 완료 커밋 ID: `8d658ed1f2719d3716b8372610ec52cd1ba765b4`
- 문서 정본 경로: `Docs/Phase2_Dev_List/`
- 문서 패키지 버전: `2026-07-13-r1`

> 이 문서는 “현재 구현된 사실”, “차후 작업”, “아직 확정되지 않은 설계”를 분리한다.  
> Phase 3의 실제 게임 메커니즘은 아직 확정 자료가 없으므로 임의로 발명하지 않는다.

---

## 1. 문서 목록

### `01_PHASE2_GPT_FULL_SPEC.md`
GPT·Codex가 Phase 2의 설계, 코드 구조, 수치, 생명주기, Scene 연결, 검증 계약을 빠짐없이 복구하기 위한 상세 기술 명세서다.

### `02_PHASE2_USER_SUMMARY.md`
사용자가 Phase 2의 현재 완성 상태와 남은 일을 빠르게 읽기 위한 요약본이다.

### `03_PHASE2_DEFERRED_INGAME_WORK.md`
인게임 총괄 작업에서 Phase 2에 추가해야 할 리소스, UI, 아이템, Request 효과, 사운드, 모바일 검증, 밸런스 작업을 관리한다.

### `04_PHASE3_PREIMPLEMENTATION_PLAN.md`
Phase 3에 들어가기 전에 확정해야 할 설계와 실제 구현 순서를 정리한다. 현재 확정된 사실과 미확정 질문을 분리한다.

### `05_FULL_GAME_AUTOMATED_VALIDATION_PLAN.md`
Phase 3 작업 전에 만들기로 한 실제 Play Mode 기반 통합 자동검수 시스템의 구현 명세다.

### `06_VALIDATION_EVIDENCE_AND_REGRESSION_RISKS.md`
Phase 2 완료 판정의 근거, 통과한 검증 수치, 실제 Play Mode 결과, 과거 결함과 재발 방지 기준을 기록한다.

### `07_HOME_PC_CONTINUATION_CHECKLIST.md`
현재 PC에서 커밋·푸시하고 집 PC에서 같은 브랜치와 Commit으로 안전하게 이어가는 절차다.

### `08_CODEX_NEW_SESSION_HANDOFF_PROMPT.md`
새 PC 또는 새 대화에서 Codex에게 그대로 복사해 전달할 초기 인수인계 프롬프트다.

### `09_DOCUMENT_PLACEMENT_AND_COMMIT_SCOPE.md`
이 문서들을 저장소에 넣을 위치와, 기존 18개 파일 감사 결과를 깨뜨리지 않고 문서 커밋을 관리하는 방법을 설명한다.

---

## 2. 권장 읽기 순서

### 사용자가 읽을 때
1. `02_PHASE2_USER_SUMMARY.md`
2. `03_PHASE2_DEFERRED_INGAME_WORK.md`
3. `04_PHASE3_PREIMPLEMENTATION_PLAN.md`
4. `05_FULL_GAME_AUTOMATED_VALIDATION_PLAN.md`
5. `07_HOME_PC_CONTINUATION_CHECKLIST.md`

### GPT·Codex가 읽을 때
1. `00_HANDOFF_INDEX.md`
2. `01_PHASE2_GPT_FULL_SPEC.md`
3. `06_VALIDATION_EVIDENCE_AND_REGRESSION_RISKS.md`
4. 현재 수행할 작업에 따라 `03`, `04`, `05`
5. 저장소의 실제 코드와 `git status`

---

## 3. 현재 완료 상태

### Phase 2 기능
- Phase 1 완료 후 Phase 2 전환
- 별도 READY 없이 전환 Overlay midpoint에서 Phase 2 활성화
- PC 좌클릭 드래그 도포
- 단일 Pointer 계약
- 보드 밖 이동 후 같은 클릭을 유지하여 재진입하면 Stroke 지속
- 128×128 논리 판정
- 1024×1024 GPU Mask
- 99%인 16,221/16,384셀에서 Clear
- Phase 2 최대 점수 1,450
- 99% 도달 후 0.4초 시각 자동 마감
- 자동 마감 중 Logic·Score 불변
- 완료 후 PhaseExitReady 1회
- Phase 3 미등록 시 안전 잠금

### 최종 검증
- Stage 5B: `157/157`
- Stage 5A integrated: `118/118`
- Logic: `36/36`
- Visual: `34/34`
- Runtime Orchestration: `69/69`
- 기존 공용·Phase 1 회귀검증 전부 통과
- 사용자 실제 Play Mode 통합 스모크 통과

---

## 4. 진실의 우선순위

자료가 충돌할 때 다음 순서로 판단한다.

1. 현재 브랜치의 실제 Production 코드와 Scene
2. 2026-07-13 최종 Validation 결과
3. 이 인수인계 패키지
4. 기존 Stage별 보고서
5. 초기 개발 지시서와 아이디어 문서

예를 들어 초기 설계에는 난이도별 별도 “가장자리 완화”가 제안됐지만, 현재 Production Core는 전체 128×128 Grid, 99% threshold, 원-보드 교차 판정으로 동작한다. 초기 제안을 구현된 사실로 오해하면 안 된다.

---

## 5. 문서 갱신 규칙

Phase 2 Stage 5B 완료 Commit ID는 다음 두 문서에 현재 실제 값으로 기록되어 있다.

- `00_HANDOFF_INDEX.md`
- `08_CODEX_NEW_SESSION_HANDOFF_PROMPT.md`

향후 Phase 2 완료 기준 Commit이나 운영 Branch가 변경되면 두 문서의 Expected 값과 실제 저장소 상태를 함께 갱신하고, 문서 정본은 `Docs/Phase2_Dev_List/`에서 유지한다.

Phase 3 설계가 확정되면 `04_PHASE3_PREIMPLEMENTATION_PLAN.md`의 “미확정 질문”을 실제 명세로 대체한다.

Full Game 자동검수 시스템이 완성되면 `05_FULL_GAME_AUTOMATED_VALIDATION_PLAN.md`에 실제 메뉴, 파일 경로, JSON Schema 버전과 최종 Assertion 수를 기록한다.
