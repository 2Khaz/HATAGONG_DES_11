# 다른 로컬 PC에서 안전하게 이어서 작업하는 체크리스트

## 1. 현재 PC에서 출발 전

### 1.1 Phase 2 코드 커밋
현재 검수된 18개 파일만 Stage한다.

권장 제목:

```text
feat: complete Phase 2 Stage 5B integration
```

Commit 후 확인:

```bash
git status
git rev-parse HEAD
git show --stat --oneline --decorate HEAD
```

기대:

- Working tree clean
- 정확한 18개 파일
- 최종 Commit ID 확보

### 1.2 원격 Branch 푸시

```bash
git push origin Sub_Phase2LogicCore
```

확인:

```bash
git fetch origin --prune
git rev-parse HEAD
git rev-parse origin/Sub_Phase2LogicCore
```

두 값이 같아야 한다.

### 1.3 문서 패키지 처리
이 문서들은 기존 18개 파일 감사 이후 생성됐다.

권장:

1. Phase 2 코드 18개를 먼저 별도 커밋·푸시
2. 문서를 `Docs/Handoff/Phase2_2026-07-13/`에 복사
3. 문서의 완료 Commit ID 갱신
4. 별도 문서 커밋

권장 문서 커밋 제목:

```text
docs: add Phase 2 and Phase 3 handoff package
```

---

## 2. 집 PC에 프로젝트가 없는 경우

설치:

- Git
- Unity Hub
- Unity `6000.3.19f1`
- VS Code
- C#·Unity 관련 Extension
- Codex 로그인
- MCP를 사용할 경우 로컬 연결 설정

Clone:

```bash
git clone https://github.com/2Khaz/HATAGONG_DES_11.git
cd HATAGONG_DES_11
git fetch origin --prune
git switch --track origin/Sub_Phase2LogicCore
```

이미 Branch가 만들어졌다면:

```bash
git switch Sub_Phase2LogicCore
git pull --ff-only origin Sub_Phase2LogicCore
```

---

## 3. 집 PC에 기존 프로젝트가 있는 경우

Unity와 VS Code를 닫는다.

```bash
git status --short --untracked-files=all
```

### 깨끗할 때

```bash
git switch Sub_Phase2LogicCore
git fetch origin --prune
git pull --ff-only origin Sub_Phase2LogicCore
```

### 로컬 변경이 있을 때

바로 Pull하지 않는다.

```bash
git status
git diff
git diff --cached
```

정체를 모르는 변경에 다음을 사용하지 않는다.

- `reset --hard`
- `clean -fd`
- 강제 checkout
- force push

필요한 변경은 별도 Commit 또는 사용자가 확인한 방식으로 보관한다.

---

## 4. Commit 일치 확인

현재 PC에서 기록한 Phase 2 완료 Commit과 비교한다.

```bash
git branch --show-current
git status
git rev-parse HEAD
git rev-parse origin/Sub_Phase2LogicCore
```

기대:

- Branch: `Sub_Phase2LogicCore`
- Working tree clean
- local HEAD = remote Branch = 기록한 완료 Commit

---

## 5. Unity 프로젝트 열기

Unity Hub에서 다음이 있는 최상위 폴더를 연다.

```text
Assets/
Packages/
ProjectSettings/
```

버전:

```text
6000.3.19f1
```

처음 열 때는 Package 복원, Shader Import, Script Compile, Library 생성에 시간이 걸릴 수 있다.

다른 PC에서 복사하지 않을 폴더:

- Library
- Temp
- Logs
- obj
- UserSettings

이 폴더들은 PC별 캐시·설정이다.

---

## 6. PC별 설정

### Unity 외부 편집기

```text
Edit
→ Preferences
→ External Tools
→ External Script Editor
→ Visual Studio Code
```

### VS Code
- 저장소 최상위 폴더 열기
- C# Language Server 정상 확인
- Codex 로그인
- 필요한 Workspace 설정 확인

### MCP
Unity Package는 저장소에서 복원되더라도 다음은 PC별일 수 있다.

- 로컬 서버 실행
- VS Code client config
- Session 인증
- Port
- Startup config

---

## 7. 집 PC 첫 실행 최소 검증

Setup을 다시 실행하지 않는다.

최소:

1. Console Compile Error 0
2. `Assets/Scenes/INGAME.unity` 정상 로드
3. Stage 5B Validation

```text
Tools > HATAGONG > Phase2 > Validate Stage 5B Scene Integration
```

기대:

```text
157/157, failures=0
```

추가 권장:

```text
Stage 5A integrated: 118/118
```

새 PC에서 코드가 바뀌지 않았다면 전체 Matrix·Stress를 다시 돌릴 필요는 없다.

---

## 8. 작업 시작 전 Snapshot

새 작업 프롬프트에 다음을 붙인다.

```text
Branch:
Sub_Phase2LogicCore

Phase 2 completion commit:
<실제 Commit ID>

Unity:
6000.3.19f1

Baseline:
Stage5B 157/157
Stage5A 118/118
Logic 36/36
Visual 34/34
Orchestration 69/69

Do not rerun Scene Setup.
Do not commit or push without user approval.
```

---

## 9. Main 병합 여부

집 PC에서 이어서 Phase 3 전 자동검수 시스템을 만들 목적이면 `Sub_Phase2LogicCore`에서 계속 작업해도 된다.

Main 반영은:

- force push 금지
- 직접 refspec 역푸시 금지
- 원격 main 최신 상태 확인
- fast-forward 또는 정상 PR/merge

사용자가 직접 결정한다.
