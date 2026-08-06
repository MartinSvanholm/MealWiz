using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace MealWiz.Functions.Shared.Ssrf;

public interface ISsrfGuardedHttpClientFactory
{
    HttpClient Create();
}

/// <summary>
/// Builds an <see cref="HttpClient"/> that pins every connection to a DNS-resolved IP address we've
/// validated ourselves, rather than trusting a hostname that the OS/HttpClient would resolve again
/// (independently) at connect time. Resolving once and connecting to that exact address closes the
/// classic DNS-rebinding SSRF bypass: an attacker's DNS record can't return a public IP for our check
/// and a private one a moment later, because there is no second lookup.
/// </summary>
public class SsrfGuardedHttpClientFactory(IIpAddressGuard ipAddressGuard) : ISsrfGuardedHttpClientFactory
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = ConnectTimeout,
            ConnectCallback = ConnectAsync
        };

        return new HttpClient(handler)
        {
            Timeout = RequestTimeout
        };
    }

    // SocketsHttpHandler.ConnectCallback's signature is fixed by the BCL: return a connected Stream or
    // throw. There's no Result-returning hook to plug into here, so this is the one place in the project
    // that necessarily signals failure via an exception rather than a Result — everywhere that calls
    // into this (RecipeFetcher's Result.Try around SendAsync) still surfaces it as a Result, same as any
    // other exception the networking stack might throw (DNS failure, connection refused, TLS error).
    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        IPAddress[] resolved;
        try
        {
            resolved = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Could not resolve host '{host}'.", ex);
        }

        var validated = resolved.FirstOrDefault(ipAddressGuard.IsPubliclyRoutable);
        if (validated is null)
        {
            throw new HttpRequestException($"Host '{host}' did not resolve to a publicly routable address.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(validated, port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
