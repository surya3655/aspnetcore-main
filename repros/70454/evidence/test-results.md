# Issue #70454 validation results

## Summary

| Check | Result |
| --- | --- |
| New regression suite on unfixed default-branch source | 48 expected failures, 8 passing controls, 0 skipped |
| Same regression rows on fixed source | 56 passed, 0 failed, 0 skipped |
| Full `Microsoft.AspNetCore.Http.Extensions.Tests` project on fixed source | 2,395 passed, 0 failed, 0 skipped; test duration 4m 9s |
| Original reproduction over Kestrel/HTTP, source-built RDF | All four responses and union case metadata passed; exit 0 |
| Original reproduction over Kestrel/HTTP, source-built RDG and runtime | All four responses and union case metadata passed; exit 0 |
| Chromium before/after capture | Before: responses differ from controls. After: both union responses exactly match controls. |
| Runtime assembly build | Succeeded, 0 warnings, 0 errors |
| Generator build | Succeeded, 0 warnings, 0 errors |
| `node --check` for browser capture script | Passed |
| `git diff --check` | Passed |

## Red/green method and row inspection

Baseline shipping source was `90f48c2ea8ab6d5d0fbdcc2862c5e02aa279b44d`, which
matched `origin/main` when the investigation began. Nine changed shipping source
files were saved, temporarily replaced with their unfixed baseline contents, and
then restored. The new tests were identical in the red and green runs.

Every TRX row was inspected and matched by full test name, including parameter
values. All 48 red failures reached the intended observable assertion:

- 36 direct/async/nullable/object/filter result-case rows: wrong status for the
  problem case, or wrapper JSON rather than DTO JSON for the success case.
- 8 metadata rows: inferred wrapper metadata instead of both case declarations.
- 2 mixed-union result rows: wrapper JSON rather than DTO JSON.
- 2 filter short-circuit rows: 200 instead of 404.

The 8 controls already passed without the fix: nullable absence/default union
serialization, data-valued mixed unions, and filter result replacement. All 56 rows
passed with the fix. The final full-project run includes all of them.

`regression-results.json` contains every row's red/green outcome and the actual red
assertion text. There were no compilation/setup failures counted as red evidence.

## Executed test scope

The focused and full runs used the repository's activated .NET environment and
this project:

`src/Http/Http.Extensions/test/Microsoft.AspNetCore.Http.Extensions.Tests.csproj`

Common build switches were `--no-restore -p:BuildNodeJS=false
-p:IncludeComponentsWebAssets=false`. The focused filter was
`FullyQualifiedName~ReturnsUnionOfIResultCases|FullyQualifiedName~ReturnsMixedResultUnion`.
The final full run had no test filter. TRX logging was enabled in each run.

The common build switches exclude unrelated generated Components JavaScript
assets from this API test graph; they do not bypass the endpoint/compiler/response
assertions. Earlier unrestricted build attempts stopped before tests because the
required targeting pack and generated Blazor assets were absent. The restored
runtime targeting pack was installed, and the supported asset-exclusion switches
were then used. No repository build/configuration files or package versions changed.

The new tests exercise both `RuntimeCreationTests` and `CompileTimeCreationTests`.
The latter verifies generated endpoint metadata, so silently falling back to RDF
does not count as RDG coverage. The original standalone reproduction was also
rebuilt with the local analyzer explicitly selected, and generated endpoint
interceptors were confirmed. It uses a union declared in application source,
whereas the shared regression types come from the test assembly.

The 53 existing generated-code baseline updates contain only the identical
result-valued-union dispatch check in the emitted JSON response helper; the full
suite verifies the snapshots.

## Browser and HTTP evidence

`before.png` and `after.png` were captured using Chromium against source-built
unfixed/fixed runtime assemblies, with the same reflection-delegate reproduction
and the same installed runtime environment. The capture script supplies an HTML
viewer page, not mocked API responses: the page fetches all four real endpoints
and displays their actual status, content type, and body.

The JSON captures record `matches: false` before and `matches: true` after. The
fixed union failure is 404 `application/problem+json`; the fixed union success is
200 with `{"id":1,"name":"ann"}`. Both match the `Results<>` controls.

The standalone fixed-behavior check additionally verifies that the union's static
200 response metadata describes `UserDto`, not the union wrapper. It intentionally
does not expect a static 404 declaration from `ProblemHttpResult`, whose status
code is supplied dynamically.

## Environment and limits

- Linux x64.
- Unchanged pinned SDK: `12.0.100-alpha.1.26557.102`.
- Repository runtime reference pack / core runtime: `12.0.0-alpha.1.26569.107`.
- SDK ASP.NET Core shared framework: `12.0.0-alpha.1.26557.102`; the relevant runtime
  assembly and RDG analyzer were replaced by the actual repository build outputs
  for final HTTP validation. Assembly location was checked in host output.
- The standalone console harness emits six CA2007 analyzer warnings; its builds
  have no errors. Shipping runtime and generator builds have no warnings/errors.
- Full OpenAPI document rendering, .NET 11/rc.3, trimming, Native AOT, performance,
  and recursive discovery of results inside nested union cases were not validated.
  Existing JSON type-info setup is retained.
- No new public/protected signatures are added. The changed return-value convention
  still requires maintainer review. `../api-proposal.md` is ready to file; no separate
  API proposal issue was filed because a native GitHub issue-creation tool is not
  available in this session. No API approval is claimed.
