# Browser P1 Action contract and validation

P1 is an intermediate ABI migration, not completion of Browser product development.
Evidence was collected on 2026-09-08, Common Emulator `emulator-26101`, 1920×1080.
Package/app: `org.tizen.browser`, version `0.1.0`; package API `10.0`, dotnet API `14`.
Resolved Tizen.NET/API14 remains `14.0.0.19326`. No other app reference was updated.

## Advertised and generated contracts

The entire default Browser category has slots 2–19 and View has slots 2–5.
`check_action_contracts.py` independently runs real actionc and checks every slot,
byte equality for Browser/View/Custom, schema registration, and these eight advertisements:

| Action suffix (prefix below) | Input | Output | Target coverage |
|---|---|---|---|
| Browser_GetCurrentPage | `{}` | `{return:Status,result:WebPageInfo}` | loaded page / Home not_found, non-null empty page |
| Browser_GetTabs | `{}` | `{return:Status,result:Tab[]}` | initial tab, two tabs, selection change; approved failure exception |
| Browser_ToPresentation | WebPageInfo | `{return:Status,result:Presentation}` | current / stale and invalid, empty strings on failure |
| View_FindById | `{id:string}` | `{return:Status,view:View}` | actual / missing and blank ID, nested empty View |
| View_GetAnnotatedViews | `{}` | `{return:Status,views:View[]}` | visible / Home, loading, Tabs empty success; approved failure exception |
| View_GetFocusedView | `{}` | `{return:Status,view:View}` | D-pad page focus / Home and non-page focus not_found |
| View_ToPresentation | View | `{return:Status,result:Presentation}` | current annotation / altered snapshot failure |
| BrowserCustom_GetPageByIds | `{ids:string[]}` | `{return:Status,result:WebPageInfo[],unresolvedIds:string[]}` | ordered duplicates and partial misses / blank and 51 IDs |

Browser prefix is `Tv_Tizen.Action.`, View prefix `Common_Tizen.Action.`,
Custom prefix `App_Tizen.Action.`. Entity names use `Tizen.Entity.`.
Status has only `Success:boolean` and `Reason:string`; reasons are bounded app strings,
not a new platform status enum. Single View result is `view`, never the former `v`.

The unadvertised Browser slots are ControlMedia, ControlTab, Engage, Exit, GetMedia,
GoToScreen, Navigate, OpenPage, Search, SearchPageList, SelectItem, SetMediaOption,
SetScreenOption, ToCalendar, UpdatePageList. Explicit app routing can reach them;
all 15 were invoked and returned `unavailable: capability_not_enabled`, with non-null
empty/nested outputs where applicable. This is a safe intermediate implementation,
not a claim that these features are implemented. Generated category completeness does
not imply product support. Old GetCurrent/Go/GetBrowserByIds/ToCalendar advertisements
are removed. UI navigation and tabs remain; standard OpenPage migration is mandatory P2 work.

The custom resolver fills a gap in the default category: batch retrieval of the current
visible page by app-owned stable tab ID. Inputs contain 1–50 nonblank IDs, each at most
256 characters. Matched and unresolved lists each preserve input order and duplicates.
A well-formed partial miss is success. Background tabs and Home/Loading/Tabs/hidden
surfaces do not resolve as visible pages. No custom Entity or platform schema edit is used.
Tab Ordinal is the documented app profile **1-based**; authoritative schema does not
specify a base. Blank tabs have a non-null Page sentinel with empty URL and `New tab`
title; they are not a successful GetCurrentPage result.

## Reproduction and distinct verification layers

```sh
python3 Browser/tests/check_action_contracts.py
./Browser/build.sh generate
./Browser/build.sh build
dotnet run --project Browser/tests/Browser.ActionProvider.Tests/Browser.ActionProvider.Tests.csproj
BROWSER_PACKAGE_OUTPUT=/tmp/browser-p1.tpk bash Browser/package.sh
# After explicitly authorized installation, with a real page loaded and WebView focused:
python3 Browser/tests/check_rpc_contracts.py --serial emulator-26101 --output /tmp/browser-p1-rpc
```

Custom generation uses a separate `actionc -i` input and never edits platform action.seq.
Default generation uses the complete category. `generate-custom-bindings.py --output-root`
can put independent custom output in a temporary directory. Package scripts only build/sign;
installation requires separate authorization. TPK output defaults under `/tmp/browser-packages`.
The signer used here is the Tizen Studio emulator test signer, not product signing.

TDD began with raw generation/advertisement checks failing against the old source and
actual-provider test compilation failing on the missing standard API/Entity types.
A target-discovered missing custom schema registration metadata was added to the failing
contract check before fixing the manifest. Raw output equality, slots and registration now pass.
Domain, UseCases, Persistence and App host suites execute separately. Portable producer
suite executes **15 assertions** and prints `PORTABLE COMPLETE`; canonical lifecycle,
legacy structure/data, URL privacy redaction, field and payload bounds are preserved.
The shared immutable tab-query snapshot and hidden resolver behavior have portable coverage.

Actual provider host execution is **unverified in this host environment**, not PASS.
The optional `--providers` mode is deliberately separate and does not catch failures as
success. Tizen.NET.API14 supplies Tizen.Applications.Common only as a compile asset under
`ref/net8.0`, with no runtime/runtimeTargets in assets/deps and no package lib/runtimes.
The host throws FileNotFoundException. No runtime DLL was copied from the target or
fabricated. The reviewer approved replacing this gate with installed provider/Parcel
verification, separately from the two query exceptions below.

Build completed with 0 errors and 227 generated warnings: CS8618 101, CS8604 34,
CS0108 23, CS8625 21, CS8600 21, CS8602 15, CS8601 12. Authored-source warnings: 0.
These concern nullability and generated ToJson member hiding; generated output was not
patched to silence them. The expanded category introduces more generated declarations;
we do not claim a quantitative clean-build baseline comparison against the old commit.
Actual failure-result serialization is covered on target rather than inferred from warnings.

The installed final TPK SHA256 is
`4888f6bd7e960dd4ab31b504b8370ff614d5f09e7f5870d804d0d6616b2c3ab4`.
ZIP integrity, manifest, signatures, Browser assembly and exact custom schema payload were
checked before update installation. The earlier validation-only package exposed a missing
custom registration entry; it was replaced using update install, without uninstalling.

## Explicit target-query exceptions

The reviewer separately approved these P1 exceptions on 2026-09-08:

- GetTabs returns typed failure only if its query has no tab coordinator. Production creates
  and injects that coordinator before starting the listener. Home, loaded page, loading,
  Tabs and renderer lifecycle probes did not reach this defensive branch.
- GetAnnotatedViews has no typed failure branch for normal empty input. Empty Home/Loading/
  Tabs registries are successful empty lists, not failed tests. No artificial failure branch
  or test-only production switch was added.

Success/empty/lifecycle observations and branch analysis replace the meaningful-target-
failure gate **only for these two Actions**. Neither is recorded as a failure-test PASS.
The original RPC report still says the gates were open when it ran; this later decision
resolves that historical status without rewriting the evidence.

## Native UI and Presentation evidence

The existing UI loaded `https://samsungtizenos.com/docs/` via the Tizen Docs shortcut and
`https://www.tizen.org/` in a newly created second tab, then selected the first tab again.
GetCurrentPage/GetTabs/custom resolver confirmed shared state, stable IDs, ordering and
visible-only resolution. Tizen.org's absent page title retained the existing title fallback;
this is not a new title-extraction feature. D-pad Down restored actual page focus after
selection; a pointer click alone did not satisfy the focused-page precondition.

The actual Browser page View had ID `browser:page:tab-1`, EntityType `Tizen.Entity.WebPageInfo`,
and generated ToJson metadata matching the current Entity. Measured bounds were approximately
(40.85,89.92,1838.30,969.10), with positive dimensions and actual focus observation.
Browser Entity→ToPresentation→Show and visible Browser View→View_ToPresentation→Show
preserved the four displayed texts: Browser page, current title, public URL, load summary.
Stable Aurum captures were inspected; animation frames are not final UI evidence. Renderer
Back plus manual Browser foreground restoration was used, not an automatic-return claim.

Renderer integration is **legacy v0.8 compatibility only**. The pre-existing approved
DisplayPresentation TPK has SHA256
`f319a78c4c5573c9040b589fc7ddfc7d495561bf3e23ad8adb177a065dbc3490`;
its source commit is unknown, package API10.0 and dotnet API14 are distinct.
No renderer rebuild, reinstall, source edit or dirty-source dependency was used.
The removed host renderer-parser coupling checked one legacy success tree and four Text
values; installed rendering replaces that success case only. It does not establish parser
error coverage, full renderer regression or canonical protocol conformance.
Canonical v0.9.1 target negotiation/lifecycle/action round trips, high resolutions,
transparent overlay behavior and full UI acceptance remain unverified.

## Evidence and retained state

Run evidence is outside Git in `/tmp/p5-browser-p1`: red/green contract logs, host logs,
package inspection/install records, exact request/response/body JSON, `rpc-page1/report.json`,
final RPC report, native screenshots, process starttime/dlog and crash comparisons.
A failed final-suite attempt due to non-page focus is retained separately; D-pad focus
and the later successful run supply the final evidence. CLI UpdatePageList reports typed
failure plus a diagnostic line; the harness preserves raw output and validates the typed
result rather than treating the extra line as JSON.

Browser was absent before this work. Retained app-owned test state consists of two normal
tabs (Tizen Docs and Tizen.org), selected `tab-1`, the v2 session JSON, and WebView storage
from those public navigations. There was no pre-existing Browser user data to restore.
The first package created only this run's app state before update. Owner-mode inspection
of its private directory was denied; later scoped root reads confirmed the session without
changing it. Other apps and existing source changes were preserved.

The first broad dump comparison had 36 files before and after, no new dump. Process
identity is claimed only over the final cmdline + `/proc/<pid>/stat` starttime observation
around subsequent RPCs, not retroactively from the launcher-reported PID 23847.
Actual Browser.App.dll PID 23846 had the same cmdline and starttime `378643` at
00:53:01 and 00:53:10 +09:00 around 31 subsequent RPC checks. The dump list stayed at
36 entries with no additions. Read-only installed Action DB enumeration exactly matched
the eight manifest advertisements. Root mode
used for read-only process/dump/discovery inspection was restored to SDK user afterward.
