using System.Net;
using MealWiz.Functions.Shared.Ssrf;

namespace MealWiz.Functions.Tests.TestInfrastructure;

/// <summary>
/// Lets integration tests reach <see cref="RecipeTestServer"/> on 127.0.0.1, which the real guard
/// (correctly) blocks. Every non-loopback address still goes through the real guard, so redirects to
/// the metadata endpoint or private ranges are rejected exactly as they are in production.
/// </summary>
public class LoopbackAllowingIpAddressGuard(IIpAddressGuard inner) : IIpAddressGuard
{
    public bool IsPubliclyRoutable(IPAddress address) =>
        IPAddress.IsLoopback(address) || inner.IsPubliclyRoutable(address);
}
