// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Caching.Tests;

public class CacheConfigTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicUsage(bool useCustomBackend)
    {
        var services = new ServiceCollection();

        if (useCustomBackend)
        {
            services.AddSingleton<IDistributedCache, CustomBackend>();
        }
        services.AddTypedDistributedCache();
        services.AddScoped<SomeService>();
        var provider = services.BuildServiceProvider();

        var s = provider.GetService<SomeService>();
        Assert.NotNull(s);

        Assert.Equal(0, s.BackendCalls);
        var x = await s.GetFromCacheAsync();
        Assert.NotNull(x);
        Assert.Equal(1, s.BackendCalls);

        for (int i = 0; i < 10; i++)
        {
            var y = await s.GetFromCacheAsync();
            Assert.NotNull(y);
            Assert.NotSame(x, y);
        }
        Assert.Equal(1, s.BackendCalls);

        await Task.Delay(TimeSpan.FromSeconds(1.5)); // timeout

        for (int i = 0; i < 10; i++)
        {
            var y = await s.GetFromCacheAsync();
            Assert.NotNull(y);
            Assert.NotSame(x, y);
        }
        Assert.Equal(2, s.BackendCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatefulUsage(bool useCustomBackend)
    {
        var services = new ServiceCollection();

        if (useCustomBackend)
        {
            services.AddSingleton<IDistributedCache, CustomBackend>();
        }
        services.AddTypedDistributedCache();
        services.AddScoped<SomeService>();
        var provider = services.BuildServiceProvider();

        var s = provider.GetService<SomeService>();
        Assert.NotNull(s);

        Assert.Equal(0, s.BackendCalls);
        var x = await s.GetFromCacheWithStateAsync(42);
        Assert.NotNull(x);
        Assert.Equal(42, x.Id);
        Assert.Equal(1, s.BackendCalls);

        for (int i = 0; i < 10; i++)
        {
            var y = await s.GetFromCacheWithStateAsync(42);
            Assert.NotNull(y);
            Assert.NotSame(x, y);
            Assert.Equal(42, y.Id);
        }
        Assert.Equal(1, s.BackendCalls);

        await Task.Delay(TimeSpan.FromSeconds(1.5)); // timeout

        for (int i = 0; i < 10; i++)
        {
            var y = await s.GetFromCacheWithStateAsync(42);
            Assert.NotNull(y);
            Assert.Equal(42, y.Id);
            Assert.NotSame(x, y);
        }
        Assert.Equal(2, s.BackendCalls);

        var z = await s.GetFromCacheWithStateAsync(43);
        Assert.NotNull(z);
        Assert.NotSame(x, z);
        Assert.Equal(43, z.Id);
        Assert.Equal(3, s.BackendCalls);
    }
}

public class SomeService(IAdvancedDistributedCache cache)
{
    private int _backendCalls;
    public int BackendCalls => _backendCalls;
    private ValueTask<Foo> BackendAsync()
    {
        var obj = new Foo { Id = Interlocked.Increment(ref _backendCalls) };
        return new(obj);
    }
    private ValueTask<Foo> BackendAsync(int id)
    {
        Interlocked.Increment(ref _backendCalls);
        var obj = new Foo { Id = id };
        return new(obj);
    }

    public async Task<Foo> GetFromCacheAsync() => await cache.GetAsync("foos", _ => BackendAsync(), CacheExpiration);

    public async Task<Foo> GetFromCacheWithStateAsync(int id) => await cache.GetAsync($"foos_{id}", (obj: this, id), static (state, ct) => state.obj.BackendAsync(state.id), CacheExpiration);

    private static readonly DistributedCacheEntryOptions CacheExpiration = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)
    };
}
public class Foo
{
    public int Id { get; set; }
}

class CustomBackend : IBufferDistributedCache
{
    readonly record struct ExpiringBuffer(DateTime Expiration, byte[] Payload)
    {
        public bool IsAlive() => DateTime.UtcNow < Expiration;
    }

    private ConcurrentDictionary<string, ExpiringBuffer> _cache = new();

    Task IDistributedCache.RemoveAsync(string key, CancellationToken token)
    {
        _cache.Remove(key, out _);
        return Task.CompletedTask;
    }
    ValueTask IBufferDistributedCache.SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken cancellationToken)
    {
        var expiration = options.AbsoluteExpirationRelativeToNow is { } ttl ? DateTime.UtcNow + ttl : DateTime.MaxValue;
        _cache[key] = new ExpiringBuffer(expiration, value.ToArray());
        return default;
    }
    ValueTask<bool> IBufferDistributedCache.TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out var found) && found.IsAlive())
        {
            destination.Write(found.Payload);
            return new(true);
        }
        return default;
    }

    byte[] IDistributedCache.Get(string key) => throw new NotImplementedException();

    Task<byte[]> IDistributedCache.GetAsync(string key, CancellationToken token) => throw new NotImplementedException();

    void IDistributedCache.Refresh(string key) => throw new NotImplementedException();

    Task IDistributedCache.RefreshAsync(string key, CancellationToken token) => throw new NotImplementedException();

    void IDistributedCache.Remove(string key) => throw new NotImplementedException();

    void IDistributedCache.Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotImplementedException();

    Task IDistributedCache.SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token) => throw new NotImplementedException();
}

