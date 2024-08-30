// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.AspNetCore.OutputCaching.StackExchangeRedis.Tests;

public class OutputCacheServiceExtensionsTests
{
    [Fact]
    public void AddStackExchangeRedisOutputCache_RegistersOutputCacheStoreAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStackExchangeRedisOutputCache();

        // Assert
        var outputCacheStore = services.FirstOrDefault(desc => desc.ServiceType == typeof(IOutputCacheStore));

        Assert.NotNull(outputCacheStore);
        Assert.Equal(ServiceLifetime.Singleton, outputCacheStore.Lifetime);
    }

    [Fact]
    public void AddStackExchangeRedisOutputCache_ReplacesPreviouslyUserRegisteredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped(typeof(IOutputCacheStore), sp => Mock.Of<IOutputCacheStore>());

        // Act
        services.AddStackExchangeRedisOutputCache();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var distributedCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(IOutputCacheStore));

        Assert.NotNull(distributedCache);
        Assert.Equal(ServiceLifetime.Scoped, distributedCache.Lifetime);
        Assert.IsAssignableFrom<RedisOutputCacheStore>(serviceProvider.GetRequiredService<IOutputCacheStore>());
    }

    [Fact]
    public void AddStackExchangeRedisOutputCache_AllowsChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddStackExchangeRedisOutputCache());
    }

    [Fact]
    public void AddStackExchangeRedisOutputCache_IOutputCacheStoreWithoutLoggingCanBeResolved()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStackExchangeRedisOutputCache();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();
        var outputCacheStore = serviceProvider.GetRequiredService<IOutputCacheStore>();

        Assert.NotNull(outputCacheStore);
    }

    [Fact]
    public void AddStackExchangeRedisOutputCache_IOutputCacheStoreWithLoggingCanBeResolved()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStackExchangeRedisOutputCache();
        services.AddLogging();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();
        var outputCacheStore = serviceProvider.GetRequiredService<IOutputCacheStore>();

        Assert.NotNull(outputCacheStore);
    }

    [Fact]
    public void AddStackExchangeRedisOutputCache_UsesLoggerFactoryAlreadyRegisteredWithServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped(typeof(IOutputCacheStore), sp => Mock.Of<IOutputCacheStore>());

        var loggerFactory = new Mock<ILoggerFactory>();

        loggerFactory
            .Setup(lf => lf.CreateLogger(It.IsAny<string>()))
            .Returns((string name) => NullLoggerFactory.Instance.CreateLogger(name))
            .Verifiable();

        services.AddScoped(typeof(ILoggerFactory), _ => loggerFactory.Object);

        // Act
        services.AddLogging();
        services.AddStackExchangeRedisOutputCache();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var distributedCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(IOutputCacheStore));

        Assert.NotNull(distributedCache);
        Assert.Equal(ServiceLifetime.Scoped, distributedCache.Lifetime);
        Assert.IsAssignableFrom<RedisOutputCacheStore>(serviceProvider.GetRequiredService<IOutputCacheStore>());

        loggerFactory.Verify();
    }

    [Fact]
    public void AddStackExchangeRedisOutputCache_UseCustomConfiguration()
    {
        var configuration = ParseConfiguration("""
            {
              "Foo": {
                 "KeyPrefix": "my prefix",
                 "ConnectionString": "my config string"
              }
            }
            """);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<RedisOutputCacheOptions>()
            .BindConfiguration("Foo")
            .Configure<IOptions<CustomOptions>>((inbuilt, custom) =>
            {
                inbuilt.Configuration = custom.Value.ConnectionString;
            });

        services.AddStackExchangeRedisOutputCache();

        var store = Assert.IsAssignableFrom<RedisOutputCacheStore>(
            services.BuildServiceProvider().GetRequiredService<IOutputCacheStore>());
        Assert.Equal("my config string", store.Options.Configuration);

        
    }
    static IConfiguration ParseConfiguration(string json)
    {
        return new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();
    }

    public class CustomOptions
    {
        public TimeSpan DefaultLease { get; set; }
        public string KeyPrefix { get; set; } = default!;
        public string ConnectionString { get; set; } = default!;
    }
}
