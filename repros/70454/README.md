# Issue #70454 reproduction

Issue: https://github.com/dotnet/aspnetcore/issues/70454

## Scope and acceptance criteria

Reproduce the issue's two endpoint declarations without modifying shipping code.
Exercise both Boolean values through real HTTP requests against Kestrel, with
Request Delegate Generator (RDG) disabled and enabled. Check status, content type,
and JSON shape independently for every response. Success means observing the
reported bug, not fixing it; the executable throws if any response differs.

`Program.cs` contains the issue's endpoint declarations plus a self-checking HTTP
client. The server is stopped and disposed after the checks. `Repro.csproj`
is isolated from repository build imports and uses the installed shared framework.
C# preview is required for the issue's `union` syntax.

## Environment and baseline

- Linux x64.
- Checkout: `90f48c2ea8ab6d5d0fbdcc2862c5e02aa279b44d`, equal to `origin/main`
  at investigation time.
- SDK: `12.0.100-alpha.1.26557.102`, as pinned by the unchanged root `global.json`.
- Microsoft.AspNetCore.App and Microsoft.NETCore.App:
  `12.0.0-alpha.1.26557.102`.
- SDK/shared-framework commit: `76e3c03e6b29f1bbe1aca97d655c560db2320436`.

## Observed HTTP responses

Both reflection and confirmed RDG execution produced these same responses:

| Request | HTTP status | Media type | Body shape |
| --- | --- | --- | --- |
| `/results/false` | 404 | `application/problem+json` | Problem details with `status: 404` |
| `/union/false` | 200 | `application/json` | Wrapper with `problemDetails.status: 404`, `contentType`, and `statusCode: 404` |
| `/results/true` | 200 | `application/json` | `{"id":1,"name":"ann"}` |
| `/union/true` | 200 | `application/json` | `{"value":{"id":1,"name":"ann"},"statusCode":200}` |

Exact failure-case bodies:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404}
```

```json
{"problemDetails":{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404},"contentType":"application/problem+json","statusCode":404}
```

## Execution evidence

After sourcing the repository's `activate.sh`, the reflection execution used
`dotnet run` with `EnableRequestDelegateGenerator=false`. At that point the
compiler-generated files contained no RDG endpoint interceptors.

The RDG execution used `dotnet build --no-incremental` with
`EnableRequestDelegateGenerator=true`, followed by `dotnet run --no-build`.
Forcing recompilation matters when toggling this property: an initial incremental
run reused the reflection build and was not counted as RDG evidence.

The forced build emitted `GeneratedRouteBuilderExtensions.g.cs` with endpoint
interceptors. Its union response path called
`httpContext.Response.WriteAsJsonAsync(result, responseJsonTypeInfo)`. The
`Results<>` path executed the result instead.

Each verified execution checked all four requests, printed
`REPRODUCED: all four HTTP responses match issue #70454.`, and exited with code 0.
The RDG build succeeded with five CA2007 analyzer warnings in the console harness
and no errors.

## Source and history checks

- `a6319aeabf` (#66951) added Minimal API union response coverage for data unions;
  it did not cover a union of `IResult` cases.
- On the checkout baseline, `RequestDelegateFactory` dispatches declared
  `IResult` return types to result execution, otherwise selecting serialization
  for this union shape.
- `src/Shared/RouteHandlers/ExecuteHandlerHelper.cs` tests `obj is IResult`
  before its string/JSON fallbacks; it does not unwrap `IUnion.Value`.

## Evidence limits

This reproduces the HTTP behavior on the repository-pinned SDK's prebuilt shared
framework, not framework assemblies built from checkout `90f48c2ea8`. Current
checkout source and relevant history were inspected separately; execution of that
exact source revision is not claimed. The issue's .NET 11 and rc.3 environments
were not rerun. Endpoint-filter return values and OpenAPI metadata were not
validated. No framework fix or public API change is included.
