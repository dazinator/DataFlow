namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// Fluent builder for setting up test service providers with minimal boilerplate.
/// 
/// BEFORE (8+ lines of boilerplate):
/// var services = new ServiceCollection();
/// services.AddScoped<MyActor>();
/// services.AddScoped<IDependency>(_ => mockDependency);
/// var serviceProvider = services.BuildServiceProvider();
/// var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
/// 
/// AFTER (1-2 lines):
/// var scopeFactory = TestServiceBuilder.Create()
///     .WithActor<MyActor>()
///     .WithScoped<IDependency>(mockDependency)
///     .BuildScopeFactory();
/// </summary>
public class TestServiceBuilder
{
    private readonly ServiceCollection _services = new();

    /// <summary>
    /// Creates a new test service builder.
    /// </summary>
    public static TestServiceBuilder Create() => new();

    /// <summary>
    /// Registers an actor as transient (standard pattern for actors).
    /// </summary>
    public TestServiceBuilder WithActor<TActor>() where TActor : class
    {
        _services.AddTransient<TActor>();
        return this;
    }

    /// <summary>
    /// Registers a scoped service with an instance.
    /// </summary>
    public TestServiceBuilder WithScoped<TService>(TService instance) where TService : class
    {
        _services.AddScoped<TService>(_ => instance);
        return this;
    }

    /// <summary>
    /// Registers a scoped service with a factory.
    /// </summary>
    public TestServiceBuilder WithScoped<TService>(Func<IServiceProvider, TService> factory) where TService : class
    {
        _services.AddScoped(factory);
        return this;
    }

    /// <summary>
    /// Registers a singleton service with an instance.
    /// </summary>
    public TestServiceBuilder WithSingleton<TService>(TService instance) where TService : class
    {
        _services.AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Builds the service provider.
    /// </summary>
    public IServiceProvider Build()
    {
        return _services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds and returns the scope factory (common pattern for actor blocks).
    /// </summary>
    public IServiceScopeFactory BuildScopeFactory()
    {
        var serviceProvider = Build();
        return serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }
}
