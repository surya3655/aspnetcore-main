// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Test.Helpers;
using Microsoft.AspNetCore.InternalTesting;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Components.Authorization;

public class AuthorizeViewTest
{
    // Nothing should exceed the timeout in a successful run of the the tests, this is just here to catch
    // failures.
    private static readonly TimeSpan Timeout = Debugger.IsAttached ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(10);

    [Fact]
    public void RendersNothingIfNotAuthorized()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView(
            childContent:
                context => builder => builder.AddContent(0, "This should not be rendered"));

        // Act
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        // Assert
        var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
        Assert.Empty(diff.Edits);

        // Assert: The IAuthorizationService was given expected criteria
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Null(call.user.Identity);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public void RendersNotAuthorizedIfNotAuthorized()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView(
            notAuthorized:
                context => builder => builder.AddContent(0, $"You are not authorized, even though we know you are {context.User.Identity.Name}"));
        rootComponent.AuthenticationState = CreateAuthenticationState("Nellie");

        // Act
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        // Assert
        var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
        Assert.Collection(diff.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
            AssertFrame.Text(
                renderer.Batches.Single().ReferenceFrames[edit.ReferenceFrameIndex],
                "You are not authorized, even though we know you are Nellie");
        });

        // Assert: The IAuthorizationService was given expected criteria
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Nellie", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public void RendersNothingIfAuthorizedButNoChildContentOrAuthorizedProvided()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        authorizationService.NextResult = AuthorizationResult.Success();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView();
        rootComponent.AuthenticationState = CreateAuthenticationState("Nellie");

        // Act
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        // Assert
        var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
        Assert.Empty(diff.Edits);

        // Assert: The IAuthorizationService was given expected criteria
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Nellie", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public void RendersChildContentIfAuthorized()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        authorizationService.NextResult = AuthorizationResult.Success();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView(
            childContent: context => builder =>
                builder.AddContent(0, $"You are authenticated as {context.User.Identity.Name}"));
        rootComponent.AuthenticationState = CreateAuthenticationState("Nellie");

        // Act
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        // Assert
        var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
        Assert.Collection(diff.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
            AssertFrame.Text(
                renderer.Batches.Single().ReferenceFrames[edit.ReferenceFrameIndex],
                "You are authenticated as Nellie");
        });

        // Assert: The IAuthorizationService was given expected criteria
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Nellie", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public void RendersAuthorizedIfAuthorized()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        authorizationService.NextResult = AuthorizationResult.Success();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView(
            authorized: context => builder =>
                builder.AddContent(0, $"You are authenticated as {context.User.Identity.Name}"));
        rootComponent.AuthenticationState = CreateAuthenticationState("Nellie");

        // Act
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        // Assert
        var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
        Assert.Collection(diff.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
            AssertFrame.Text(
                renderer.Batches.Single().ReferenceFrames[edit.ReferenceFrameIndex],
                "You are authenticated as Nellie");
        });

        // Assert: The IAuthorizationService was given expected criteria
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Nellie", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public void RespondsToChangeInAuthorizationState()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        authorizationService.NextResult = AuthorizationResult.Success();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView(
            childContent: context => builder =>
                builder.AddContent(0, $"You are authenticated as {context.User.Identity.Name}"));
        rootComponent.AuthenticationState = CreateAuthenticationState("Nellie");

        // Render in initial state. From other tests, we know this renders
        // a single batch with the correct output.
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();
        var authorizeViewComponentId = renderer.Batches.Single()
            .GetComponentFrames<AuthorizeView>().Single().ComponentId;
        authorizationService.AuthorizeCalls.Clear();

        // Act
        rootComponent.AuthenticationState = CreateAuthenticationState("Ronaldo");
        rootComponent.TriggerRender();

        // Assert: It's only one new diff. We skip the intermediate "await" render state
        // because the task was completed synchronously.
        Assert.Equal(2, renderer.Batches.Count);
        var batch = renderer.Batches.Last();
        var diff = batch.DiffsByComponentId[authorizeViewComponentId].Single();
        Assert.Collection(diff.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.UpdateText, edit.Type);
            AssertFrame.Text(
                batch.ReferenceFrames[edit.ReferenceFrameIndex],
                "You are authenticated as Ronaldo");
        });

        // Assert: The IAuthorizationService was given expected criteria
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Ronaldo", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public void ThrowsIfBothChildContentAndAuthorizedProvided()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView(
            authorized: context => builder => { },
            childContent: context => builder => { });

        // Act/Assert
        renderer.AssignRootComponentId(rootComponent);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            rootComponent.TriggerRender());
        Assert.Equal("Do not specify both 'Authorized' and 'ChildContent'.", ex.Message);
    }

    [Fact]
    public void RendersNothingUntilAuthorizationCompleted()
    {
        // Arrange
        var @event = new ManualResetEventSlim();
        var authorizationService = new TestAuthorizationService();
        var renderer = CreateTestRenderer(authorizationService);
        renderer.OnUpdateDisplayComplete = () => { @event.Set(); };
        var rootComponent = WrapInAuthorizeView(
            notAuthorized:
                context => builder => builder.AddContent(0, "You are not authorized"));
        var authTcs = new TaskCompletionSource<AuthenticationState>();
        rootComponent.AuthenticationState = authTcs.Task;

        // Act/Assert 1: Auth pending
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();
        var batch1 = renderer.Batches.Single();
        var authorizeViewComponentId = batch1.GetComponentFrames<AuthorizeView>().Single().ComponentId;
        var diff1 = batch1.DiffsByComponentId[authorizeViewComponentId].Single();
        Assert.Empty(diff1.Edits);

        // Act/Assert 2: Auth process completes asynchronously
        @event.Reset();
        authTcs.SetResult(new AuthenticationState(new ClaimsPrincipal()));

        // We need to wait here because the continuations of SetResult will be scheduled to run asynchronously.
        @event.Wait(Timeout);

        Assert.Equal(2, renderer.Batches.Count);
        var batch2 = renderer.Batches[1];
        var diff2 = batch2.DiffsByComponentId[authorizeViewComponentId].Single();
        Assert.Collection(diff2.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
            AssertFrame.Text(
                batch2.ReferenceFrames[edit.ReferenceFrameIndex],
                "You are not authorized");
        });
    }

    [Fact]
    public async Task RendersAuthorizingUntilAuthorizationCompletedAsync()
    {
        // Covers https://github.com/dotnet/aspnetcore/pull/31794
        // Arrange
        var authorizationService = new TestAsyncAuthorizationService();
        authorizationService.NextResult = AuthorizationResult.Success();
        var renderer = CreateTestRenderer(authorizationService);
        renderer.OnUpdateDisplayComplete = () => { };
        var rootComponent = WrapInAuthorizeView(
            authorizing: builder => builder.AddContent(0, "Auth pending..."),
            authorized: context => builder => builder.AddContent(0, $"Hello, {context.User.Identity.Name}!"));

        var authTcs = new TaskCompletionSource<AuthenticationState>();
        rootComponent.AuthenticationState = authTcs.Task;

        // Act/Assert 1: Auth pending
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();
        var batch1 = Assert.Single(renderer.Batches);
        var authorizeViewComponentId = Assert.Single(batch1.GetComponentFrames<AuthorizeView>()).ComponentId;
        var diff1 = Assert.Single(batch1.DiffsByComponentId[authorizeViewComponentId]);
        Assert.Collection(diff1.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
            AssertFrame.Text(
                batch1.ReferenceFrames[edit.ReferenceFrameIndex],
                "Auth pending...");
        });

        // We need to do this because the continuation from the TCS might run asynchronously
        // (This wouldn't happen under the sync context or in wasm)
        var renderTcs = new TaskCompletionSource();
        renderer.OnUpdateDisplayComplete = () => renderTcs.SetResult();
        authTcs.SetResult(await CreateAuthenticationState("Monsieur"));

        await renderTcs.Task;
        Assert.Equal(2, renderer.Batches.Count);
        var batch2 = renderer.Batches[1];
        var diff2 = Assert.Single(batch2.DiffsByComponentId[authorizeViewComponentId]);
        Assert.Collection(diff2.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.UpdateText, edit.Type);
            Assert.Equal(0, edit.SiblingIndex);
            AssertFrame.Text(
                batch2.ReferenceFrames[edit.ReferenceFrameIndex],
                "Hello, Monsieur!");
        });

        // Assert: The IAuthorizationService was given expected criteria
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Monsieur", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public async Task RendersAuthorizingUntilAuthorizationCompleted()
    {
        // Arrange
        var @event = new ManualResetEventSlim();
        var authorizationService = new TestAuthorizationService();
        authorizationService.NextResult = AuthorizationResult.Success();
        var renderer = CreateTestRenderer(authorizationService);
        renderer.OnUpdateDisplayComplete = () => { @event.Set(); };
        var rootComponent = WrapInAuthorizeView(
            authorizing: builder => builder.AddContent(0, "Auth pending..."),
            authorized: context => builder => builder.AddContent(0, $"Hello, {context.User.Identity.Name}!"));
        var authTcs = new TaskCompletionSource<AuthenticationState>();
        rootComponent.AuthenticationState = authTcs.Task;

        // Act/Assert 1: Auth pending
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();
        var batch1 = renderer.Batches.Single();
        var authorizeViewComponentId = batch1.GetComponentFrames<AuthorizeView>().Single().ComponentId;
        var diff1 = batch1.DiffsByComponentId[authorizeViewComponentId].Single();
        Assert.Collection(diff1.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
            AssertFrame.Text(
                batch1.ReferenceFrames[edit.ReferenceFrameIndex],
                "Auth pending...");
        });

        // Act/Assert 2: Auth process completes asynchronously
        @event.Reset();
        authTcs.SetResult(await CreateAuthenticationState("Monsieur"));

        // We need to wait here because the continuations of SetResult will be scheduled to run asynchronously.
        @event.Wait(Timeout);

        Assert.Equal(2, renderer.Batches.Count);
        var batch2 = renderer.Batches[1];
        var diff2 = batch2.DiffsByComponentId[authorizeViewComponentId].Single();
        Assert.Collection(diff2.Edits, edit =>
        {
            Assert.Equal(RenderTreeEditType.UpdateText, edit.Type);
            Assert.Equal(0, edit.SiblingIndex);
            AssertFrame.Text(
                batch2.ReferenceFrames[edit.ReferenceFrameIndex],
                "Hello, Monsieur!");
        });

        // Assert: The IAuthorizationService was given expected criteria
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Monsieur", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public void IncludesPolicyInAuthorizeCall()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView(policy: "MyTestPolicy");
        rootComponent.AuthenticationState = CreateAuthenticationState("Nellie");

        // Act
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        // Assert
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Nellie", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements,
                req => Assert.Equal("MyTestPolicy", ((TestPolicyRequirement)req).PolicyName));
        });
    }

    [Fact]
    public void IncludesRolesInAuthorizeCall()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = WrapInAuthorizeView(roles: "SuperTestRole1, SuperTestRole2");
        rootComponent.AuthenticationState = CreateAuthenticationState("Nellie");

        // Act
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        // Assert
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Nellie", call.user.Identity.Name);
            Assert.Null(call.resource);
            Assert.Collection(call.requirements, req => Assert.Equal(
                new[] { "SuperTestRole1", "SuperTestRole2" },
                ((RolesAuthorizationRequirement)req).AllowedRoles));
        });
    }

    [Fact]
    public void IncludesResourceInAuthorizeCall()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        var renderer = CreateTestRenderer(authorizationService);
        var resource = new object();
        var rootComponent = WrapInAuthorizeView(resource: resource);
        rootComponent.AuthenticationState = CreateAuthenticationState("Nellie");

        // Act
        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        // Assert
        Assert.Collection(authorizationService.AuthorizeCalls, call =>
        {
            Assert.Equal("Nellie", call.user.Identity.Name);
            Assert.Same(resource, call.resource);
            Assert.Collection(call.requirements, req =>
                Assert.IsType<DenyAnonymousAuthorizationRequirement>(req));
        });
    }

    [Fact]
    public void RejectsNonemptyScheme()
    {
        // Arrange
        var authorizationService = new TestAuthorizationService();
        var renderer = CreateTestRenderer(authorizationService);
        var rootComponent = new TestAuthStateProviderComponent(builder =>
        {
            builder.OpenComponent<AuthorizeViewCoreWithScheme>(0);
            builder.CloseComponent();
        });
        renderer.AssignRootComponentId(rootComponent);

        // Act/Assert
        var ex = Assert.Throws<NotSupportedException>(rootComponent.TriggerRender);
        Assert.Equal("The authorization data specifies an authentication scheme with value 'test scheme'. Authentication schemes cannot be specified for components.", ex.Message);
    }

    [Fact]
    public void RendersForbiddenContentForAuthenticatedUnauthorizedUser()
    {
        var authorizationService = new TestAuthorizationService
        {
            NextResult = CreateFailedAuthorizationResultWithReason()
        };

        var renderer = CreateTestRenderer(authorizationService);

        var renderedAuthorized = false;
        var renderedNotAuthorized = false;
        var renderedForbidden = false;

        var rootComponent = WrapInAuthorizeView(
            authorized: context => builder =>
            {
                renderedAuthorized = true;
                builder.AddContent(0, "Authorized content");
            },
            notAuthorized: context => builder =>
            {
                renderedNotAuthorized = true;
                builder.AddContent(0, "NotAuthorized content");
            },
            forbidden: context => builder =>
            {
                renderedForbidden = true;
                builder.AddContent(0, "Forbidden content");
            });

        rootComponent.AuthenticationState = Task.FromResult(
            new AuthenticationState(CreateAuthenticatedUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.False(renderedAuthorized);
        Assert.False(renderedNotAuthorized);
        Assert.True(renderedForbidden);
    }

    [Fact]
    public void RendersAuthorizedContentWhenAuthorizationSucceedsEvenWhenForbiddenIsSupplied()
    {
        var authorizationService = new TestAuthorizationService
        {
            NextResult = AuthorizationResult.Success()
        };

        var renderer = CreateTestRenderer(authorizationService);

        var renderedAuthorized = false;
        var renderedNotAuthorized = false;
        var renderedForbidden = false;

        var rootComponent = WrapInAuthorizeView(
            authorized: context => builder =>
            {
                renderedAuthorized = true;
                builder.AddContent(0, "Authorized content");
            },
            notAuthorized: context => builder =>
            {
                renderedNotAuthorized = true;
                builder.AddContent(0, "NotAuthorized content");
            },
            forbidden: context => builder =>
            {
                renderedForbidden = true;
                builder.AddContent(0, "Forbidden content");
            });

        rootComponent.AuthenticationState = Task.FromResult(
            new AuthenticationState(CreateAuthenticatedUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.True(renderedAuthorized);
        Assert.False(renderedNotAuthorized);
        Assert.False(renderedForbidden);
    }

    [Fact]
    public void ForbiddenReceivesAuthorizationStateWithResult()
    {
        var authorizationService = new TestAuthorizationService
        {
            NextResult = CreateFailedAuthorizationResultWithReason()
        };

        var renderer = CreateTestRenderer(authorizationService);

        AuthorizationStateWithResult capturedContext = null;

        var rootComponent = WrapInAuthorizeView(
            forbidden: context => builder =>
            {
                capturedContext = context;
                builder.AddContent(0, "Forbidden content");
            });

        rootComponent.AuthenticationState = Task.FromResult(new AuthenticationState(CreateAuthenticatedUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.NotNull(capturedContext);
        Assert.Equal("Test user", capturedContext.User.Identity.Name);
        Assert.False(capturedContext.AuthorizationResult.Succeeded);
    }

    [Fact]
    public void RendersNotAuthorizedContentForAnonymousUserEvenWhenForbiddenIsSupplied()
    {
        var authorizationService = new TestAuthorizationService
        {
            NextResult = CreateFailedAuthorizationResultWithReason()
        };

        var renderer = CreateTestRenderer(authorizationService);

        var renderedNotAuthorized = false;
        var renderedForbidden = false;

        var rootComponent = WrapInAuthorizeView(
            notAuthorized: context => builder =>
            {
                renderedNotAuthorized = true;
                builder.AddContent(0, "NotAuthorized content");
            },
            forbidden: context => builder =>
            {
                renderedForbidden = true;
                builder.AddContent(0, "Forbidden content");
            });

        rootComponent.AuthenticationState = Task.FromResult(new AuthenticationState(CreateAnonymousUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.True(renderedNotAuthorized);
        Assert.False(renderedForbidden);
    }

    [Fact]
    public void ForbiddenCanAccessAuthorizationFailureReason()
    {
        var authorizationService = new TestAuthorizationService
        {
            NextResult = CreateFailedAuthorizationResultWithReason()
        };

        var renderer = CreateTestRenderer(authorizationService);

        string capturedReason = null;

        var rootComponent = WrapInAuthorizeView(
            forbidden: context => builder =>
            {
                capturedReason = context.AuthorizationResult.Failure
                    .FailureReasons
                    .Single()
                    .Message;

                builder.AddContent(0, capturedReason);
            });

        rootComponent.AuthenticationState = Task.FromResult(new AuthenticationState(CreateAuthenticatedUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.Equal(FailureReasonMessage, capturedReason);
    }

    [Fact]
    public void FallsBackToNotAuthorizedWhenForbiddenIsNotSupplied()
    {
        var authorizationService = new TestAuthorizationService
        {
            NextResult = CreateFailedAuthorizationResultWithReason()
        };

        var renderer = CreateTestRenderer(authorizationService);

        var renderedNotAuthorized = false;
        AuthenticationState capturedContext = null;

        var rootComponent = WrapInAuthorizeView(
            notAuthorized: context => builder =>
            {
                renderedNotAuthorized = true;
                capturedContext = context;
                builder.AddContent(0, "NotAuthorized fallback");
            });

        rootComponent.AuthenticationState = Task.FromResult(new AuthenticationState(CreateAuthenticatedUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.True(renderedNotAuthorized);

        var authorizationStateWithResult = Assert.IsType<AuthorizationStateWithResult>(capturedContext);

        Assert.Equal("Test user", authorizationStateWithResult.User.Identity.Name);
        Assert.False(authorizationStateWithResult.AuthorizationResult.Succeeded);
    }

    [Fact]
    public void NotAuthorizedFallbackCanAccessAuthorizationFailureReason()
    {
        var authorizationService = new TestAuthorizationService
        {
            NextResult = CreateFailedAuthorizationResultWithReason()
        };

        var renderer = CreateTestRenderer(authorizationService);

        string capturedReason = null;

        var rootComponent = WrapInAuthorizeView(
            notAuthorized: context => builder =>
            {
                var authorizationStateWithResult =
                    Assert.IsType<AuthorizationStateWithResult>(context);

                capturedReason = authorizationStateWithResult.AuthorizationResult
                    .Failure
                    .FailureReasons
                    .Single()
                    .Message;

                builder.AddContent(0, capturedReason);
            });

        rootComponent.AuthenticationState = Task.FromResult(
            new AuthenticationState(CreateAuthenticatedUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.Equal(FailureReasonMessage, capturedReason);
    }

    [Fact]
    public void ForbiddenCanAccessMultipleAuthorizationFailureReasons()
    {
        var firstReason = "Missing permission: Project.Read";
        var secondReason = "Missing permission: Project.Edit";

        var result = AuthorizationResult.Failed(
            AuthorizationFailure.Failed(new[]
            {
                new AuthorizationFailureReason(new TestFailureReasonHandler(), firstReason),
                new AuthorizationFailureReason(new TestFailureReasonHandler(), secondReason),
            }));

        var authorizationService = new TestAuthorizationService
        {
            NextResult = result
        };

        var renderer = CreateTestRenderer(authorizationService);

        string[] capturedReasons = null;

        var rootComponent = WrapInAuthorizeView(
            forbidden: context => builder =>
            {
                capturedReasons = context.AuthorizationResult.Failure
                    .FailureReasons
                    .Select(reason => reason.Message)
                    .ToArray();

                builder.AddContent(0, "Forbidden content");
            });

        rootComponent.AuthenticationState = Task.FromResult(
            new AuthenticationState(CreateAuthenticatedUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.Equal(new[] { firstReason, secondReason }, capturedReasons);
    }

    [Fact]
    public void RendersAuthorizedContentWhenAuthorizeDataIsNullEvenWhenForbiddenIsSupplied()
    {
        var authorizationService = new TestAuthorizationService
        {
            NextResult = CreateFailedAuthorizationResultWithReason()
        };

        var renderer = CreateTestRenderer(authorizationService);

        var renderedAuthorized = false;
        var renderedNotAuthorized = false;
        var renderedForbidden = false;

        var rootComponent = WrapInAuthorizeViewCoreWithoutAuthorizeData(
            childContent: context => builder =>
            {
                renderedAuthorized = true;
                builder.AddContent(0, "Authorized content");
            },
            notAuthorized: context => builder =>
            {
                renderedNotAuthorized = true;
                builder.AddContent(0, "NotAuthorized content");
            },
            forbidden: context => builder =>
            {
                renderedForbidden = true;
                builder.AddContent(0, "Forbidden content");
            });

        rootComponent.AuthenticationState = Task.FromResult(
            new AuthenticationState(CreateAnonymousUser()));

        renderer.AssignRootComponentId(rootComponent);
        rootComponent.TriggerRender();

        Assert.True(renderedAuthorized);
        Assert.False(renderedNotAuthorized);
        Assert.False(renderedForbidden);
        Assert.Empty(authorizationService.AuthorizeCalls);
    }

    private static TestAuthStateProviderComponent WrapInAuthorizeView(
        RenderFragment<AuthenticationState> childContent = null,
        RenderFragment<AuthenticationState> authorized = null,
        RenderFragment<AuthenticationState> notAuthorized = null,
        RenderFragment authorizing = null,
        RenderFragment<AuthorizationStateWithResult> forbidden = null,
        string policy = null,
        string roles = null,
        object resource = null)
    {
        return new TestAuthStateProviderComponent(builder =>
        {
            var sequence = 0;

            builder.OpenComponent<AuthorizeView>(sequence++);

            if (childContent != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeView.ChildContent), childContent);
            }

            if (authorized != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeView.Authorized), authorized);
            }

            if (notAuthorized != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeView.NotAuthorized), notAuthorized);
            }

            if (authorizing != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeView.Authorizing), authorizing);
            }

            if (forbidden != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeViewCore.Forbidden), forbidden);
            }

            if (policy != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeView.Policy), policy);
            }

            if (roles != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeView.Roles), roles);
            }

            if (resource != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeView.Resource), resource);
            }

            builder.CloseComponent();
        });
    }

    private static TestAuthStateProviderComponent WrapInAuthorizeViewCoreWithoutAuthorizeData(
        RenderFragment<AuthenticationState> childContent = null,
        RenderFragment<AuthenticationState> authorized = null,
        RenderFragment<AuthenticationState> notAuthorized = null,
        RenderFragment authorizing = null,
        RenderFragment<AuthorizationStateWithResult> forbidden = null,
        object resource = null)
    {
        return new TestAuthStateProviderComponent(builder =>
        {
            var sequence = 0;

            builder.OpenComponent<AuthorizeViewCoreWithoutAuthorizeData>(sequence++);

            if (childContent != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeViewCore.ChildContent), childContent);
            }

            if (authorized != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeViewCore.Authorized), authorized);
            }

            if (notAuthorized != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeViewCore.NotAuthorized), notAuthorized);
            }

            if (authorizing != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeViewCore.Authorizing), authorizing);
            }

            if (forbidden != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeViewCore.Forbidden), forbidden);
            }

            if (resource != null)
            {
                builder.AddComponentParameter(sequence++, nameof(AuthorizeViewCore.Resource), resource);
            }

            builder.CloseComponent();
        });
    }

    class TestAuthStateProviderComponent : AutoRenderComponent
    {
        private readonly RenderFragment _childContent;

        public Task<AuthenticationState> AuthenticationState { get; set; }
            = Task.FromResult(new AuthenticationState(new ClaimsPrincipal()));

        public TestAuthStateProviderComponent(RenderFragment childContent)
        {
            _childContent = childContent;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<Task<AuthenticationState>>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<Task<AuthenticationState>>.Value), AuthenticationState);
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(builder =>
            {
                builder.OpenComponent<NeverReRenderComponent>(0);
                builder.AddComponentParameter(1, "ChildContent", _childContent);
                builder.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    // This is useful to show that the reason why a CascadingValue refreshes is because the
    // value itself changed, not just that we're re-rendering the entire tree and have to
    // recurse into all descendants because we're passing ChildContent
    class NeverReRenderComponent : ComponentBase
    {
        [Parameter] public RenderFragment ChildContent { get; set; }

        protected override bool ShouldRender() => false;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, ChildContent);
        }
    }

    public static Task<AuthenticationState> CreateAuthenticationState(string username)
        => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new TestIdentity { Name = username })));

    public static TestRenderer CreateTestRenderer(IAuthorizationService authorizationService)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<AuthenticationStateProvider, TestAuthenticationStateProvider>();
        serviceCollection.AddSingleton<IAuthorizationPolicyProvider, TestAuthorizationPolicyProvider>();
        serviceCollection.AddSingleton(authorizationService);

        return new TestRenderer(serviceCollection.BuildServiceProvider());
    }

    public class AuthorizeViewCoreWithScheme : AuthorizeViewCore
    {
        protected override IAuthorizeData[] GetAuthorizeData()
            => new[] { new AuthorizeAttribute { AuthenticationSchemes = "test scheme" } };
    }

    private const string FailureReasonMessage = "Missing permission: Project.Read";

    private static ClaimsPrincipal CreateAnonymousUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    private static ClaimsPrincipal CreateAuthenticatedUser()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, "Test user"),
            },
            authenticationType: "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    public class AuthorizeViewCoreWithoutAuthorizeData : AuthorizeViewCore
    {
        protected override IAuthorizeData[] GetAuthorizeData()
            => null;
    }

    private static AuthorizationResult CreateFailedAuthorizationResultWithReason()
    {
        var reason = new AuthorizationFailureReason(
            new TestFailureReasonHandler(),
            FailureReasonMessage);

        return AuthorizationResult.Failed(
            AuthorizationFailure.Failed(new[] { reason }));
    }

    private sealed class TestFailureReasonHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
