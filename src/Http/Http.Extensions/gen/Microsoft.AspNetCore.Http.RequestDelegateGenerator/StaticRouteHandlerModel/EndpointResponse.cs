// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Analyzers.RouteEmbeddedLanguage.Infrastructure;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.AspNetCore.Http.RequestDelegateGenerator.StaticRouteHandlerModel.Emitters;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Http.RequestDelegateGenerator.StaticRouteHandlerModel;

using WellKnownType = WellKnownTypeData.WellKnownType;

internal class EndpointResponse
{
    public ITypeSymbol? ResponseType { get; set; }
    public ITypeSymbol WrappedResponseType { get; set; }
    public string WrappedResponseTypeDisplayName { get; set; }
    public string? ContentType { get; set; }
    public bool IsAwaitable { get; set; }
    public bool HasNoResponse { get; set; }
    public bool IsIResult { get; set; }
    public bool IsSerializable { get; set; }
    public bool IsEndpointMetadataProvider { get; set; }
    public bool IsUnion { get; set; }
    public bool IsUnionResult { get; set; }
    public List<ITypeSymbol>? UnionResultCaseMetadataProviderTypes { get; set; }
    private WellKnownTypes WellKnownTypes { get; init; }

    internal EndpointResponse(IMethodSymbol method, WellKnownTypes wellKnownTypes)
    {
        WellKnownTypes = wellKnownTypes;
        ResponseType = UnwrapResponseType(method, out bool isAwaitable, out bool awaitableIsVoid);
        WrappedResponseType = method.ReturnType;
        WrappedResponseTypeDisplayName = method.ReturnType.ToDisplayString(EmitterConstants.DisplayFormat);
        IsAwaitable = isAwaitable;
        HasNoResponse = method.ReturnsVoid || awaitableIsVoid;
        IsIResult = GetIsIResult();
        IsSerializable = GetIsSerializable();
        ContentType = GetContentType();
        IsEndpointMetadataProvider = ImplementsIEndpointMetadataProvider(ResponseType, wellKnownTypes);
        IsUnion = GetUnionType() is { } unionType &&
            unionType.Implements(WellKnownTypes.GetOptional(WellKnownType.System_Runtime_CompilerServices_IUnion));
        IsUnionResult = TryGetUnionResultCaseMetadataProviderTypes(out var unionCaseTypes);
        UnionResultCaseMetadataProviderTypes = unionCaseTypes;
    }

    private INamedTypeSymbol? GetUnionType()
    {
        var responseType = ResponseType as INamedTypeSymbol;
        return responseType?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? responseType.TypeArguments[0] as INamedTypeSymbol
            : responseType;
    }

    private bool TryGetUnionResultCaseMetadataProviderTypes(out List<ITypeSymbol>? caseMetadataProviderTypes)
    {
        caseMetadataProviderTypes = null;
        var responseType = GetUnionType();
        if (!IsUnion || responseType is null ||
            !responseType.HasAttribute(WellKnownTypes.GetOptional(WellKnownType.System_Runtime_CompilerServices_UnionAttribute)))
        {
            return false;
        }

        var resultType = WellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Http_IResult);
        var caseTypes = new List<ITypeSymbol>();
        // Match the public case constructors used by runtime metadata discovery.
        foreach (var constructor in responseType.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public || constructor.Parameters.Length != 1)
            {
                continue;
            }

            var caseType = constructor.Parameters[0].Type;
            if (!caseType.Implements(resultType) && !SymbolEqualityComparer.Default.Equals(caseType, resultType))
            {
                return false;
            }

            caseTypes.Add(caseType);
        }

        if (caseTypes.Count == 0)
        {
            return false;
        }

        var metadataProviderType = WellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Http_Metadata_IEndpointMetadataProvider);
        caseMetadataProviderTypes = caseTypes.Where(caseType => caseType.Implements(metadataProviderType)).ToList();
        return true;
    }

    private static bool ImplementsIEndpointMetadataProvider(ITypeSymbol? responseType, WellKnownTypes wellKnownTypes)
        => responseType == null ? false : responseType.Implements(wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Http_Metadata_IEndpointMetadataProvider));

    private ITypeSymbol? UnwrapResponseType(IMethodSymbol method, out bool isAwaitable, out bool awaitableIsVoid)
    {
        isAwaitable = false;
        awaitableIsVoid = false;
        var returnType = method.ReturnType;
        var task = WellKnownTypes.Get(WellKnownType.System_Threading_Tasks_Task);
        var taskOfT = WellKnownTypes.Get(WellKnownType.System_Threading_Tasks_Task_T);
        var valueTask = WellKnownTypes.Get(WellKnownType.System_Threading_Tasks_ValueTask);
        var valueTaskOfT = WellKnownTypes.Get(WellKnownType.System_Threading_Tasks_ValueTask_T);
        if (returnType.OriginalDefinition.Equals(taskOfT, SymbolEqualityComparer.Default) ||
            returnType.OriginalDefinition.Equals(valueTaskOfT, SymbolEqualityComparer.Default))
        {
            isAwaitable = true;
            awaitableIsVoid = false;
            return ((INamedTypeSymbol)returnType).TypeArguments[0];
        }

        if (returnType.OriginalDefinition.Equals(task, SymbolEqualityComparer.Default) ||
            returnType.OriginalDefinition.Equals(valueTask, SymbolEqualityComparer.Default))
        {
            isAwaitable = true;
            awaitableIsVoid = true;
            return null;
        }

        return returnType;
    }

    private bool GetIsSerializable() =>
        !IsIResult &&
        !HasNoResponse &&
        ResponseType != null &&
        ResponseType.TypeKind != TypeKind.Error &&
        ResponseType.SpecialType != SpecialType.System_String &&
        ResponseType.SpecialType != SpecialType.System_Object;

    private bool GetIsIResult()
    {
        var resultType = WellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Http_IResult);
        return WellKnownTypes.Implements(ResponseType, resultType) ||
            SymbolEqualityComparer.Default.Equals(ResponseType, resultType);
    }

    private string? GetContentType()
    {
        // `void` returning methods do not have a Content-Type.
        // We don't have a strategy for resolving a Content-Type
        // from an IResult. Typically, this would be done via an
        // IEndpointMetadataProvider so we don't need to set a
        // Content-Type here.
        if (IsIResult || HasNoResponse)
        {
            return null;
        }

        return ResponseType!.SpecialType is SpecialType.System_String ? "text/plain; charset=utf-8" : "application/json";
    }

    public override bool Equals(object obj)
    {
        return obj is EndpointResponse otherEndpointResponse &&
            SymbolEqualityComparer.Default.Equals(otherEndpointResponse.ResponseType, ResponseType) &&
            SymbolEqualityComparer.Default.Equals(otherEndpointResponse.WrappedResponseType, WrappedResponseType) &&
            otherEndpointResponse.WrappedResponseTypeDisplayName.Equals(WrappedResponseTypeDisplayName, StringComparison.Ordinal) &&
            otherEndpointResponse.IsAwaitable == IsAwaitable &&
            otherEndpointResponse.HasNoResponse == HasNoResponse &&
            otherEndpointResponse.IsIResult == IsIResult &&
            otherEndpointResponse.IsUnionResult == IsUnionResult &&
            string.Equals(otherEndpointResponse.ContentType, ContentType, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() =>
        HashCode.Combine(SymbolEqualityComparer.Default.GetHashCode(ResponseType), SymbolEqualityComparer.Default.GetHashCode(WrappedResponseType), WrappedResponseTypeDisplayName, IsAwaitable, HasNoResponse, IsIResult, IsUnionResult, ContentType);
}
