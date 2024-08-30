// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Caching.Distributed;

public interface IDistributedCacheInvalidation : IDistributedCache
{
    event Func<string, ValueTask> KeyInvalidated;
    event Func<string, ValueTask> TagInvalidated;
    ValueTask RemoveTagsAsync(ReadOnlyMemory<string> tags, CancellationToken cancellationToken);
}
