// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.Authorization;

/// <summary>
/// A base class for components that display differing content depending on the user's authorization status.
/// </summary>
public abstract class AuthorizeViewCore : ComponentBase
{
    private AuthenticationState? currentAuthenticationState;
    private AuthorizationResult? currentAuthorizationResult;

    /// <summary>
    /// The content that will be displayed if the user is authorized.
    /// </summary>
    [Parameter] public RenderFragment<AuthenticationState>? ChildContent { get; set; }

    /// <summary>
    /// The content that will be displayed if the user is not authorized.
    /// </summary>
    /// <remarks>
    /// This fragment is used when the user is not authenticated. It also serves as
    /// the fallback for authorization failures when <see cref="Forbidden"/> is not supplied.
    /// In fallback scenarios, the fragment receives an <see cref="AuthorizationStateWithResult"/>
    /// instance as the runtime context, allowing callers to access the associated
    /// <see cref="AuthorizationResult"/> and any authorization failure reasons.
    /// </remarks>
    [Parameter] public RenderFragment<AuthenticationState>? NotAuthorized { get; set; }

    /// <summary>
    /// The content that will be displayed if the user is authorized.
    /// If you specify a value for this parameter, do not also specify a value for <see cref="ChildContent"/>.
    /// </summary>
    [Parameter] public RenderFragment<AuthenticationState>? Authorized { get; set; }

    /// <summary>
    /// The content that will be displayed while asynchronous authorization is in progress.
    /// </summary>
    [Parameter] public RenderFragment? Authorizing { get; set; }

    /// <summary>
    /// The content that will be displayed if the user is authenticated but not authorized.
    /// </summary>
    /// <remarks>
    /// This fragment represents the logical "forbidden" (HTTP 403) case and receives
    /// an <see cref="AuthorizationStateWithResult"/> containing the current user and
    /// the authorization result, including any failure reasons.
    /// If this parameter is not specified, the component falls back to rendering
    /// <see cref="NotAuthorized"/>.
    /// </remarks>
    [Parameter] public RenderFragment<AuthorizationStateWithResult>? Forbidden { get; set; }

    /// <summary>
    /// The resource to which access is being controlled.
    /// </summary>
    [Parameter] public object? Resource { get; set; }

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    [Inject] private IAuthorizationPolicyProvider AuthorizationPolicyProvider { get; set; } = default!;

    [Inject] private IAuthorizationService AuthorizationService { get; set; } = default!;

    private static bool ShouldRenderNotAuthorized(AuthenticationState authenticationState)
    {
        return authenticationState.User.Identity?.IsAuthenticated != true;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var authorized = Authorized ?? ChildContent;

        if (currentAuthorizationResult is null)
        {
            builder.AddContent(0, Authorizing);
            return;
        }

        if (currentAuthorizationResult.Succeeded)
        {
            builder.AddContent(0, authorized?.Invoke(currentAuthenticationState!));
            return;
        }

        var authenticationStateWithResult = new AuthorizationStateWithResult(
            currentAuthenticationState!.User,
            currentAuthorizationResult);

        if (ShouldRenderNotAuthorized(currentAuthenticationState))
        {
            builder.AddContent(0, NotAuthorized?.Invoke(authenticationStateWithResult));
            return;
        }

        if (Forbidden is not null)
        {
            builder.AddContent(0, Forbidden.Invoke(authenticationStateWithResult));
            return;
        }

        builder.AddContent(0, NotAuthorized?.Invoke(authenticationStateWithResult));
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // We allow 'ChildContent' for convenience in basic cases, and 'Authorized' for symmetry
        // with 'NotAuthorized' in other cases. Besides naming, they are equivalent. To avoid
        // confusion, explicitly prevent the case where both are supplied.
        if (ChildContent != null && Authorized != null)
        {
            throw new InvalidOperationException($"Do not specify both '{nameof(Authorized)}' and '{nameof(ChildContent)}'.");
        }

        if (AuthenticationState == null)
        {
            throw new InvalidOperationException($"Authorization requires a cascading parameter of type Task<{nameof(AuthenticationState)}>. Consider using {typeof(CascadingAuthenticationState).Name} to supply this.");
        }

        // Clear the previous authorization result so the Authorizing content can be shown
        // while the new authorization check is being evaluated.
        currentAuthorizationResult = null;

        currentAuthenticationState = await AuthenticationState;

        var authorizeData = GetAuthorizeData();
        if (authorizeData is null)
        {
            currentAuthorizationResult = AuthorizationResult.Success();
            return;
        }

        EnsureNoAuthenticationSchemeSpecified(authorizeData);

        var policy = await AuthorizationPolicy.CombineAsync(
            AuthorizationPolicyProvider,
            authorizeData);

        currentAuthorizationResult = await AuthorizationService.AuthorizeAsync(currentAuthenticationState.User, Resource, policy!);
    }

    /// <summary>
    /// Gets the data required to apply authorization rules.
    /// </summary>
    protected abstract IAuthorizeData[]? GetAuthorizeData();

    private static void EnsureNoAuthenticationSchemeSpecified(IAuthorizeData[] authorizeData)
    {
        // It's not meaningful to specify a nonempty scheme, since by the time Components
        // authorization runs, we already have a specific ClaimsPrincipal (we're stateful).
        // To avoid any confusion, ensure the developer isn't trying to specify a scheme.
        for (var i = 0; i < authorizeData.Length; i++)
        {
            var entry = authorizeData[i];
            if (!string.IsNullOrEmpty(entry.AuthenticationSchemes))
            {
                throw new NotSupportedException($"The authorization data specifies an authentication scheme with value '{entry.AuthenticationSchemes}'. Authentication schemes cannot be specified for components.");
            }
        }
    }
}
