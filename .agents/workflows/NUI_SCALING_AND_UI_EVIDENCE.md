# NUI Scaling and UI Evidence Workflow

이 workflow는 1920×1080 reference canvas를 사용하는 Tizen NUI 앱에서 화면 크기와 platform inset을 안전하게 처리하고, Aurum UI automation으로 검증 가능한 screenshot evidence를 만들어 README에 연결하는 공통 절차다.

적용 대상:

- Calendar처럼 top-left ancestor transform을 사용하는 NUI Action 앱
- Reminder처럼 manual exactly-once scaling을 유지하는 기존 NUI 앱의 migration/검증
- D-pad, pointer, ViewAnnotation을 함께 제공하는 앱
- Public Common Emulator에서 초기 검증한 뒤 TV/product profile로 확장할 앱

Calendar는 현재 ancestor-transform 구조를 사용한다. Reminder는 현재 position, size, point size, radius를 helper에서 manual scaling하는 compatibility 구조다. 새 앱은 ancestor transform을 우선하고, 이 문서가 Reminder가 이미 ancestor transform으로 migration됐다는 의미는 아니다.

## 1. Reference canvas와 runtime viewport

### 1.1 실제 drawable area를 읽는다

physical display size를 하드코딩하지 않는다. render 시점마다 다음 값을 읽는다.

```csharp
var windowSize = Window.Default.WindowSize;
var insets = Window.Default.GetInsets();
```

1920×1080 reference canvas라면 viewport는 다음처럼 계산한다.

```text
availableWidth  = windowWidth  - insetStart - insetEnd
availableHeight = windowHeight - insetTop   - insetBottom
scale           = min(availableWidth / 1920, availableHeight / 1080)
contentWidth    = 1920 × scale
contentHeight   = 1080 × scale
offsetX         = insetStart + (availableWidth  - contentWidth)  / 2
offsetY         = insetTop   + (availableHeight - contentHeight) / 2
```

이 정책은 다음을 보장한다.

- 같은 aspect ratio: 전체 canvas uniform scaling
- 4:3: 16:9 canvas를 stretch하지 않고 위아래 남는 영역을 centered top/bottom letterbox로 처리
- ultrawide: stretch하지 않고 centered pillarbox 처리
- platform inset: content를 실제 drawable area 안에 유지

reference canvas 크기가 다른 앱은 1920과 1080을 앱의 design width/height로 바꾸되 계산 순서는 유지한다.

### 1.2 exactly-once ancestor transform을 사용한다

권장 구조:

```text
physical root (window size, full-window background)
  └─ design canvas (1920×1080, top-left pivot)
       ├─ page content in design units
       ├─ focus targets in design units
       └─ overlays/editors in design units
```

- physical root는 전체 window를 채우고 background를 그린다.
- design canvas의 `ParentOrigin`과 `PivotPoint`는 `TopLeft`로 고정한다.
- design canvas에만 `Position = (offsetX, offsetY)`와 uniform `Scale = scale`을 적용한다.
- descendant position, size, font, spacing, radius, border, focus indicator는 모두 design units로 유지한다.
- page와 modal/editor overlay는 같은 transformed canvas 정책을 사용한다.

금지하는 혼합 방식:

- top-level pane만 scaling하고 pane 내부 font/radius/border를 고정 pixel로 유지
- caller와 helper가 각각 같은 좌표를 scaling하는 double scaling
- X/Y는 screen space인데 width/height는 design space인 혼합 geometry
- page는 proportional transform을 쓰지만 overlay는 raw window ratio를 별도 계산

pane별 manual scaling을 선택해야 하는 기존 앱은 position, size, typography, border, radius, focus geometry가 정확히 한 번 scaling되는지 helper contract와 tests로 고정한다. 현재 Reminder가 이 경로이며 `CanvasPosition`, `S`, point-size/radius scaling을 함께 사용한다. top-level offset과 pane-local scaling을 중복 적용하지 않는다.

### 1.3 platform inset과 product safe area를 구분한다

- platform inset은 `Window.Default.GetInsets()`에서 읽는 runtime physical constraint다.
- product safe area는 앱 design에서 정한 design-unit 여백이다.
- platform inset을 제외한 available area에 canvas를 배치한 뒤, canvas 내부에서 product safe area를 적용한다.
- Emulator Back/Home overlay 또는 overscan을 피하기 위한 하단 여백은 상단 여백과 다를 수 있다.

두 safe area를 합쳐 physical pixel처럼 계산하지 않는다. product safe area는 ancestor transform의 영향을 받는 design units로 유지한다.

### 1.4 resize와 transient invalid geometry를 fail-closed 처리한다

```csharp
Window.Default.Resized += OnWindowGeometryChanged;
Window.Default.InsetsChanged += OnWindowGeometryChanged;
```

종료 시 반드시 두 event를 해제한다.

resize/inset transition 중 width, height 또는 available area가 잠시 0 이하가 될 수 있다. 새 viewport가 유효한지 non-throwing `TryCreate`로 확인한 뒤에만 기존 root를 교체한다.

```text
read WindowSize/GetInsets
  → TryCreate viewport
  → invalid: keep current root and skip this frame
  → valid: create new root
  → attach new root
  → dispose/replace old root according to app lifecycle policy
```

invalid frame에서 기존 root를 먼저 제거하거나 throwing viewport factory를 직접 호출하지 않는다.

## 2. ViewAnnotation geometry와 focus

- `ScreenBounds`와 `WindowBounds`는 design coordinate를 단순 수식으로 추정하지 않고 실제 NUI view의 final geometry를 기준으로 만든다.
- ancestor-transform 앱에서는 `CalculateScreenPositionSize()` 결과의 X/Y/width/height가 finite이고 width/height가 양수일 때만 snapshot을 publish한다.
- ancestor-transform 아래의 `View.Size`는 design-space size이므로 world geometry가 준비되지 않았을 때 screen-space width/height fallback으로 사용하지 않는다.
- manual exactly-once scaling을 유지하는 기존 앱에서 `View.Size`가 이미 physical scaled size인 경우에만 compatibility fallback을 둘 수 있다. 이 경우에도 X/Y의 validity, scaled-size contract, non-1.0 native bounds를 별도로 검증하고 새 앱에는 일반화하지 않는다.
- 현재 Calendar는 strict fail-closed path이고, Reminder는 manually scaled `View.Size` compatibility fallback을 유지한다.
- annotation은 visible active surface에 있는 stable Entity ID만 포함한다.
- focus 변경, render, overlay open/close, pause/terminate에서 snapshot lifecycle을 갱신한다.
- `GetFocusedView` 검증 전 앱을 foreground로 만들고 실제 annotated actor에 focus를 이동한다. command bar처럼 annotation이 없는 control의 empty result를 provider defect로 오판하지 않는다.

필수 device flow:

```text
GetAnnotatedViews
  → finite positive bounds
  → FindById and identical bounds
  → focus annotated actor
  → GetFocusedView
  → ToPresentation
  → bounded missing-ID failure
```

## 3. Geometry test matrix

Tizen-free viewport helper와 design metrics는 최소 다음을 검증한다.

| Window | Zero-inset expected result | 목적 |
|---|---|---|
| 1920×1080 | scale 1.0, offset 0/0 | reference |
| 1280×720 | scale 2/3, offset 0/0 | smaller 16:9 |
| 1440×1080 | scale 0.75, offset 0/135 | 4:3 letterbox |
| 2560×1080 | scale 1.0, offset 320/0 | ultrawide pillarbox |

추가 assertions:

- non-zero asymmetric platform insets
- product top/bottom safe area가 transformed physical boundary 안에 있음
- zero/negative window dimension rejection
- insets가 drawable area를 모두 소진할 때 `TryCreate` failure
- pane와 focus target이 reference canvas bounds를 넘지 않음
- render policy와 renderer가 같은 design-space content height를 사용

host geometry test는 native profile 검증이 아니다. 1280×720 host test만 통과했다면 README와 완료 보고에 실제 1280×720 render/focus/View bounds를 검증했다고 쓰지 않는다.

## 4. Aurum UI automation과 screenshot capture

### 4.1 repository wrapper를 사용한다

standalone PATH command를 가정하지 않는다.

```sh
cd <repository-root>
SKILL_ROOT="$PWD/.agents/skills/tizen-aurum-ui-automation"
python3 "$SKILL_ROOT/scripts/prepare_client.py"
AURUM="$SKILL_ROOT/scripts/aurum-ui"

SERIAL=<target-serial>
"$AURUM" session-start --serial "$SERIAL"
"$AURUM" health
"$AURUM" tree --max-depth 4
```

health에서 native target resolution을 먼저 확인한다. host Emulator window 좌표를 Aurum coordinate로 사용하지 않는다.

### 4.2 fixture는 public Action path로 만든다

화면 capture를 위해 app data JSON이나 platform database를 직접 수정하지 않는다. 가능하면 public Action wire path로 deterministic fixture를 생성한다.

- title/ID/time을 고정한다.
- capture 전에 expected page/filter/detail state를 만든다.
- screenshot 후 fixture를 cleanup하거나 문서에서 test fixture임을 명시한다.
- restart persistence를 주장할 때는 앱을 실제 terminate/relaunch한 뒤 public query로 확인한다.

### 4.3 accessibility tree capability를 독립적으로 기록한다

Aurum tree가 empty roots를 반환해도 key, pointer, screenshot RPC는 동작할 수 있다.

```text
tree usable
  → element/geometry 기반 조작 가능

tree empty
  → D-pad 또는 calibrated native coordinates
  → 각 state change 뒤 screenshot postcondition
  → semantic element lookup을 했다고 주장하지 않음
```

visible label에서 임의 element ID를 만들지 않는다.

### 4.4 capture loop

각 화면마다 다음을 반복한다.

```text
launch/reset app
  → deterministic fixture 확인
  → 한 번의 key/click/tap
  → layout stabilization
  → focus/selection/overlay 상태 screenshot 확인
  → pointer를 content 밖으로 이동
  → stable repository path에 native screenshot 저장
```

예:

```sh
"$AURUM" key right
"$AURUM" key down --count 2
"$AURUM" key enter
"$AURUM" move 1900 1040
"$AURUM" screenshot <Domain>/docs/images/<domain>-detail.png
```

RPC success만으로 control activation을 증명하지 않는다. 화면 전환, focus cue, detail/editor content가 보이는 fresh frame을 postcondition으로 사용한다.

권장 capture coverage:

- 모든 primary navigation page/tab
- 대표 list selection과 detail
- search/filter 적용 상태
- create/edit form
- destructive confirmation 또는 cancel state
- focus indicator가 명확한 화면
- empty/error state가 요구사항에 포함된 경우 해당 상태

### 4.5 session cleanup

capture가 끝나면 이 작업이 소유한 scoped SDB forward를 정리한다.

```sh
"$AURUM" session-stop --serial "$SERIAL"
```

이번 workflow가 bootstrap을 시작했고 다른 consumer가 사용하지 않는 것이 확인된 경우에만 `--stop-bootstrap`을 추가한다. app restart/persistence 검증이 목적이 아니라면 capture state를 불필요하게 파괴하지 않는다.

## 5. Screenshot와 README evidence

### 5.1 repository asset

- screenshot은 `<Domain>/docs/images/` 아래에 저장한다.
- `/tmp` path를 README에 link하지 않는다.
- descriptive stable filename을 사용한다.
- build output, raw BGRA frame, generated client environment는 commit하지 않는다.

각 image를 decode하고 dimensions를 검증한다.

```sh
python3 - <<'PY'
from pathlib import Path
from PIL import Image

for path in sorted(Path('<Domain>/docs/images').glob('*.png')):
    with Image.open(path) as image:
        image.verify()
    with Image.open(path) as image:
        print(path, image.size, image.mode)
PY
```

README의 relative image link가 실제 file로 resolve되는지도 script 또는 Markdown link checker로 확인한다.

### 5.2 README 필수 내용

각 domain README에는 다음을 기록한다.

1. 구현된 primary pages와 주요 interaction states
2. screenshot gallery
3. reference canvas와 inset-aware scaling 공식 또는 공통 workflow link
4. target serial/label, profile, platform, app ID, native resolution
5. Aurum wrapper path와 input/capture mechanism
6. accessibility-tree capability
7. deterministic fixture 생성 방식
8. Action/View E2E 결과
9. host geometry와 actual native profile 검증 범위의 구분
10. platform overlay, 미검증 profile, 알려진 limitation

provenance 예:

```text
Capture date: YYYY-MM-DD
Target serial/label: <serial> / <label>
Profile: Public Tizen Common Emulator
Application ID: <application-id>
Resolution: 1920×1080
Automation: .agents/skills/tizen-aurum-ui-automation/scripts/aurum-ui
Input: Aurum remote keys and/or native coordinate input
Capture: native Aurum screenshot RPC
Tree capability: usable / empty roots / unavailable
Fixture data: public Action-created deterministic fixtures
```

### 5.3 evidence 표현 규칙

다음 gate를 별도로 보고한다.

```text
Host viewport/geometry tests: PASS
App Release build: PASS
TPK package/install: PASS
Action/View wire E2E: PASS
Native 1920×1080 render and D-pad: PASS
Native 1280×720 render and View bounds: NOT VERIFIED
TV/product profile: NOT VERIFIED
```

screenshot은 기능 correctness 전체를 증명하지 않는다. Action result, persistence, focus navigation, pointer activation, ViewAnnotation bounds는 각각 실제 path로 검증한다.

## 6. Commit 전 checklist

- [ ] 실제 `WindowSize`와 `GetInsets()`를 사용한다.
- [ ] reference canvas ancestor transform이 exactly once 적용된다.
- [ ] page와 overlay가 같은 coordinate policy를 사용한다.
- [ ] invalid viewport에서 기존 frame을 유지한다.
- [ ] resize/inset handlers를 subscribe/unsubscribe한다.
- [ ] View bounds에 design-space size fallback이 없다.
- [ ] four-shape host geometry matrix와 inset edge cases가 통과한다.
- [ ] 최신 TPK를 install/launch했다.
- [ ] D-pad/pointer 상태 변화마다 visual postcondition이 있다.
- [ ] primary/detail/filter/editor screenshot coverage가 있다.
- [ ] 모든 image가 decode되고 dimensions가 맞는다.
- [ ] README image links와 provenance가 유효하다.
- [ ] host geometry와 native profile evidence를 구분했다.
- [ ] build/package 및 `/tmp` artifacts가 staged되지 않았다.
- [ ] pre-existing unrelated worktree가 commit scope에서 제외됐다.
