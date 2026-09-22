using System.Net;

namespace DnsSharp.Transports;

internal static class EndpointParser
{
    public static (string Host, int Port) Parse(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint is required.", nameof(endpoint));
        }

        if (IPEndPoint.TryParse(endpoint, out var ipEndPoint))
        {
            return (ipEndPoint.Address.ToString(), ipEndPoint.Port);
        }

        var colonIndex = endpoint.LastIndexOf(':');
        if (colonIndex > 0 && int.TryParse(endpoint[(colonIndex + 1)..], out var parsedPort))
        {
            return (endpoint[..colonIndex], parsedPort);
        }

        return (endpoint, 53);
    }
}
