using Microsoft.Extensions.DependencyInjection;
using SteamOid2.API;
using SteamOid2.WebRequests;

namespace SteamOid2;

/// <summary>
/// Extensions to add this library to a <see cref="IServiceCollection"/>.
/// </summary>
public static class SteamOid2DependencyInjectionExtensions
{
    /// <summary>
    /// Adds <see cref="ISteamOid2Client"/> and <see cref="ISteamOid2WebRequestProvider"/> to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add relevant services to.</param>
    /// <param name="lifetime">The lifetime of <see cref="ISteamOid2Client"/>, defaulting to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The same collection for chaining.</returns>
    public static IServiceCollection AddSteamOid2(this IServiceCollection serviceCollection, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        serviceCollection.Add(new ServiceDescriptor(typeof(ISteamOid2Client), typeof(SteamOid2Client), lifetime));
        serviceCollection.Add(new ServiceDescriptor(typeof(ISteamOid2WebRequestProvider), typeof(SystemNetHttpWebRequestProvider), ServiceLifetime.Transient));
        return serviceCollection;
    }

    /// <summary>
    /// Adds <see cref="ISteamOid2Client"/> and <see cref="ISteamOid2WebRequestProvider"/> to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add relevant services to.</param>
    /// <param name="lifetime">The lifetime of <see cref="ISteamOid2Client"/>, defaulting to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <typeparam name="TWebRequestProvider">A type of <see cref="ISteamOid2WebRequestProvider"/> to use instead of <see cref="SystemNetHttpWebRequestProvider"/>.</typeparam>
    /// <returns>The same collection for chaining.</returns>
    public static IServiceCollection AddSteamOid2<TWebRequestProvider>(this IServiceCollection serviceCollection, ServiceLifetime lifetime = ServiceLifetime.Singleton) where TWebRequestProvider : class, ISteamOid2WebRequestProvider
    {
        serviceCollection.Add(new ServiceDescriptor(typeof(ISteamOid2Client), typeof(SteamOid2Client), lifetime));
        serviceCollection.Add(new ServiceDescriptor(typeof(ISteamOid2WebRequestProvider), typeof(TWebRequestProvider), ServiceLifetime.Transient));
        return serviceCollection;
    }
}