// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Components.Server.Circuits;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(string[]))]
internal sealed partial class CircuitOptionsJsonSerializerContext : JsonSerializerContext
{
}

internal sealed class CircuitOptionsJavaScriptInitializersConfiguration : IConfigureOptions<CircuitOptions>
{
    private readonly IWebHostEnvironment _environment;

    public CircuitOptionsJavaScriptInitializersConfiguration(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public void Configure(CircuitOptions options)
    {
        var file = _environment.WebRootFileProvider.GetFileInfo($"{_environment.ApplicationName}.modules.json");
        if (file.Exists)
        {
            var context = new CircuitOptionsJsonSerializerContext();
            var initializers = JsonSerializer.Deserialize<string[]>(file.CreateReadStream(), context.GetTypeInfo(typeof(string[])) as System.Text.Json.Serialization.Metadata.JsonTypeInfo<string[]>);
            if (initializers != null)
            {
                for (var i = 0; i < initializers.Length; i++)
                {
                    var initializer = initializers[i];
                    options.JavaScriptInitializers.Add(initializer);
                }
            }
        }
    }
}
