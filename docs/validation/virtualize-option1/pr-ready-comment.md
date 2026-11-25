## Summary

Merged current main (`d348398995`) without rewriting published history and resolved the Virtualize C#/JS conflicts, preserving both main's loading/native-anchoring handling and the PR's existing End recalibration correction.

Implemented Option 1: the rendered/stale-spacer divisor is selected only when the End flag is present. Start/None use the current estimate; viewport capacity remains measured. No new correction mechanism or public API was added.

## Validation

- Builds: sample, Web unit project, and dependency-aware Selenium project passed with 0 warnings/errors; Debug and Release JS builds passed.
- Managed Virtualize: 63/63 passed. Nine targeted cases failed for the expected request/offset reason without Option 1; the identical main-parity subset also passed 9/9 against latest main.
- Existing JS export test: 1/1 passed.
- Permanent browser suite: 7/7 passed (original End controls on WebAssembly/Server; Server Start/None and all three async-provider anchor modes).
- Canonical Server browser matrix: 6/6 passed. Start/None fixed-item repetitions changed from 10/10 failures each to 10/10 passes each. Both viewport edges, subsequent wheel input, bounded rows/requests, and observer cleanup were checked.

Non-End browser assertions deliberately allow index-space rescaling: only End applies the correction that preserves estimated item position. Earlier attempts that applied End's near-tail bound to other modes were discarded as invalid regression evidence.

## Investigation / remaining boundaries

Confirmed in 10/10 controlled real-Server runs each: an older spacer sample can be consumed after a newer estimate is rendered, and a real same-batch parent rerender can remove the recalibration marker before JS reads it. These are unchanged End-path limitations; this scoped change does not add a mechanism to solve them.

A separate nine-append WebAssembly None/fixed blank-viewport case reproduces on exact main and on this branch. It is not claimed fixed. Async outcomes from the early over-constrained all-mode assertion are not classified as regressions without valid geometry evidence.

Complete patches, modified-file manifests, before/after screenshots, raw scoped test results, and bounded investigation evidence are included in the validation bundle. No claim is made that every Virtualize/platform/timing combination is regression-free.
