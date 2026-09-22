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

        if (endpoint.StartsWith("[", StringComparison.Ordinal))
        {
            var closing = endpoint.IndexOf(']');
            if (closing > 0)
            {
                var host = endpoint[1..closing];
                if (closing + 1 < endpoint.Length && endpoint[closing + 1] == ':' && int.TryParse(endpoint[(closing + 2)..], out var bracketPort))
                {
                    return (host, bracketPort);
                }

                return (host, 53);
            }
        }

        if (IPAddress.TryParse(endpoint, out _))
        {
            return (endpoint, 53);
        }

        if (IPEndPoint.TryParse(endpoint, out var ipEndPoint))
        {
            return (ipEndPoint.Address.ToString(), ipEndPoint.Port);
        }

        if (endpoint.Count(c => c == ':') == 1)
        {
            var colonIndex = endpoint.LastIndexOf(':');
            if (colonIndex > 0 && int.TryParse(endpoint[(colonIndex + 1)..], out var parsedPort))
            {
                return (endpoint[..colonIndex], parsedPort);
            }
        }

        return (endpoint, 53);
    }
}
