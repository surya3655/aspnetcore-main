# Executed builds

All direct .NET commands were run after repository activation. Root npm install and workspace builds succeeded; Debug and Release Blazor bundles were generated.

## Canonical sample dependency-aware build

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:02:03.45
```

## Web unit project dependency-aware build

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:37.71
```

## Initial Selenium dependency-aware build

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:03:04.82
```

## Exact-main C#/JS comparison build

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:02:14.25
```

## Final contract-correct Selenium project build

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:36.10
```

## Clean sample after temporary pages removed

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:12.14
```

## Scope and setup limits

The all-project restore was cancelled in favor of supported focused builds; a full repository build was not claimed. Initial argument/missing-JS-tooling/assets errors were corrected. Chromium 131 could not start WebAssembly; Chrome 158 with the matching driver was used for final Selenium validation. Diagnostic scratch-tool failures are not product build results.

Representative executed project commands were `dotnet build <project> --no-restore -v:q -p:UseIisNativeAssets=false` and `dotnet test <project> --no-build --no-restore --filter <selected cases>`. Builds were dependency-aware before no-build test loops.
