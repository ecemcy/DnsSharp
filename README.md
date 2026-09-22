# DnsSharp

DnsSharp is a modern .NET DNS client library focused on practical resolver behavior with dependency injection support.

## Features

- DI-first public API (`IDnsResolver`, `IDnsTransport`, `IDnsCache`)
- Defensive RFC-aware wire codec:
  - compressed names
  - question/answer/authority/additional parsing
  - EDNS OPT support
  - safe unknown-record handling with preserved raw bytes
- Record type support:
  - Common: `A`, `AAAA`, `NS`, `CNAME`, `SOA`, `PTR`, `MX`, `TXT`, `SRV`, `CAA`
  - DNSSEC parsing: `DS`, `RRSIG`, `DNSKEY` (parsing only; no cryptographic chain validation)
- Transports:
  - UDP DNS
  - TCP DNS
  - DNS-over-HTTPS (DoH) via `HttpClientFactory` with configurable endpoint
- Resolver behavior:
  - timeout + cancellation token support
  - deterministic transport order
  - server fallback
  - TCP fallback for truncated UDP responses
- In-memory TTL-aware caching with negative caching
- Optional Polly-based retry + circuit breaker integration
- OpenTelemetry hooks (activity source + meter)

## Target framework

- .NET 8 (`net8.0`)

## Installation

### From source

```bash
git clone https://github.com/ecemcy/DnsSharp.git
cd DnsSharp
dotnet build DnsSharp.slnx
```

### Package references used by the library

- `Microsoft.Extensions.Caching.Memory`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Http`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Options`
- `Polly`

## Project structure

- `src/DnsSharp/Abstractions` → public contracts
- `src/DnsSharp/Models` → query/response/record models
- `src/DnsSharp/Options` → configurable options
- `src/DnsSharp/Transports` → UDP/TCP/DoH implementations
- `src/DnsSharp/Wire` → wire encoder/decoder
- `src/DnsSharp/Caching` → in-memory cache
- `src/DnsSharp/Services` → resolver orchestration
- `src/DnsSharp/Extensions` → DI registration

## Dependency injection setup

```csharp
using DnsSharp.Extensions;
using DnsSharp.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services
    .AddDnsSharp(options =>
    {
        options.Servers.Clear();
        options.Servers.Add("1.1.1.1:53");
        options.Servers.Add("8.8.8.8:53");

        options.TransportOrder.Clear();
        options.TransportOrder.Add("udp");
        options.TransportOrder.Add("tcp");

        options.QueryTimeoutMs = 3000;
        options.EnableCaching = true;
        options.EnableTcpFallback = true;
        options.EnableEdns = true;
    })
    .AddDnsSharpPolly(polly =>
    {
        polly.RetryCount = 2;
        polly.RetryBaseDelayMs = 150;
        polly.CircuitBreakerFailures = 8;
        polly.CircuitBreakerDurationSeconds = 20;
    })
    .AddDnsSharpOpenTelemetry(telemetry =>
    {
        telemetry.EnableTracing = true;
        telemetry.EnableMetrics = true;
    });

var provider = services.BuildServiceProvider();
var resolver = provider.GetRequiredService<DnsSharp.Abstractions.IDnsResolver>();
var response = await resolver.QueryAsync("example.com", RecordType.A);
```

## Resolver usage

```csharp
var aRecords = response.Answers
    .Where(r => r.Type == RecordType.A)
    .Select(r => r.Data)
    .ToList();
```

## DNS-over-HTTPS configuration

```csharp
services.AddDnsSharp(options =>
{
    options.DohEndpoint = "https://dns.google/dns-query";
    options.DohUseGet = false; // POST by default
    options.TransportOrder.Clear();
    options.TransportOrder.Add("doh");
});
```

## Custom transport/cache extension points

- Implement `IDnsTransport` and register with:

```csharp
services.AddDnsTransport<MyCustomTransport>();
```

- Implement `IDnsCache` and replace default registration in DI.

## OpenTelemetry integration

DnsSharp emits:

- Activity source: `DnsSharp` (`DnsTelemetryConventions.ActivitySourceName`)
- Meter: `DnsSharp` (`DnsTelemetryConventions.MeterName`)

Use your own OpenTelemetry setup to subscribe to those names.

## Polly integration notes

Polly is optional. If not configured, DnsSharp uses a no-op resilience strategy.

## Limitations

- DNSSEC record parsing is supported, but cryptographic validation and trust-chain validation are not implemented.
- The library does not include DNS-over-TLS or DNS-over-QUIC transports.
- The built-in cache is process-local memory cache.

## Build instructions

```bash
dotnet restore DnsSharp.slnx
dotnet build DnsSharp.slnx -c Release
```

## Development notes

- Public API includes XML documentation.
- Wire-format and transport code includes inline comments around DNS-specific behavior.
