# Tizen Action Domain 개발 가이드라인

이 문서는 `Calendar` 구현과 Public Tizen 10.1 Common Emulator 검증에서 확인한 방법을 다른 Action domain에도 재사용하기 위한 개발 기준이다.

적용 대상:

- Tizen Action/TIDL 기반 C# provider
- NUI UI를 포함하거나 독립 provider로 동작하는 domain
- CRUD, 검색, Entity resolver, persistence, alarm/notification 등 상태를 갖는 Action
- Public Common Emulator 초기 검증 후 TV 또는 제품 target으로 확장하는 프로젝트

> 이 문서의 명령과 결과는 개발 기준이다. 실제 source, target capability, 현재 Action schema, 그리고 fresh command output이 최종 진실이다.

---

## 1. 완료 기준과 원칙

### 1.1 계층별 완료 기준을 분리한다

다음은 서로 대체되지 않는 별도 gate이다.

| Gate | 증명하는 것 | 증명하지 않는 것 |
|---|---|---|
| Domain/use-case host test | business rule, rollback, persistence transition | Tizen runtime/provider RPC |
| `dotnet build` | C# source 및 referenced assembly compile | manifest/signing/device runtime |
| TPK package | payload, manifest, signature archive | target installation/runtime |
| Emulator Action E2E | registration, wire format, typed RPC result | NUI visual/input acceptance |
| UI acceptance | focus, remote, pointer, layout, editability | provider-only correctness |

최종 보고는 `PASS`를 한 줄로 합치지 않는다. 예:

```text
Host tests/build: PASS
TPK packaging: PASS
Action provider E2E: PASS
Persistence/restart/alarm: PASS
D-pad/pointer/screenshot visual acceptance: NOT VERIFIED
TV profile validation: NOT VERIFIED (Common emulator only)
```

### 1.2 변경 전 보존 규칙

1. `git status --short`와 branch를 기록한다.
2. 기존 untracked tree, local patch, `.dev/`, `.hermes/`를 삭제/clean/reset하지 않는다.
3. 사용자가 요청하지 않는 commit/push를 하지 않는다.
4. generated code는 직접 수정하지 않는다. generator input 또는 template을 수정하고 재생성한다.
5. Platform-owned Action schema와 application-owned implementation을 섞지 않는다.
6. `action.seq`의 기존 method order는 ABI다. 기존 항목을 재정렬하지 않고 추가만 한다.

---

## 2. 권장 아키텍처

### 2.1 Domain, use-case, adapter를 분리한다

권장 경계:

```text
<Domain>
  Entity/value object
  Thread-safe repository
  Search/resolver/query rules

<UseCases>
  Command service
  Transaction and compensation
  Interfaces: persistence, scheduling, external services

<Persistence>
  Store document/versioning
  Atomic JSON/file implementation

<App>
  Tizen/NUI composition root
  Tizen adapters: alarm, notification, app data directory
  UI rendering and input dispatch

<ActionProvider>
  Generated binding + thin provider service
  Entity ↔ domain conversion and typed status mapping
```

의존성은 안쪽으로 향하게 한다.

```text
NUI / Action provider / Tizen adapter
             ↓
         UseCases
             ↓
           Domain
```

`Domain`과 `UseCases`는 Tizen runtime assembly 없이 host에서 실행 가능해야 한다. generated `ServiceBase`는 반드시 thin adapter로 유지한다.

### 2.2 UI와 provider는 같은 service instance를 공유한다

한 프로세스에 UI와 provider가 함께 있을 때:

- UI가 자기 Action RPC를 호출해서 mutation하지 않는다.
- UI와 provider는 같은 repository 및 `CommandService` 인스턴스를 composition root에서 주입받는다.
- provider Action이 성공하면 UI repository에서 즉시 같은 상태를 조회할 수 있어야 한다.

```csharp
var events = new DomainRepository([]);
var reminders = new ReminderRepository([]);
var commands = new DomainCommandService(events, reminders, persistence, scheduler);

ActionProviderHost.Start(events, commands);
NuiApplication.Start(events, reminders, commands);
```

### 2.3 상태 변경은 persistence-first publish를 사용한다

외부 side effect가 있는 command는 다음 순서를 기본으로 한다.

1. 현재 repository snapshot을 확보한다.
2. 새 alarm/외부 resource가 필요하면 먼저 생성하고 ID를 기록한다.
3. 원하는 document를 persistence에 저장한다.
4. 성공한 경우에만 repository snapshot을 publish한다.
5. 실패하면 이번 operation에서 새로 만든 resource만 best-effort로 취소한다.

기존 resource를 대체하는 update는 replacement를 만든 뒤 document 저장 성공 후 기존 resource를 취소한다. 이렇게 하면 persistence 실패 때 기존 상태를 보존할 수 있다.

### 2.4 외부 ID의 소유 범위를 명확히 한다

앱이 만든 alarm, notification, file, job 등의 handle은 domain document에 저장한다. 복원 시에는 저장된 application-owned handle만 취소/교체한다.

금지 예:

```csharp
AlarmManager.CancelAll();
```

권장 예:

```csharp
foreach (var item in document.Reminders)
{
    if (item.AlarmId is int existingAlarmId)
    {
        TryCancel(existingAlarmId);
    }
}
```

이는 다른 application의 system resource를 지우지 않는 최소 권한/최소 영향 원칙이다.

---

## 3. TDD 개발 절차

### 3.1 RED → GREEN → REFACTOR를 한 behavior씩 반복한다

새 production code를 추가하기 전에 focused test를 먼저 작성한다.

1. **RED**: 하나의 observable behavior를 명확히 하는 test를 작성한다.
2. targeted test를 실행하고, feature가 없어서 의도대로 실패하는지 확인한다.
3. **GREEN**: test를 통과하는 최소 production code만 작성한다.
4. targeted test를 다시 실행한다.
5. **REFACTOR**: 중복과 명명만 개선하고 test green을 유지한다.
6. suite를 넓혀 regression을 확인한다.

테스트 이름은 구현이 아니라 결과를 설명한다.

```csharp
Assert(
    scheduler.Cancelled.SequenceEqual([persistedAlarmId]),
    "Restore must cancel only persisted app-owned alarm handles before persisting reconciled handles.");
```

### 3.2 Use-case test 최소 목록

상태를 갖는 domain은 최소한 다음을 다룬다.

- create: duplicate, validation, persistence failure compensation
- update: replacement 생성, old handle cancellation, persistence failure
- delete: persistence 성공 뒤 resource cancellation과 repository publish
- search/resolver: stable ID, order, unresolved ID contract
- restore: missing/corrupt document, stale handle replacement, completed/past data non-reschedule
- alarm/notification: future-only scheduling, complete/delete cancellation
- concurrent access: repository snapshot/query/mutation thread safety

### 3.3 Host test는 Tizen-free seam을 테스트한다

generated provider runtime assembly가 host에 없어서 test가 실패한다면, test를 skip하거나 mock으로 빈 결과를 만들지 않는다. behavior를 `UseCases` interface 뒤로 옮기고 host test는 그 seam을 검증한다.

예외:

- generated provider의 실제 wire/runtime behavior는 emulator에서 확인한다.
- host에서는 generated provider가 compile되는 것까지만 독립 gate로 기록한다.

---

## 4. Entity와 Action 설계 기준

### 4.1 Stable ID와 resolver 계약

Entity resolver를 제공하는 domain은 다음을 지킨다.

- ID는 생성 후 변경하지 않는다.
- batch resolver는 요청 order를 보존한다.
- 중복 ID 요청의 결과도 요청 순서에 맞춰 반환한다.
- 찾지 못한 ID는 별도 `unresolvedIds`로 명시한다.
- input count, empty ID, ID length의 upper bound를 둔다.

half-open interval query가 필요한 schedule/calendar 계열은 아래 정의를 문서/테스트에 고정한다.

```text
item.Start < endExclusive
item.End   > startInclusive
```

### 4.2 provider는 입력 변환과 status mapping만 담당한다

Provider service 책임:

- generated entity의 format 검증
- Entity → domain model conversion
- command/query service 호출
- domain result → typed status/output conversion

Provider service가 repository transaction, JSON I/O, alarm scheduling policy를 직접 구현하면 안 된다.

### 4.3 Action input wire-format을 추측하지 않는다

`action-tool`의 schema만으로 Entity argument의 JSON shape가 모호할 수 있다. 다음 순서로 확인한다.

1. generated dispatcher에서 parameter 이름과 Entity field를 확인한다.
2. `action-tool execute --help`를 확인한다.
3. 하나의 positive input과 하나의 negative input을 실제 device에서 실행한다.
4. 이후 성공한 invocation shape를 test/runbook에 기록한다.

single-Entity Action의 경우 Calendar 검증에서는 generated parameter wrapper 아래가 아니라 `params.arguments`에 Entity field를 직접 넣는 형식이 동작했다.

```sh
action-tool execute '{
  "id": 101,
  "params": {
    "name": "<ActionName>",
    "appid": "<application-id>",
    "arguments": {
      "Id": "e2e-item-101",
      "Title": "Example"
    }
  }
}'
```

항상 target schema와 actual generated code를 기준으로 다시 확인한다.

---

## 5. Persistence 가이드

### 5.1 Store document

- top-level schema version을 둔다.
- schema version을 unknown 상태로 조용히 무시하지 말고 거부한다.
- missing file은 empty state로 복구 가능해야 한다.
- corrupt JSON은 backup 후 empty recovery 또는 명시적 failure policy를 택한다.
- write는 temporary file 후 atomic replace를 사용한다.
- save 실패가 기존 valid file을 손상시키지 않아야 한다.

### 5.2 Restart reconciliation

앱 시작 순서:

```text
Load persisted document
  → persisted app-owned external handles individually cancel
  → future + incomplete item만 schedule
  → replacement handle을 포함한 document save
  → repositories publish
```

다음은 재예약하지 않는다.

- 과거 due time
- completed item
- 삭제된 item
- document에 없거나 app이 소유하지 않는 handle

---

## 6. NUI와 입력 UX 기준

### 6.1 UI state는 rendering과 분리한다

다음 같은 pure state/reducer를 먼저 test한다.

- selected/visible date or selected entity
- focus region/index
- overlay/editor/delete-confirmation state
- Back hierarchy
- remote navigation transition
- pointer semantic command

D-pad와 pointer/touch는 같은 business command를 dispatch한다. device-dependent view event에서 business rule을 중복 구현하지 않는다.

### 6.2 Focus와 pointer activation

TV/NUI 화면에서는 시각적 focus ring과 non-color cue를 제공한다. pointer click 계약은 명시적으로 test한다.

```text
Down → Up-inside: exactly one activation
Up without Down: no activation
Up outside: no activation
Cancel: no activation
Consumed sequence: no second activation
```

### 6.3 destructive flow와 editor

- Delete는 대상 title/ID와 날짜/시각처럼 식별 가능한 내용을 confirmation에 표시한다.
- Cancel은 원래 detail/editor context로 정확히 복귀한다.
- input field는 실제로 focus/edit/save 가능한 native control이어야 한다.
- disabled/decorative Add button을 남기지 않는다.

---

## 7. Generated C# provider 관리

### 7.1 재생성 규칙

generated C#은 절대 수동 수정하지 않는다.

```sh
export ACTIONC_ACTION2TIDL="$HOME/.local/bin/action2tidl"
export ACTIONC_TIDLC="$HOME/.local/bin/tidlc"

actionc \
  -a <Action.Category> \
  -d /path/to/default-actions \
  -l 'C#' \
  -o <ProviderProject>/Generated/<ProviderName>
```

주의:

- `-o` output base에는 `.cs` 확장자를 붙이지 않는다.
- ABI-sensitive category는 필요한 action만 임의로 골라 생성하지 않는다. generator가 요구하는 전체 category order를 유지한다.
- 재생성 결과는 temporary location과 repository output을 `cmp` 또는 checksum으로 비교한다.
- schema/action definition 자체를 바꾸는 작업은 platform/default-actions 소유 여부를 먼저 확인한다.

### 7.2 unsupported inherited Action

한 category가 domain에 필요 없는 inherited Action을 포함하는 경우:

- generated method 자체는 유지한다.
- typed status로 명시적 `unsupported`를 반환한다.
- silent success나 unimplemented exception을 반환하지 않는다.

---

## 8. Build와 TPK packaging

### 8.1 Host gate

프로젝트별 canonical test/build command를 문서화하고 모든 변경 전후 실행한다.

예시:

```sh
set -euo pipefail

dotnet run --project <Domain.Tests>/<Domain.Tests>.csproj
dotnet run --project <Persistence.Tests>/<Persistence.Tests>.csproj
dotnet run --project <UseCases.Tests>/<UseCases.Tests>.csproj
dotnet run --project <App.Tests>/<App.Tests>.csproj

dotnet build <ActionProvider>.csproj --configuration Debug --no-restore
dotnet build <SecondActionProvider>.csproj --configuration Debug --no-restore
dotnet build <App>.csproj --configuration Debug --no-restore

git diff --check
```

### 8.2 Public emulator default signing

Public emulator 전용 package는 custom signing profile을 지정하지 않는다.

```sh
tizen package -t tpk -o "$PACKAGE_OUTPUT" -- "$STAGE"
```

`-s`를 추가하면 custom profile을 명시 선택하게 된다. default signer warning은 emulator 전용 package라는 뜻이며, distribution/production certificate 성공 증거가 아니다.

### 8.3 net8.0 managed TPK staging

host-compatible `net8.0` output에 stale nested `packaging/` directory가 있을 수 있다. staging root에는 `bin/<configuration>/net8.0/`의 top-level regular file만 복사한다.

```sh
OUT="$PWD/<App>/bin/Debug/net8.0"
STAGE="$(mktemp -d /tmp/action-domain-stage.XXXXXX)"
PACKAGE_OUTPUT="$(mktemp -d /tmp/action-domain-package.XXXXXX)"

python3 - "$OUT" "$STAGE" <<'PY'
import os
import shutil
import sys
source, destination = sys.argv[1:]
for name in os.listdir(source):
    path = os.path.join(source, name)
    if os.path.isfile(path):
        shutil.copy2(path, os.path.join(destination, name))
PY

cp <App>/tizen-manifest.xml "$STAGE/tizen-manifest.xml"
tizen package -t tpk -o "$PACKAGE_OUTPUT" -- "$STAGE"
```

generic packager가 executable-like filename을 출력해도 ZIP based payload일 수 있다. 반드시 검증 후 명시적 `.tpk` 이름으로 전달한다.

```sh
unzip -t "$PACKAGE_OUTPUT/<emitted-file>"
unzip -Z1 "$PACKAGE_OUTPUT/<emitted-file>"
sha256sum <final>.tpk
```

필수 archive entry:

```text
author-signature.xml
signature1.xml
tizen-manifest.xml
bin/<MainApplication>.dll
lib/<all project-reference DLLs>
lib/<App executable/runtimeconfig/deps>
```

---

## 9. Emulator deployment 및 Action E2E

### 9.1 target preflight

```sh
sdb devices
sdb -s <serial> capability
sdb -s <serial> shell 'id'
```

확인 항목:

- connected state
- `profile_name`과 manifest profile 정합성
- platform version
- architecture
- guest shell/root availability

Common profile에서 성공한 것은 Common emulator validation이다. TV profile, Samsung vendor, remote-key policy 검증으로 표현하지 않는다.

### 9.2 install, launch, runtime survival

```sh
sdb -s <serial> install <package>.tpk
/home/hjhun/tizen-studio/tools/ide/bin/tizen run -s <serial> -p <application-id>
sdb -s <serial> shell 'app_launcher --is-running=<application-id>'
sdb -s <serial> shell 'ps -ef | grep <application-id> | grep -v grep'
```

install transport 성공만으로 registration 또는 provider runtime 성공이라고 결론 내리지 않는다.

### 9.3 provider discovery

TPK direct install은 package manager가 action-provider metadata를 등록할 수 있다. 다음 postcondition을 우선 확인한다.

```sh
sdb -s <serial> shell 'action-tool find-appids <Action.Category> --json'
```

application ID가 발견되고 explicit `appid` invocation이 성공하면 registration은 성공이다.

`TPK`에 대해 `tpk-backend --preload`을 시도할 필요가 있는 경우에도, preload failure와 discovery/invocation result를 분리해서 기록한다. RPM-backed fixture는 `unified-backend --preload -y <package-id>`가 필요한 별도 flow이다.

### 9.4 Action E2E 최소 시나리오

각 advertised Action에 대해:

1. provider discovery
2. positive typed result
3. one bounded validation/negative case
4. domain postcondition
5. mutation 이후 search/resolver postcondition
6. cleanup

CRUD domain 최소 흐름:

```text
Create → Get/Resolve → Search → Update → presentation/query verification → Delete → not-found verification
```

reminder/schedule domain 최소 흐름:

```text
Create → Search → Update → Complete/Reopen as applicable → Search state verification → Delete
```

### 9.5 persistence/alarm/restart E2E

1. future persistent entity와 reminder를 생성한다.
2. app data JSON의 stored ID/state를 확인한다.
3. device alarm dump 또는 official scheduler query로 app ID와 scheduled alarm을 확인한다.
4. app terminate 후 launch한다.
5. persisted entity와 reminder를 Action query/UI에서 확인한다.
6. stale alarm ID가 replacement ID로 바뀌었는지 확인한다.
7. completed/past/deleted item이 reschedule되지 않았는지 확인한다.

Public Common Emulator에서 alarm dump 예:

```sh
sdb -s <serial> shell 'alarmmgr_tool -d'
sdb -s <serial> shell 'tail -100 /var/log/appfw/alarmmgr_log/registered_all_alarms.log'
```

notification popup visibility는 scheduler evidence와 별도의 UI/OS policy gate로 기록한다.

---

## 10. 다른 domain을 시작하는 체크리스트

### Design

- [ ] Entity, stable ID, ownership, lifecycle을 정의했다.
- [ ] platform-owned schema인지 application-owned schema인지 확인했다.
- [ ] Query/resolver ordering 및 unresolved behavior를 정의했다.
- [ ] external handle(alarm/job/notification/file)의 ownership과 cleanup 범위를 정의했다.
- [ ] UI와 provider가 공유할 command/query service composition을 설계했다.

### TDD

- [ ] 첫 feature test가 production code보다 먼저 실패했다.
- [ ] create/update/delete/restore failure compensation test가 있다.
- [ ] host test는 Tizen-free domain/use-case seam만 테스트한다.
- [ ] generated binding runtime evidence는 device E2E로 분리했다.

### Provider

- [ ] generated source provenance를 확인했다.
- [ ] `action.seq` ABI order를 보존했다.
- [ ] Action input JSON shape를 actual device invocation으로 확인했다.
- [ ] 모든 advertised Action이 typed result와 postcondition을 갖는다.

### Packaging and device

- [ ] profile/manifest/emulator compatibility를 확인했다.
- [ ] archive signature, manifest, all dependencies를 검사했다.
- [ ] install, launch, process survival을 각각 확인했다.
- [ ] provider discovery와 explicit `appid` invocation을 확인했다.
- [ ] restart/persistence/external resource reconciliation을 확인했다.

### UI

- [ ] focus ring과 D-pad hierarchy를 확인했다.
- [ ] pointer activation contract를 확인했다.
- [ ] editor field의 실제 input/save를 확인했다.
- [ ] destructive confirmation의 cancel/confirm/back hierarchy를 확인했다.
- [ ] fresh screenshots에 clipping, touch target, disabled action 문제가 없는지 확인했다.

---

## 11. Calendar 구현에서 재사용 가능한 검증 명령 요약

```sh
cd ~/samba/workspace/tizen-action-examples

# Host tests and builds
set -euo pipefail
dotnet run --project Calendar/tests/Calendar.Domain.Tests/Calendar.Domain.Tests.csproj
dotnet run --project Calendar/tests/Calendar.Persistence.Tests/Calendar.Persistence.Tests.csproj
dotnet run --project Calendar/tests/Calendar.UseCases.Tests/Calendar.UseCases.Tests.csproj
dotnet run --project Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj
dotnet build Calendar/src/Calendar.ActionProvider/Calendar.ActionProvider.csproj --configuration Debug --no-restore
dotnet build Calendar/src/Calendar.ScheduleActionProvider/Calendar.ScheduleActionProvider.csproj --configuration Debug --no-restore
dotnet build Calendar/src/Calendar.App/Calendar.App.csproj --configuration Debug --no-restore
git diff --check

# Device preflight
sdb devices
sdb -s emulator-26101 capability
sdb -s emulator-26101 shell 'id'

# Device discovery
sdb -s emulator-26101 shell 'action-tool find-appids Tizen.Action.Calendar --json'
sdb -s emulator-26101 shell 'action-tool find-appids Tizen.Action.Schedule --json'
```

---

## 12. 참고 자료

- `CALENDAR_DEVELOPMENT_HANDOFF.md`: Calendar 구현의 구체적 source, command, 결과
- `.dev/DEVELOPTMENT.md`: 현재 구현 결과와 검증 기록
- `.dev/progress/developer.md`: 개발 결정 및 device evidence
- Tizen Action default action schemas: `/home/hjhun/samba/workspace/appfw/tizen-action/default-actions`
- TIDL implementation repository: `/home/hjhun/samba/workspace/appfw/tidl`

새 domain을 시작할 때 이 문서를 baseline으로 사용하되, domain-specific Entity contract, existing action category ABI, target device profile을 먼저 확인한 뒤 세부 계획을 작성한다.
