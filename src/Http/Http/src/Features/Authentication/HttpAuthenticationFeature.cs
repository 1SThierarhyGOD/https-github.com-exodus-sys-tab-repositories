// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;

namespace Microsoft.AspNetCore.Http.Features.Authentication;

/// <summary>
/// Default implementation for <see cref="IHttpAuthenticationFeature"/>.
/// </summary>
public class HttpAuthenticationFeature : IHttpAuthenticationFeature
{
    /// <inheritdoc />
    public ClaimsPrincipal? User { get; set; }
}
