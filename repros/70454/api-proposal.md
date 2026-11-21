# [API Proposal] Execute active IResult cases returned in C# unions

Ready-to-file convention proposal for `dotnet/aspnetcore`.

- Labels: `api-suggestion`, `api-proposal`
- Issue type: Feature, if available
- Champion: not assigned; a maintainer must champion the review
- Status: not filed; this session has no native GitHub issue-creation tool
- Originating issue: https://github.com/dotnet/aspnetcore/issues/70454
- Implementation: the implementation draft PR containing this document

## Evidence checklist

- [x] Originating issue and its comments inspected.
- [x] Implementation source and diff inspected.
- [x] Public/protected signature changes inventoried: none.
- [x] Observable convention changes described below.
- [ ] Champion assigned and API review scheduled.

## Background and Motivation

Minimal API handlers can return C# unions of data types. When a union's active
case is an `IResult`, the current framework serializes that result as data rather
than executing it. Issue #70454 demonstrates a `ProblemHttpResult` carrying status
404 being serialized in an HTTP 200 response, and an `Ok<T>` wrapper being
serialized instead of its DTO. This proposal gives application authors execution
behavior analogous to `Results<T1, T2>` without adding another public wrapper.
The target is ordinary Minimal API applications using RDF or RDG.

## Usage Examples

```csharp
using Microsoft.AspNetCore.Http.HttpResults;

app.MapGet("/union/{ok:bool}", (bool ok) => ok
    ? (Outcome)TypedResults.Ok(new UserDto(1, "ann"))
    : TypedResults.Problem(statusCode: 404));

app.MapGet("/async-union/{ok:bool}", async Task<Outcome> (bool ok) =>
{
    await Task.Yield();
    return ok
        ? TypedResults.Ok(new UserDto(1, "ann"))
        : TypedResults.Problem(statusCode: 404);
});

public record UserDto(int Id, string Name);
public union Outcome(Ok<UserDto>, ProblemHttpResult);
```

The false case executes a 404 problem response; the true case writes only the DTO.

## Proposed API

```diff
// No public or protected declarations are added, removed, or changed.
// No PublicAPI.Unshipped.txt additions are required.
```

Proposed Minimal API return-value convention:

1. At the top-level response-writing boundary, an `IUnion` whose active `Value`
   implements `IResult` executes that result. Existing direct `IResult` dispatch
   retains precedence.
2. This applies to synchronous, `Task<T>`, and `ValueTask<T>` handlers, including
   boxed object returns and the final value returned by an endpoint filter.
3. Filters still receive the handler's union value and may replace or short-circuit
   it. The framework does not replace the union with an adapter before filters run.
4. Non-result active cases retain union JSON serialization. Nested unions in DTOs
   or `Ok<T>` payloads are not recursively treated as endpoint results. Nullable
   absence and default union values retain existing null serialization semantics.
5. For a compiler-marked union whose declared cases all implement `IResult`, infer
   endpoint metadata from the case types' existing `IEndpointMetadataProvider`
   implementations, rather than inferring one wrapper-JSON 200 response. A union's
   own metadata provider takes precedence. Mixed/data unions retain existing
   metadata behavior.
6. Metadata has the same limits as `Results<>`: a dynamic status code supplied to
   `TypedResults.Problem(statusCode: 404)` cannot be inferred from the result type.
   This proposal does not introduce static 404 metadata for `ProblemHttpResult`.

## Alternative Designs

- Continue using `Results<Ok<UserDto>, ProblemHttpResult>` as in the issue's control.
  This already executes correctly but does not make the C# union shape equivalent.
- Warn through an analyzer or reject the endpoint at startup. The reporter explicitly
  identifies these as preferable to silently sending the wrong status. They do not
  provide the requested execution behavior.
- Unwrap every union before ordinary return dispatch. The implementation deliberately
  does not do this: data-union string cases must remain JSON strings rather than
  becoming plaintext responses.

## Risks

- This changes previously observable serialization for top-level result-valued
  unions, including mixed unions when their active case is an `IResult`.
- Runtime case-metadata discovery currently depends on the compiler's public
  single-argument case constructors and union marker. Review should confirm that
  representation as an appropriate runtime contract.
- RDF and RDG must remain consistent for async handlers, filters, nullable values,
  and metadata-provider ordering.
- This proposal does not establish API approval. A champion must complete review,
  and `api-approved` must cover the final convention before an RTM release.

## Source justifications

- Incorrect HTTP status: "A 404 problem goes out as a 200, with no error and no warning."
  — originating issue.
- Result execution and metadata parity: "The same behavior as
  `Results<Ok<UserDto>, ProblemHttpResult>`: the active case is executed" — originating issue.
- Diagnostic alternative: "an analyzer warning or a startup error would be much better"
  — originating issue.
- Runtime execution boundary: `if (value is IUnion { Value: IResult result })` and
  `return result.ExecuteAsync(response.HttpContext);` — implementation in
  `ExecuteHandlerHelper.WriteJsonResponseAsync` and RDF's fast response writer.
- Preservation of data serialization: the existing JSON-writing branches remain
  after the new result-valued-union check — implementation diff.
- All-result metadata classification: `TryGetUnionResultCaseTypes` rejects a case
  when `!typeof(IResult).IsAssignableFrom(caseType)` — implementation diff.
