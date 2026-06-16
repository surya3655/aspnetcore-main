// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.Endpoints;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(IList<object?>))]
[JsonSerializable(typeof(List<object?>))]
[JsonSerializable(typeof(ComponentParameter))]
[JsonSerializable(typeof(IList<ComponentParameter>))]
internal sealed partial class WebAssemblyComponentSerializationContext : JsonSerializerContext
{
}

// See the details of the component serialization protocol in WebAssemblyComponentDeserializer.cs on the Components solution.
internal sealed class WebAssemblyComponentSerializer
{
    public static void SerializeInvocation(ref ComponentMarker marker, Type type, ParameterView parameters)
    {
        var assembly = type.Assembly.GetName().Name ?? throw new InvalidOperationException("Cannot prerender components from assemblies with a null name");
        var typeFullName = type.FullName ?? throw new InvalidOperationException("Cannot prerender component types with a null name");
        var (definitions, values) = ComponentParameter.FromParameterView(parameters);

        // We need to serialize and Base64 encode parameters separately since they can contain arbitrary data that might
        // cause the HTML comment to be invalid (like if you serialize a string that contains two consecutive dashes "--").
        var serializationOptions = new JsonSerializerOptions { TypeInfoResolver = WebAssemblyComponentSerializationContext.Default };
        var serializedDefinitions = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(definitions, serializationOptions));
        var serializedValues = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(values, serializationOptions));

        marker.WriteWebAssemblyData(assembly, typeFullName, serializedDefinitions, serializedValues);
    }
}
