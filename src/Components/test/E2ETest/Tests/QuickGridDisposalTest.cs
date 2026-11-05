// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.E2ETest.Infrastructure;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures;
using Microsoft.AspNetCore.E2ETesting;
using OpenQA.Selenium;
using TestServer;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Components.E2ETests.Tests;

public class QuickGridDisposalTest : ServerTestBase<BasicTestAppServerSiteFixture<QuickGridDisposalStartup>>
{
    public QuickGridDisposalTest(
        BrowserFixture browserFixture,
        BasicTestAppServerSiteFixture<QuickGridDisposalStartup> serverFixture,
        ITestOutputHelper output)
        : base(browserFixture, serverFixture, output)
    {
    }

    public override Task InitializeAsync() => InitializeAsync(BrowserFixture.StreamingContext);

    [Fact]
    public void RemovesListenersWhenDisposedDuringPendingInit()
    {
        VerifyListenerDisposal(holdInit: true);
    }

    [Fact]
    public void RemovesListenersWhenDisposedAfterInit()
    {
        VerifyListenerDisposal(holdInit: false);
    }

    private void VerifyListenerDisposal(bool holdInit)
    {
        Navigate($"{ServerPathBase}/quickgrid-disposal");
        Browser.Exists(By.Id("show-grid"));
        var js = (IJavaScriptExecutor)Browser;
        try
        {
            js.ExecuteScript("quickGridDisposal.hold = arguments[0]", holdInit);
            Browser.Click(By.Id("show-grid"));
            Browser.Equal(1L, () => js.ExecuteScript("return quickGridDisposal.inits"));
            js.ExecuteScript("quickGridDisposal.probe()");
            Assert.Equal(3L, js.ExecuteScript("return quickGridDisposal.connectedAccesses"));
            if (!holdInit)
            {
                Browser.Equal(1L, () => js.ExecuteScript("return quickGridDisposal.completions"));
            }

            Browser.Click(By.Id("remove-grid"));
            Browser.Equal(1L, () => js.ExecuteScript("return quickGridDisposal.disposals"));
            Browser.DoesNotExist(By.CssSelector("table.quickgrid"));
            if (holdInit)
            {
                Assert.Equal(0L, js.ExecuteScript("return quickGridDisposal.completions"));
                js.ExecuteScript("quickGridDisposal.probe()");
                Assert.Equal(3L, js.ExecuteScript("return quickGridDisposal.detachedAccesses"));
            }

            js.ExecuteScript("quickGridDisposal.release()");
            Browser.Equal(1L, () => js.ExecuteScript("return quickGridDisposal.completions"));
            js.ExecuteScript("quickGridDisposal.probe()");
            Assert.Equal(0L, js.ExecuteScript("return quickGridDisposal.detachedAccesses"));
        }
        finally
        {
            js.ExecuteScript("quickGridDisposal.cleanup()");
        }
    }
}
