// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components;

// This extends the shared ServerComponentSerializationContext with types only available in Components.Server.
// Components.Server compiles Components.Shared source which includes RootComponentOperation* types.
// Components.Endpoints cannot see RootComponentOperation* types, so they are NOT in the shared context.
[JsonSerializable(typeof(RootComponentOperationBatch))]
[JsonSerializable(typeof(RootComponentOperation))]
[JsonSerializable(typeof(RootComponentOperationType))]
internal sealed partial class ServerComponentSerializationContext
{
}
