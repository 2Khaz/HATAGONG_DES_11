# 인수인계 문서 저장 위치와 Git 커밋 범위

## 1. 중요한 상태

Phase 2 Stage 5B 코드의 최종 감사에서는 다음만 존재했다.

- Tracked modified: 2
- Untracked: 16
- 총 변경 파일: 18
- 예상 밖 변경: 0

이 문서 패키지는 그 감사 이후 `/mnt/data`에 별도로 생성됐다.

따라서 문서를 저장소에 복사하면 “18개만 변경” 상태는 더 이상 사실이 아니다.

---

## 2. 실제 적용 결과 — 현재 정본

- 실제 문서 경로: `Docs/Phase2_Dev_List/`
- Phase 2 기능 파일: 18개
- 인수인계 Markdown: 10개
- 총 Commit 포함 파일: 28개
- 기능과 문서를 분리하지 않고 동일 Commit으로 처리
- 적용 Commit: `8d658ed1f2719d3716b8372610ec52cd1ba765b4`
- 실제 Commit 제목: `[ADD]260713 7번째 커밋_이정현`
- `MANIFEST.json`: 저장소에 포함하지 않음
- ZIP: 저장소에 포함하지 않음

이번 프로젝트에서는 사용자가 기능과 문서를 하나의 Commit으로 처리했다. `Docs/Phase2_Dev_List/`가 현재 인수인계 문서의 정본이며, 향후 작업자는 문서를 다른 경로로 이동하거나 동일 문서를 중복 생성하지 않는다.

---

## 3. 당시 권장 저장 위치 — 미적용

다음 경로는 문서 패키지를 저장소에 넣기 전에 제시한 일반적인 권장안이며 실제 적용 경로가 아니다. 현재 정본 경로로 사용하지 않는다.

저장소 루트:

```text
Docs/
└─ Handoff/
   └─ Phase2_2026-07-13/
      ├─ 00_HANDOFF_INDEX.md
      ├─ 01_PHASE2_GPT_FULL_SPEC.md
      ├─ 02_PHASE2_USER_SUMMARY.md
      ├─ 03_PHASE2_DEFERRED_INGAME_WORK.md
      ├─ 04_PHASE3_PREIMPLEMENTATION_PLAN.md
      ├─ 05_FULL_GAME_AUTOMATED_VALIDATION_PLAN.md
      ├─ 06_VALIDATION_EVIDENCE_AND_REGRESSION_RISKS.md
      ├─ 07_HOME_PC_CONTINUATION_CHECKLIST.md
      ├─ 08_CODEX_NEW_SESSION_HANDOFF_PROMPT.md
      └─ 09_DOCUMENT_PLACEMENT_AND_COMMIT_SCOPE.md
```

`Assets/` 아래에 넣지 않는다.

이유:

- Unity Asset import 대상이 아님
- `.meta` 불필요
- Production Asset과 문서 분리
- 기존 `Docs/` 보고서와 일관됨

---

## 4. 당시 권장 커밋 순서 — 일반 권장안

다음 두 Commit 분리 방식은 일반적인 안전 권장안으로 유지한다. 이번 프로젝트의 실제 적용 결과는 위 2절과 같이 하나의 Commit이다.

### Commit 1 — 검수 완료된 Phase 2 코드
기존 18개 파일만 포함한다.

```text
feat: complete Phase 2 Stage 5B integration
```

이 Commit은 이미 자동·수동·범위 감사가 끝난 단위다.

### Commit 2 — 인수인계 문서
Phase 2 완료 Commit ID를 문서에 반영한 뒤 문서만 포함한다.

```text
docs: add Phase 2 and Phase 3 handoff package
```

장점:

- 기능 Commit의 감사 범위를 보존
- 문서 오탈자를 기능 코드와 분리
- 집 PC에서 Commit 이력 이해가 쉬움
- 문서 수정으로 Scene·Runtime 재검증이 불필요

---

## 5. 문서 복사 후 갱신할 값 — 적용 완료

### `00_HANDOFF_INDEX.md`
현재 실제 Commit 반영값:

```text
Phase 2 완료 커밋 ID: 8d658ed1f2719d3716b8372610ec52cd1ba765b4
```

### `08_CODEX_NEW_SESSION_HANDOFF_PROMPT.md`
현재 실제 Commit 반영값:

```text
8d658ed1f2719d3716b8372610ec52cd1ba765b4
```

Branch를 main에 병합한 뒤 문서를 쓰는 경우 Expected branch도 실제 운영 정책에 맞춰 수정한다.

---

## 6. 당시 문서 커밋 전 읽기 전용 검사

다음 명령은 미적용 권장 경로를 전제로 작성된 역사적 예시다. 현재 문서 검사에는 `Docs/Phase2_Dev_List/`를 사용한다.

```bash
git status --short --untracked-files=all
git diff --check
git diff -- Docs/Handoff/Phase2_2026-07-13
```

문서만 별도 Stage한 뒤:

```bash
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
```

확인:

- Markdown 10개만 포함
- 코드·Scene 없음
- 임시 ZIP 없음
- 로컬 로그 없음
- `.meta` 없음
- Phase 2 완료 Commit ID가 실제 값

---

## 7. ZIP 처리

다운로드 ZIP은 전달 편의용이다.

저장소에는 기본적으로:

- 개별 `.md` 파일만 넣음
- ZIP은 넣지 않음

ZIP을 Commit하면 같은 내용이 중복되고 Diff 검토가 어려워진다.

---

## 8. 문서 유지 관리

Phase 3 설계 확정:
- `04_PHASE3_PREIMPLEMENTATION_PLAN.md` 업데이트

자동검수 구축:
- `05_FULL_GAME_AUTOMATED_VALIDATION_PLAN.md`에 실제 파일·메뉴·Schema 기록

Phase 2 리소스·아이템 구현:
- `03_PHASE2_DEFERRED_INGAME_WORK.md` 상태 변경

검증 기준선 변경:
- `06_VALIDATION_EVIDENCE_AND_REGRESSION_RISKS.md` 갱신

큰 변경마다 문서 버전을 날짜 또는 `r2`, `r3`로 갱신한다.
