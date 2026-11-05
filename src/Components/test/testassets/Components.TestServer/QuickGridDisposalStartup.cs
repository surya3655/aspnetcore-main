// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Components.TestServer.RazorComponents;
using Microsoft.AspNetCore.Builder;

namespace TestServer;

public class QuickGridDisposalStartup(IConfiguration configuration)
{
    private readonly RazorComponentEndpointsStartup<App> _startup = new(configuration);

    public void ConfigureServices(IServiceCollection services) => _startup.ConfigureServices(services);

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Query.ContainsKey("original")
                && context.Request.Path.Value is { } path
                && path.Contains("/_content/Microsoft.AspNetCore.Components.QuickGrid/QuickGrid", StringComparison.Ordinal)
                && path.EndsWith(".razor.js", StringComparison.Ordinal))
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.ContentType = "text/javascript";
                await context.Response.WriteAsync("""
                    import { init as originalInit } from './QuickGrid.razor.js?original=1';
                    export * from './QuickGrid.razor.js?original=1';
                    export function init(table) {
                        return window.quickGridDisposal.init(table, originalInit);
                    }
                    """);
                return;
            }

            await next();
        });

        _startup.Configure(app, env);
    }
}
