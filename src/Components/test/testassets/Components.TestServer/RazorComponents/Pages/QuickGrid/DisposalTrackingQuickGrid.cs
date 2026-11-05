// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.JSInterop;

namespace Components.TestServer.RazorComponents.Pages.QuickGrid;

public class DisposalTrackingQuickGrid : QuickGrid<string>, IAsyncDisposable
{
    [Inject] private IJSRuntime Runtime { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            await Runtime.InvokeVoidAsync("quickGridDisposal.completed");
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await base.DisposeAsync();
        await Runtime.InvokeVoidAsync("quickGridDisposal.disposed");
    }
}
