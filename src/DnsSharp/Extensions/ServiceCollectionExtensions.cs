using DnsSharp.Abstractions;
using DnsSharp.Caching;
using DnsSharp.Options;
using DnsSharp.Resilience;
using DnsSharp.Services;
using DnsSharp.Telemetry;
using DnsSharp.Transports;
using DnsSharp.Wire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DnsSharp.Extensions;

/// <summary>
/// Dependency injection extensions for DnsSharp.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers DnsSharp core services, transports, cache, and options.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional DNS options configuration callback.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddDnsSharp(this IServiceCollection services, Action<DnsOptions>? configure = null)
    {
        services.AddOptions<DnsOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddOptions<DnsTelemetryOptions>();
        services.TryAddSingleton<DnsTelemetry>();

        services.AddMemoryCache();
        services.TryAddSingleton<IDnsCache, MemoryDnsCache>();
        services.TryAddSingleton<IDnsWireCodec, DnsWireCodec>();
        services.TryAddSingleton<IDnsResilienceStrategy, NoOpDnsResilienceStrategy>();

        // Register default transports with stable names (udp/tcp/doh).
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDnsTransport, UdpDnsTransport>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDnsTransport, TcpDnsTransport>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDnsTransport, DohDnsTransport>());

        services.AddHttpClient("DnsSharp.DoH")
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);

        services.TryAddSingleton<IDnsResolver, DnsResolver>();
        return services;
    }

    /// <summary>
    /// Registers a custom DNS transport implementation.
    /// </summary>
    /// <typeparam name="TTransport">Transport type implementing <see cref="IDnsTransport"/>.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddDnsTransport<TTransport>(this IServiceCollection services)
        where TTransport : class, IDnsTransport
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDnsTransport, TTransport>());
        return services;
    }

    /// <summary>
    /// Enables optional Polly-based resilience behavior for DNS operations.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Resilience options configuration callback.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddDnsSharpPolly(this IServiceCollection services, Action<DnsPollyOptions>? configure = null)
    {
        services.AddOptions<DnsPollyOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IDnsResilienceStrategy, PollyDnsResilienceStrategy>();
        return services;
    }

    /// <summary>
    /// Configures DnsSharp tracing and metrics hooks.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Telemetry options callback.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddDnsSharpOpenTelemetry(this IServiceCollection services, Action<DnsTelemetryOptions>? configure = null)
    {
        services.AddOptions<DnsTelemetryOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<DnsTelemetry>();
        return services;
    }
}
