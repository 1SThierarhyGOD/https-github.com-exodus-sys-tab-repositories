// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Caching.Distributed;
internal sealed class DistributedCache : IAdvancedDistributedCache
{
    private readonly IServiceProvider _services;
    private readonly IDistributedCache _backend;
    private readonly IBufferDistributedCache? _bufferBackend;

    public DistributedCache(IOptions<TypedDistributedCacheOptions> options, IServiceProvider services, IDistributedCache backend)
    {
        _services = services;
        _backend = backend;
        _bufferBackend = backend as IBufferDistributedCache; // do the type test once only
        _ = options;
    }

    static class WrappedCallbackCache<T>
    {
        // for the simple usage scenario (no TState), pack the original callback as the "state", and use a wrapper function that just unrolls and invokes from the state
        public static readonly Func<Func<CancellationToken, ValueTask<T>>, CancellationToken, ValueTask<T>> Instance = static (callback, ct) => callback(ct);

    }
    public ValueTask<T> GetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> callback, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        => GetAsync(key, callback, WrappedCallbackCache<T>.Instance, options, cancellationToken);

    public ValueTask<T> GetAsync<TState, T>(string key, TState state, Func<TState, CancellationToken, ValueTask<T>> callback, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(callback);

        return _bufferBackend is not null
            ? GetBufferedBackendAsync(key, state, callback, options, cancellationToken)
            : GetLegacyBackendAsync(key, state, callback, options, cancellationToken);
    }

    ValueTask IAdvancedDistributedCache.RefreshAsync(string key, CancellationToken cancellationToken) => new(_backend.RefreshAsync(key, cancellationToken));

    private ValueTask<T> GetBufferedBackendAsync<TState, T>(string key, TState state, Func<TState, CancellationToken, ValueTask<T>> callback, DistributedCacheEntryOptions? options, CancellationToken cancellationToken)
    {
        var buffer = new RecyclableArrayBufferWriter<byte>();
        var pendingGet = _bufferBackend!.TryGetAsync(key, buffer, cancellationToken);

        if (!pendingGet.IsCompletedSuccessfully)
        {
            return AwaitedBackend(this, key, state, callback, options, cancellationToken, buffer, pendingGet);
        }

        // fast path; backend available immediately
        if (pendingGet.GetAwaiter().GetResult())
        {
            var result = GetSerializer<T>().Deserialize(new(buffer.GetCommittedMemory()));
            buffer.Dispose();
            return new(result);
        }
    
        // fall back to main code-path, but without the pending bytes (we've already checked those)
        return AwaitedBackend(this, key, state, callback, options, cancellationToken, buffer, default);

        static async ValueTask<T> AwaitedBackend(DistributedCache @this, string key, TState state, Func<TState, CancellationToken, ValueTask<T>> callback, DistributedCacheEntryOptions? options,
             CancellationToken cancellationToken, RecyclableArrayBufferWriter<byte> buffer, ValueTask<bool> pendingGet)
        {
            using (buffer)
            {
                if (await pendingGet)
                {
                    return @this.GetSerializer<T>().Deserialize(new(buffer.GetCommittedMemory()));
                }

                var value = await callback(state, cancellationToken);
                if (value is null)
                {
                    await @this._backend.RemoveAsync(key, cancellationToken);
                }
                else
                {
                    buffer.Reset();
                    @this.GetSerializer<T>().Serialize(value, buffer);
                    await @this._bufferBackend!.SetAsync(key, new(buffer.GetCommittedMemory()), options ?? _defaultOptions, cancellationToken);
                }

                return value;
            }
        }
    }

    private ICacheSerializer<T> GetSerializer<T>()
    {
        var obj = (ICacheSerializer<T>?)_services.GetService(typeof(ICacheSerializer<T>));
        if (obj is null)
        {
            ThrowNoSerializer(typeof(T));
        }
        return obj!;

    }

    static void ThrowNoSerializer(Type type) => throw new InvalidOperationException("No serializer registered for " + type.FullName);

    private ValueTask<T> GetLegacyBackendAsync<TState, T>(string key, TState state, Func<TState,  CancellationToken, ValueTask<T>> callback, DistributedCacheEntryOptions? options, CancellationToken cancellationToken)
    {
        var pendingBytes = _backend.GetAsync(key, cancellationToken);
        if (!pendingBytes.IsCompletedSuccessfully)
        {
            return AwaitedBackend(this, key, state, callback, options, cancellationToken, pendingBytes);
        }

        // fast path; backend available immediately
        var bytes = pendingBytes.Result;
        if (bytes is not null)
        {
            return new(GetSerializer<T>().Deserialize(new ReadOnlySequence<byte>(bytes))!);
        }

        // fall back to main code-path, but without the pending bytes (we've already checked those)
        return AwaitedBackend(this, key, state, callback, options, cancellationToken, null);

        static async ValueTask<T> AwaitedBackend(DistributedCache @this, string key, TState state, Func<TState, CancellationToken, ValueTask<T>> callback, DistributedCacheEntryOptions? options,
             CancellationToken cancellationToken, Task<byte[]?>? pendingBytes)
        {
            if (pendingBytes is not null)
            {
                var bytes = await pendingBytes;
                if (bytes is not null)
                {
                    return @this.GetSerializer<T>().Deserialize(new ReadOnlySequence<byte>(bytes));
                }
            }

            var value = await callback(state, cancellationToken);
            if (value is null)
            {
                await @this._backend.RemoveAsync(key, cancellationToken);
            }
            else
            {
                using var writer = new RecyclableArrayBufferWriter<byte>();
                @this.GetSerializer<T>().Serialize(value, writer);
                await @this._backend.SetAsync(key, writer.ToArray(), options ?? _defaultOptions, cancellationToken);
            }

            return value;
        }
    }

    static readonly DistributedCacheEntryOptions _defaultOptions = new();

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        => new(_backend.RemoveAsync(key, cancellationToken));
}
