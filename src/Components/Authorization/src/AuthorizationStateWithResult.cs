// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Microsoft.AspNetCore.Components.Authorization;

/// <summary>
/// Represents an <see cref="AuthenticationState"/> that also includes
/// the result of an authorization evaluation.
/// </summary>
/// <remarks>
/// This type enables Blazor components to access detailed authorization
/// results, including failure reasons, while preserving the existing
/// <see cref="AuthenticationState"/>-based template model.
/// </remarks>
public sealed class AuthorizationStateWithResult : AuthenticationState
{
    /// <summary>
    /// Gets the authorization result associated with the current user.
    /// </summary>
    /// <value>
    /// An <see cref="AuthorizationResult"/> containing success or failure
    /// information and any associated failure reasons.
    /// </value>
    public AuthorizationResult AuthorizationResult { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationStateWithResult"/> class.
    /// </summary>
    /// <param name="user">
    /// The <see cref="ClaimsPrincipal"/> representing the current authenticated user.
    /// </param>
    /// <param name="authorizationResult">
    /// The <see cref="AuthorizationResult"/> produced by the authorization system.
    /// </param>
    public AuthorizationStateWithResult(
        ClaimsPrincipal user,
        AuthorizationResult authorizationResult)
        : base(user)
    {
        AuthorizationResult = authorizationResult;
    }
}
