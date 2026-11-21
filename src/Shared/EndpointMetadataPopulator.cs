// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Internal;

#nullable enable

namespace Microsoft.AspNetCore.Http;

[RequiresUnreferencedCode("This API performs reflection on types that can't be statically analyzed.")]
[RequiresDynamicCode("This API performs reflection on types that can't be statically analyzed.")]
internal static class EndpointMetadataPopulator
{
    private static readonly MethodInfo PopulateMetadataForParameterMethod = typeof(EndpointMetadataPopulator).GetMethod(nameof(PopulateMetadataForParameter), BindingFlags.NonPublic | BindingFlags.Static)!;
    internal static readonly MethodInfo PopulateMetadataForEndpointMethod = typeof(EndpointMetadataPopulator).GetMethod(nameof(PopulateMetadataForEndpoint), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static void PopulateMetadata(MethodInfo methodInfo, EndpointBuilder builder, IEnumerable<ParameterInfo>? parameters = null, bool populateUnionCaseMetadata = false)
    {
        object?[]? invokeArgs = null;
        parameters ??= methodInfo.GetParameters();

        // Get metadata from parameter types
        foreach (var parameter in parameters)
        {
            if (typeof(IEndpointParameterMetadataProvider).IsAssignableFrom(parameter.ParameterType))
            {
                // Parameter type implements IEndpointParameterMetadataProvider
                invokeArgs ??= new object[2];
                invokeArgs[0] = parameter;
                invokeArgs[1] = builder;
                PopulateMetadataForParameterMethod.MakeGenericMethod(parameter.ParameterType).Invoke(null, invokeArgs);
            }

            if (typeof(IEndpointMetadataProvider).IsAssignableFrom(parameter.ParameterType))
            {
                // Parameter type implements IEndpointMetadataProvider
                invokeArgs ??= new object[2];
                invokeArgs[0] = methodInfo;
                invokeArgs[1] = builder;
                PopulateMetadataForEndpointMethod.MakeGenericMethod(parameter.ParameterType).Invoke(null, invokeArgs);
            }
        }

        // Get metadata from return type
        var returnType = methodInfo.ReturnType;
        if (CoercedAwaitableInfo.IsTypeAwaitable(returnType, out var coercedAwaitableInfo))
        {
            returnType = coercedAwaitableInfo.AwaitableInfo.ResultType;
        }

        if (returnType is not null && typeof(IEndpointMetadataProvider).IsAssignableFrom(returnType))
        {
            // Return type implements IEndpointMetadataProvider
            invokeArgs ??= new object[2];
            invokeArgs[0] = methodInfo;
            invokeArgs[1] = builder;
            PopulateMetadataForEndpointMethod.MakeGenericMethod(returnType).Invoke(null, invokeArgs);
        }
        else if (populateUnionCaseMetadata && returnType is not null && TryGetUnionResultCaseTypes(returnType, out var unionCaseTypes))
        {
            foreach (var unionCaseType in unionCaseTypes)
            {
                if (typeof(IEndpointMetadataProvider).IsAssignableFrom(unionCaseType))
                {
                    // Union case type implements IEndpointMetadataProvider
                    invokeArgs ??= new object[2];
                    invokeArgs[0] = methodInfo;
                    invokeArgs[1] = builder;
                    PopulateMetadataForEndpointMethod.MakeGenericMethod(unionCaseType).Invoke(null, invokeArgs);
                }
            }
        }
    }

    private static void PopulateMetadataForParameter<T>(ParameterInfo parameter, EndpointBuilder builder)
        where T : IEndpointParameterMetadataProvider
    {
        T.PopulateMetadata(parameter, builder);
    }

    private static void PopulateMetadataForEndpoint<T>(MethodInfo method, EndpointBuilder builder)
        where T : IEndpointMetadataProvider
    {
        T.PopulateMetadata(method, builder);
    }

    // Union cases are identified from the compiler-emitted public single-argument constructors.
    internal static bool TryGetUnionResultCaseTypes(Type returnType, [NotNullWhen(true)] out List<Type>? caseTypes)
    {
        caseTypes = null;
        var unionType = Nullable.GetUnderlyingType(returnType) ?? returnType;

        if (!typeof(IUnion).IsAssignableFrom(unionType) ||
            !unionType.IsDefined(typeof(UnionAttribute), inherit: false))
        {
            return false;
        }

        var caseTypesList = new List<Type>();
        foreach (var constructor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            var parameters = constructor.GetParameters();
            if (parameters.Length == 1)
            {
                caseTypesList.Add(parameters[0].ParameterType);
            }
        }

        if (caseTypesList.Count == 0)
        {
            return false;
        }

        foreach (var caseType in caseTypesList)
        {
            if (!typeof(IResult).IsAssignableFrom(caseType))
            {
                // Mixed or non-result union. Retain existing metadata behavior.
                return false;
            }
        }

        caseTypes = caseTypesList;
        return true;
    }
}
