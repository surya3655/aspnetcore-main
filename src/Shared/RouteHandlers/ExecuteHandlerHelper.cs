// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Internal;

internal static class ExecuteHandlerHelper
{
    public static Task ExecuteReturnAsync(object obj, HttpContext httpContext, JsonTypeInfo<object> jsonTypeInfo)
    {
        // Terminal built ins
        if (obj is IResult result)
        {
            return result.ExecuteAsync(httpContext);
        }
        else if (obj is string stringValue)
        {
            SetPlaintextContentType(httpContext);
            return httpContext.Response.WriteAsync(stringValue);
        }
        else
        {
            // Otherwise, we JSON serialize when we reach the terminal state
            return WriteJsonResponseAsync(httpContext.Response, obj, jsonTypeInfo);
        }
    }

    public static void SetPlaintextContentType(HttpContext httpContext)
    {
        httpContext.Response.ContentType ??= "text/plain; charset=utf-8";
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The 'JsonSerializer.IsReflectionEnabledByDefault' feature switch, which is set to false by default for trimmed ASP.NET apps, ensures the JsonSerializer doesn't use Reflection.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "See above.")]
    public static Task WriteJsonResponseAsync<T>(HttpResponse response, T? value, JsonTypeInfo<T> jsonTypeInfo)
    {
        if (value is IUnion { Value: IResult result } &&
            jsonTypeInfo.Options.GetTypeInfo(value.GetType()) is { Kind: JsonTypeInfoKind.Union, UnionCases: { Count: > 0 } cases } &&
            cases.All(static unionCase => typeof(IResult).IsAssignableFrom(unionCase.CaseType)))
        {
            return result.ExecuteAsync(response.HttpContext);
        }

        var runtimeType = value?.GetType();

        if (jsonTypeInfo.ShouldUseWith(runtimeType))
        {
            // In this case the polymorphism is not
            // relevant for us and will be handled by STJ, if needed.
            return HttpResponseJsonExtensions.WriteAsJsonAsync(response, value!, jsonTypeInfo, default);
        }

        // Since we don't know the type's polymorphic characteristics
        // our best option is to serialize the value as 'object'.
        // call WriteAsJsonAsync<object>() rather than the declared type
        // and avoid source generators issues.
        // https://github.com/dotnet/aspnetcore/issues/43894
        // https://learn.microsoft.com/dotnet/standard/serialization/system-text-json-polymorphism
        return response.WriteAsJsonAsync<object?>(value, jsonTypeInfo.Options);
    }
}
