using System.Net;
using MealWiz.Functions.Shared.Ssrf;
using Xunit;

namespace MealWiz.Functions.Tests.Shared.UnitTests.Ssrf;

public class IpAddressGuardTests
{
    private readonly IpAddressGuard _guard = new();

    [Theory]
    [InlineData("127.0.0.1")] // loopback
    [InlineData("10.0.0.5")] // private
    [InlineData("172.16.0.1")] // private
    [InlineData("172.31.255.255")] // private
    [InlineData("192.168.1.1")] // private
    [InlineData("169.254.169.254")] // cloud metadata endpoint
    [InlineData("169.254.0.1")] // link-local
    [InlineData("100.64.0.1")] // CGNAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")] // multicast
    [InlineData("255.255.255.255")] // broadcast
    [InlineData("192.0.2.1")] // TEST-NET-1
    [InlineData("198.51.100.1")] // TEST-NET-2
    [InlineData("203.0.113.1")] // TEST-NET-3
    [InlineData("198.18.0.1")] // benchmarking
    [InlineData("192.0.0.1")] // IETF protocol assignments
    [InlineData("240.0.0.1")] // reserved
    [InlineData("::")] // unspecified
    [InlineData("::1")] // loopback
    [InlineData("fec0::1")] // legacy site-local
    [InlineData("fe80::1")] // link-local
    [InlineData("fc00::1")] // unique local
    [InlineData("fd00::1")] // unique local
    [InlineData("ff02::1")] // multicast
    [InlineData("::ffff:169.254.169.254")] // IPv4-mapped metadata endpoint
    [InlineData("64:ff9b::a9fe:a9fe")] // NAT64-embedded metadata endpoint (169.254.169.254)
    [InlineData("64:ff9b::a00:1")] // NAT64-embedded private address (10.0.0.1)
    public void IsPubliclyRoutable_RejectsNonPublicAddresses(string address)
    {
        Assert.False(_guard.IsPubliclyRoutable(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.215.14")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("64:ff9b::0808:0808")] // NAT64-embedded 8.8.8.8
    [InlineData("172.15.255.255")] // just below 172.16.0.0/12
    [InlineData("172.32.0.0")] // just above 172.16.0.0/12
    [InlineData("100.63.255.255")] // just below 100.64.0.0/10
    [InlineData("100.128.0.0")] // just above 100.64.0.0/10
    [InlineData("11.0.0.0")] // just above 10.0.0.0/8
    public void IsPubliclyRoutable_AllowsPublicAddresses(string address)
    {
        Assert.True(_guard.IsPubliclyRoutable(IPAddress.Parse(address)));
    }
}
