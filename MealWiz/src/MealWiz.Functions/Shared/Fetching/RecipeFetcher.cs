using System.Net;
using System.Text;
using FluentResults;
using MealWiz.Functions.Shared.Ssrf;

namespace MealWiz.Functions.Shared.Fetching;

public interface IRecipeFetcher
{
    Task<Result<string>> FetchAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>
/// Fetches a page through the SSRF-guarded <see cref="HttpClient"/>. Redirects are followed manually
/// (up to <see cref="MaxRedirects"/> hops) so every hop's scheme and resolved IP get re-validated —
/// nothing is auto-followed. The response body is read into a size-capped buffer rather than trusting
/// a (possibly absent or spoofed) Content-Length header.
///
/// Every fallible step returns a <see cref="Result{T}"/> instead of throwing — matching the main
/// project's convention (<c>Result.Try(...)</c> around every Supabase call). The only exceptions ever
/// in play here are ones the .NET networking stack itself can throw (DNS/socket/TLS failures), and
/// those are caught right at the <c>Result.Try</c> boundary around each I/O call, never propagated.
/// </summary>
public class RecipeFetcher(ISsrfGuardedHttpClientFactory httpClientFactory) : IRecipeFetcher
{
    private const int MaxRedirects = 5;
    private const long MaxResponseBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps];

    public async Task<Result<string>> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.Create();

        var currentUrl = url;

        for (var redirectCount = 0; ; redirectCount++)
        {
            if (!AllowedSchemes.Contains(currentUrl.Scheme, StringComparer.OrdinalIgnoreCase))
            {
                return Result.Fail($"URL scheme '{currentUrl.Scheme}' is not allowed.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            var responseResult = await Result.Try(() =>
                client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken));

            if (responseResult.IsFailed)
            {
                return Result.Fail<string>(responseResult.Errors);
            }

            using var response = responseResult.Value;

            if (IsRedirect(response.StatusCode))
            {
                if (redirectCount >= MaxRedirects)
                {
                    return Result.Fail("Too many redirects.");
                }

                if (response.Headers.Location is not { } location)
                {
                    return Result.Fail("Redirect response had no Location header.");
                }

                currentUrl = location.IsAbsoluteUri ? location : new Uri(currentUrl, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result.Fail($"Request failed with status {(int)response.StatusCode}.");
            }

            return await ReadBodyWithSizeCapAsync(response, cancellationToken);
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Found or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private static async Task<Result<string>> ReadBodyWithSizeCapAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var streamResult = await Result.Try(() => response.Content.ReadAsStreamAsync(cancellationToken));
        if (streamResult.IsFailed)
        {
            return Result.Fail<string>(streamResult.Errors);
        }

        await using var stream = streamResult.Value;
        using var buffer = new MemoryStream();

        var chunk = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var readResult = await Result.Try(() => stream.ReadAsync(chunk, cancellationToken).AsTask());
            if (readResult.IsFailed)
            {
                return Result.Fail<string>(readResult.Errors);
            }

            var bytesRead = readResult.Value;
            if (bytesRead == 0) break;

            totalRead += bytesRead;
            if (totalRead > MaxResponseBytes)
            {
                return Result.Fail($"Response exceeded the {MaxResponseBytes}-byte cap.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }

        return Result.Ok(GetEncoding(response).GetString(buffer.ToArray()));
    }

    private static Encoding GetEncoding(HttpResponseMessage response)
    {
        var charset = response.Content.Headers.ContentType?.CharSet;
        if (string.IsNullOrWhiteSpace(charset)) return Encoding.UTF8;

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
