# Virtualize Option 1: conflict resolution and validation

Status: complete for the requested scoped change. All selected builds/tests passed; wider limitations and discarded invalid assertions are explicitly retained below.

## Scope and history

- Original PR: #70692, original head `4459644f7a4a56ffd12069ef399c7498197799b6`.
- Latest upstream main: `d3483989953ae3cccd773aea5ea04cb0c7198531` (successfully rechecked earlier during validation). The Git transport credential expired during the final recheck; a standard read-only public GitHub API request then confirmed that this is still the main tip.
- Integration commit: `827fbd1a41ca74fc0818d2adb56bf103b6f84984`.
- Implementation/test commit: `1320e91217`.
- Main was **merged, not rebased**. Published history was preserved because this workspace prohibits rewriting published commits and force-pushing.
- No public API, package, SDK, or NuGet configuration changes were authored. No additional correction mechanism was introduced.

The requested runtime matrix is Interactive Server, End/Start/None, fixed items and asynchronous `ItemsProvider`, including a non-zero leading spacer. Additional WebAssembly exploration is reported separately below; it is not silently treated as passing.

## Conflict resolution

Two files conflicted:

1. `src/Components/Web/src/Virtualization/Virtualize.cs`: retain main's loading attribute and the PR's recalibration payload. Leading-element sequences are loading attribute 3, recalibration attribute 4, and reference capture 5. Sibling regions retain their original sequences.
2. `src/Components/Web.JS/src/Virtualize.ts`: retain main's native-anchoring suppression in both observer and pending-mutation flush paths, while passing mutation records to the existing recalibration correction. The leading spacer observes both the loading and recalibration attributes. Main's shared-scroll-container anchoring ownership and cleanup remain intact.

The other incoming main changes are not manual conflict-resolution edits. The integration file manifest and complete integration/task patch identify them separately.

## Option 1 implementation

Only the divisor selector changes in product behavior:

```csharp
var spacerItemSize = GetEffectiveItemSize((AnchorMode & VirtualizeAnchorMode.End) != 0 ? _lastRenderedItemSize : GetItemHeight());
```

- End-bearing modes retain the previous rendered estimate for spacer interpretation.
- Start and None use the current estimate, restoring main's behavior.
- Viewport capacity still uses the current measured estimate.
- End's existing render-scoped recalibration payload, eligibility guards, JavaScript delta, and scroll-ignore behavior are unchanged.

## Authored source/test files

- `src/Components/Web.JS/src/Virtualize.ts`
- `src/Components/Web/src/Virtualization/Virtualize.cs`
- `src/Components/Web/test/Virtualization/VirtualizeTest.cs`
- `src/Components/test/E2ETest/ServerExecutionTests/TestSubclasses.cs`
- `src/Components/test/E2ETest/Tests/VirtualizationTest.cs`
- `src/Components/test/testassets/BasicTestApp/VirtualizationAppendAndScroll.razor`

Temporary canonical sample pages and diagnostic hooks were removed after permanent validation passed. Evidence files are separate from the implementation patch.

## Managed tests and base-tip async comparison

All 63 Virtualize unit cases passed with Option 1. Without the selector fix, 54 passed and nine failed for the expected reason:

- Eight Start/None rows: before/after spacer, increasing/decreasing estimate, initial leading offset 100. Every row failed its expected offset assertion without the fix and passed with it.
- The gated async-provider test: wrong request start without the fix (`99` instead of `42`). With the fix, the request is `42/35`; a later `37/35` request cancels it; stale completion does not render; the active completion renders exactly items 37–71. Loading attributes are present while pending and removed after completion. There are exactly four provider requests.
- All four End controls passed before and after. They retain the rendered divisor, expected capacity, before-spacer `50:25` or `50:100` payload, and one-shot attribute removal on a subsequent render.

The identical eight Start/None rows and async-provider test were also run against latest main's actual `Virtualize.cs`: **9/9 passed**. This establishes parity for the managed request/loading/cancellation contract, not universal browser scheduling parity.

The existing JavaScript Virtualize export-contract test passed (1/1). It does not establish layout or observer timing correctness.

### Permanent test ownership

- Managed owner: Virtualize distribution, rendering, requests, cancellation, and marker state. Surface: `Web/test/Virtualization/VirtualizeTest.cs`.
- Browser owner: native observers, geometry, wheel input, interop, rendering, and final viewport coverage. Surface: existing Components C# Selenium tests and `VirtualizationAppendAndScroll` asset.
- The original nine-append End browser regression remains a WebAssembly and Server control. New Server Start/None and async rows use a single ten-item bulk append, translating the validated canonical reproduction without changing the original End case. This keeps the new regression separate from the pre-existing nine-append WebAssembly behavior characterized below. The test viewport is explicitly large enough for the full scroll container.
- Injected managed callbacks prove the response to samples, not browser reachability. Real browser evidence below supplies the latter for the requested matrix.

## Canonical browser validation

A temporary per-page Interactive Server sample used 10,000 variable-height rows (120/240 pixels), overscan 5, fixed items or an asynchronously yielding provider. The scenario used real wheel input into the middle, append ten, jump to the new tail, then wheel upward by 6,000 pixels. Assertions checked nearby earlier rows and both viewport edges, then another user wheel, bounded row count, and observer release after navigation. Synchronization used observable state and bounded polling, not fixed synchronization sleeps.

| Mode/source | Before Option 1 | After Option 1 |
|---|---|---|
| None / fixed | Fail: visible tail remained 10008–10010 | Pass: earlier rows 9947–9950 |
| Start / fixed | Fail: visible tail remained 10008–10010 | Pass: earlier rows 9947–9950 |
| End / fixed | Pass | Pass, same observed End result |
| None / async provider | Pass | Pass |
| Start / async provider | Pass | Pass |
| End / async provider | Pass | Pass, same provider request count |

- Bounded repeats: Start/fixed **10/10 failures → 10/10 passes**; None/fixed **10/10 failures → 10/10 passes**.
- All six after-change cases passed both-edge coverage, subsequent wheel, row bound, and observer cleanup checks. Each final window contained 14 rows.
- End/provider used eight requests both before and after. Start/None provider cases used six after versus eight before in this scenario. These are bounded scenario measurements, not performance benchmarks.
- The original paired matrix/repeats used Chromium 131 on Server, where the runtime runs in .NET rather than WebAssembly. A Chrome 158 Server None/fixed control also passed.
- No page/console errors or failed requests were observed in the canonical six-case matrix.

## Investigation: older sample with a newer estimate

**Confirmed at the real Interactive Server boundary, 10/10 controlled runs.**

Two genuine before-spacer reports were produced through real wheel/IntersectionObserver processing while the rendered estimate was 144. The gate retained their original arguments and delivered them in capture order, modelling transport/processing delay without reordering or fabricating reports. After delivery of the first report, the rendered DOM estimate was 144.82759. The second report still contained the 144-era spacer sample and was delivered only after that newer render.

`_lastRenderedItemSize` advances during `BuildRenderTree`, not with acknowledgement or sample identity. The protocol carries no estimate/generation with the sample. Thus dispatcher serialization does not prevent this temporal mismatch. The observation proves association reachability under the controlled delay, not its natural frequency or a universal persistent blank-viewport effect. Option 1 leaves this pre-existing End behavior unchanged.

## Investigation: marker removed in the same batch

**Confirmed through a real .NET render producer, 10/10 controlled runs.**

A one-shot notification from a newly created row rerendered its parent while the render batch was still being assembled. The actual framework renderer set the recalibration pair and removed it before the JavaScript observer read the DOM. Representative sequence:

1. Set `144:144.82759`.
2. Remove the marker in the same batch.
3. MutationObserver delivers both records, each with the live marker value `null`.

Production does not request `attributeOldValue` and reads the current attribute, so the first pair is unavailable to its correction consumer. A standalone native-DOM probe also confirmed the microtask mechanism, but the real-row producer is the stronger evidence. This proves payload loss at that boundary; another anchoring/render cycle can mask the final viewport effect. No new marker or correction mechanism was added to address it.

## Wider WebAssembly characterization

The initially installed Chromium 131 could not start this WebAssembly runtime: the runtime reported missing WASM exception-handling support. Chrome for Testing 158.0.8127.36 and its matching driver resolved that prerequisite.

Expanded WebAssembly non-End validation then found a real blank-viewport case. The served WebCIL module identity matched the built DLL; this was not a stale-input inference. The actual fixture was reproduced manually, with both a full and clipped viewport. Exact latest-main C# **and** JavaScript were separately built and compared:

- None/fixed blank viewport: observed on main and Option 1.
- Start/fixed blank viewport: observed on main; additional tip coverage is not claimed.
- End/fixed blank viewport: observed on main; the PR's retained End fix passes its original regression control.
- Early all-mode Selenium assertions also applied an End-specific near-tail index bound to None/async. Those failures are **not valid proof of an async regression**: Start/None intentionally have no estimate-position correction. The manually driven async page passed on main at both tested viewport sizes. End/async also failed the pre-PR main assertion; tip WebAssembly End/async is not claimed as validated. Exploratory runs were stopped without complete suite summaries; only recorded cases are reported.

The new permanent tests do not claim these wider non-End WebAssembly cases are fixed. They cover the requested Server regression, retain original End coverage, and preserve the main-equivalent managed Start/None behavior. Adding a separate correction to address this wider existing boundary is outside Option 1's explicit scope.

## Test-contract correction during validation

An early generalization incorrectly applied End's `firstTailIndex - 60` lower bound to Start/None. That would require a correction mechanism these modes explicitly do not have. A real Server bulk-append observation showed item 9850 visible after the wheel, with both edges covered, while the old tail began at 9997. This is permitted index-space rescaling, not a failure to load earlier items.

The final tests retain the original near-tail bound **only for End**. Start/None require valid earlier real rows and both-edge coverage. Failures against the discarded all-mode near-tail bound are not cited as valid regression proof. The selector's strict causal regression proof is the nine managed rows (each fails without Option 1 and passes with it); browser evidence independently exercises the real producer and observable. The canonical before-change Start/None failures were unchanged tail indices, so they still fail the corrected earlier-items contract without any near-tail bound.

The final seven-row browser matrix passed with these contract-correct assertions. Original End assertions remain equivalent, and the existing End bug was reproduced on exact main before the PR fix.

## Requested regression audit

| Concern | Evidence / limit |
|---|---|
| Anchoring | End controls and Server mode matrix; existing prepend/append managed tests passed. Wider WebAssembly findings above are not hidden. |
| Observer cleanup | All six canonical cases released observed item/spacer targets after navigation; main's disposal and anchoring restoration retained. |
| Invalid estimates | Existing zero/negative/NaN/infinity Virtualize tests passed. Existing validation/fallback behavior was not broadened. |
| Sequence numbers | Both merge inputs retained with distinct leading-element frames; tree and real-browser render assertions passed at the requested boundary. |
| Stale scroll flags | Subsequent real user wheel advanced earlier rows in all six after-change cases; no new flag introduced. Not an exhaustive scroll-source stress test. |
| Attribute removal | End marker one-shot removal and async loading-marker removal asserted; same-batch consumption limitation separately confirmed. |
| Threading | Renderer-dispatched gated provider/cancellation test; no new threading/dispatch mechanism. No cross-thread stress benchmark claimed. |
| Performance | Constant-time selector, no new allocations/observers/interop; bounded row/request measurements above. No universal performance claim. |
| Virtualization requests | Exact managed request starts/counts/cancellation and bounded browser provider counts. |

## Build and execution record

- Repository-local SDK activated before direct .NET commands: `12.0.100-alpha.1.26557.102`.
- Submodules initialized at the fetched main revisions.
- Root npm dependency install and workspace builds passed; tracked package manifests/lockfiles remained unchanged.
- Canonical sample dependency-aware build: 0 warnings, 0 errors.
- Web unit project dependency-aware build: 0 warnings, 0 errors.
- Selenium project dependency-aware build: 0 warnings, 0 errors.
- Exact-main comparison C#/JS and Selenium app build: 0 warnings, 0 errors.
- Final contract-correct Selenium matrix: **7/7 passed**, including the original End Fact in WebAssembly and Server, Server Start/None fixed-item rows, and Server End/Start/None async-provider rows.

Setup attempts are not represented as passing builds: the all-project restore was cancelled in favor of focused supported builds; an initial multi-project argument error and missing root JS tooling/assets were corrected. The full repository build and unrelated platform/lifecycle matrices were not run.

## Deliverables and patch bases

Patches contain the complete implementation/test changes (evidence files are separate to avoid a self-referential patch). Each patch passed `git apply --cached --check` against an isolated index populated from its stated base.

| File | Base | Contents |
|---|---|---|
| `complete-task.patch` | original PR head `4459644f7a` | All imported main changes, conflict resolution, Option 1, and tests |
| `pr.patch` | latest main `d348398995` | Complete cumulative PR implementation/test diff |
| `option1.patch` | resolved merge `827fbd1a41` | Only Option 1 and its test changes |

`implementation-files.txt` lists every changed implementation path relative to the original PR head. `main-integration-files.txt` identifies imported main changes. `modified-files.txt` also includes the evidence bundle. `SHA256SUMS` covers every delivered evidence file other than itself.

### Before/after screenshots

- `screenshots/before-start-fixed.png` / `after-start-fixed.png`
- `screenshots/before-none-fixed.png` / `after-none-fixed.png`
- `screenshots/before-end-fixed.png` / `after-end-fixed.png` (preserved End behavior)
- Separately labelled baseline/tip WebAssembly existing-failure screenshots

The paired Server images use the same temporary sample/input. Start/None stayed at tail rows before Option 1 and show earlier covered rows after it. They do not claim to depict a new shipping UI.

### Results and reviewer comment

`results/` contains managed red/green and latest-main parity logs, JS results, the final seven-case Selenium log, browser matrices/repeats, representative timing records and all 20 timing-run summaries, and explicitly qualified wider characterization records. `results/build-results.md` records build outcomes and scope limits.

`pr-ready-comment.md` is ready to paste as a summary, not a merge approval. No pull request has been published from this session; the original remote PR has not been updated. The work uses a main merge rather than a history-rewriting rebase.

