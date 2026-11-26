# Issue #70454: reproduction and fix evidence

Issue: https://github.com/dotnet/aspnetcore/issues/70454

## Acceptance criteria

- Execute a top-level union's active `IResult` instead of serializing the result wrapper.
- Match `Results<Ok<UserDto>, ProblemHttpResult>` for both Boolean values, including
  HTTP status, content type, and body.
- Cover RDF and RDG, synchronous and asynchronous handlers, nullable/boxed values,
  and endpoint filters.
- Propagate existing case-provider metadata for all-result unions without an
  inferred wrapper-JSON 200 response. Do not invent a static 404 declaration for
  `ProblemHttpResult`'s dynamically supplied status code.
- Preserve existing null and data-union JSON behavior. Keep MVC's separate
  return-value conventions unchanged.
- Demonstrate each regression row failing for the expected reason on unfixed source
  and passing on fixed source; retain passing non-regression controls.

## Artifacts

- `Program.cs`: the issue's endpoint declarations plus a self-checking HTTP client.
  The default mode checks the original bug; `--expect-fixed` checks corrected HTTP
  responses and case metadata. `--serve` keeps the host available for browser capture.
- `Repro.csproj`: isolated from repository build imports. `UseRepositoryAssembly`
  selects the locally built runtime assembly; `UseRepositoryGenerator` selects the
  locally built generator. Validation disabled automatic SDK RDG when explicitly
  selecting the local analyzer.
- `capture.cjs`: a Chromium evidence viewer. Playwright supplies only the viewer
  page; its `fetch` requests reach the real Kestrel endpoints. Response status,
  content type, and body are not mocked. The viewer is not a shipping application UI.
- `evidence/before.png` and `evidence/after.png`: browser captures against the unfixed
  baseline source and fixed source respectively, using reflection delegates.
- `evidence/before-responses.json` and `evidence/after-responses.json`: the browser's
  captured HTTP response data.
- `evidence/test-results.md` and `evidence/regression-results.json`: validation
  summary and per-row red/green results.
- `api-proposal.md`: ready-to-file proposal for review of the return-value convention;
  no public or protected signatures are added.

## Observed HTTP behavior

| Request | Unfixed source | Fixed source |
| --- | --- | --- |
| `/results/false` | 404 problem details | 404 problem details |
| `/union/false` | 200 JSON result wrapper | 404 problem details |
| `/results/true` | 200 DTO JSON | 200 DTO JSON |
| `/union/true` | 200 `Ok<T>` wrapper JSON | 200 DTO JSON |

Before, the union failure body was:

```json
{"problemDetails":{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404},"contentType":"application/problem+json","statusCode":404}
```

After, it matches the `Results<>` control:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404}
```

## Baseline, history, and validation boundaries

The investigation began at `90f48c2ea8ab6d5d0fbdcc2862c5e02aa279b44d`, equal to
`origin/main` at investigation time. Initial reproduction used the pinned SDK's
prebuilt shared framework. Subsequent red/green validation built the actual
unfixed/fixed repository source, keeping the new tests identical in both runs.
The final browser captures also use those source-built runtime assemblies.

The earlier union-support change, `a6319aeabf` (#66951), covered data unions rather
than result-valued unions. RDF and the generated writer both previously selected
JSON serialization for the reported shape.

The API-level evidence is the real producer/pipeline boundary, and the browser
captures additionally demonstrate the Kestrel/HTTP boundary. Full OpenAPI document
rendering, .NET 11/rc.3, trimming, and Native AOT are not claimed as verified.
Execution recognizes an active top-level `IResult`; it does not recursively discover
results inside DTOs or nested union cases. Existing JSON type-info setup is retained.
