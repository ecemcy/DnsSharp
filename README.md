# DnsSharp

DnsSharp is a modern, DI-friendly DNS client library for .NET. It supports UDP/TCP, DNS-over-HTTPS scaffolding, EDNS, DNSSEC record parsing, TTL-aware caching, Polly resilience policies, and OpenTelemetry instrumentation.

## Features
- **Dependency Injection** friendly (`IDnsResolver`, `IDnsTransport`, `IDnsCache`)
- **RFC-aware wire format**: name compression, EDNS OPT, DNSSEC RRs (RRSIG, DNSKEY, DS), and parsing for common RR types
- **Transports**: UDP, TCP, DoH scaffold
- **Caching**: TTL-aware in-memory cache with negative caching
- **Resilience**: Polly retry and circuit-breaker policies configurable via DI
- **Observability**: OpenTelemetry spans and metrics hooks
- **Extensible**: Add custom transports, caches, or record parsers

## Quickstart

### Install
Add project to your solution or package as a NuGet package.

### Required NuGet packages
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.Logging`
- `Polly`
- `Polly.Extensions.Http`
- `OpenTelemetry`
- `OpenTelemetry.Exporter.Console` (or your preferred exporter)
- `OpenTelemetry.Instrumentation.AspNetCore` (optional)
- `System.Buffers` (if needed)

### DI registration example
```csharp
services.AddDnsSharp(options =>
{
    options.Servers.Add("8.8.8.8");
    options.Servers.Add("1.1.1.1");
    options.EnableCaching = true;
    options.DefaultTtlSeconds = 300;
    options.QueryTimeoutMs = 2000;
})
.AddPollyPolicies(polly =>
{
    polly.RetryCount = 3;
    polly.CircuitBreakerFailures = 5;
    polly.CircuitBreakerDurationSeconds = 30;
})
.AddOpenTelemetry(otel =>
{
    otel.ServiceName = "MyService";
    otel.EnableConsoleExporter = true;
});
```

### Usage
```
var resolver = serviceProvider.GetRequiredService<IDnsResolver>();
var resp = await resolver.QueryAsync("example.com", RecordType.A);
```

### Extending
- Implement IDnsTransport to add custom transports (DoT, QUIC)
- Implement IDnsCache to add Redis or IMemoryCache adapters
- Add record parsers by extending DnsWireFormat parsing switch
