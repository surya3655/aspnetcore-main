// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authorization;

namespace BasicTestApp.AuthTest;

public sealed record ProjectPermissionRequirement(string Permission) : IAuthorizationRequirement;
