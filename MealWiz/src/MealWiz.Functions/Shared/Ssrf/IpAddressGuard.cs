using System.Net;
using System.Net.Sockets;

namespace MealWiz.Functions.Shared.Ssrf;

public interface IIpAddressGuard
{
    bool IsPubliclyRoutable(IPAddress address);
}

/// <summary>
/// Rejects private, loopback, link-local (including the 169.254.169.254 cloud metadata
/// endpoint), and other non-globally-routable address ranges, for both IPv4 and IPv6.
/// </summary>
public class IpAddressGuard : IIpAddressGuard
{
    public bool IsPubliclyRoutable(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPubliclyRoutableIPv4(address),
            AddressFamily.InterNetworkV6 => IsPubliclyRoutableIPv6(address),
            _ => false
        };
    }

    private static bool IsPubliclyRoutableIPv4(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return false; // 127.0.0.0/8

        var bytes = address.GetAddressBytes();

        if (bytes[0] == 0) return false; // 0.0.0.0/8 "this network"
        if (bytes[0] == 10) return false; // 10.0.0.0/8 private
        if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) return false; // 172.16.0.0/12 private
        if (bytes[0] == 192 && bytes[1] == 168) return false; // 192.168.0.0/16 private
        if (bytes[0] == 169 && bytes[1] == 254) return false; // 169.254.0.0/16 link-local, incl. 169.254.169.254 metadata
        if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) return false; // 100.64.0.0/10 CGNAT
        if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) return false; // 192.0.0.0/24 IETF protocol assignments
        if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) return false; // 192.0.2.0/24 TEST-NET-1
        if (bytes[0] == 198 && bytes[1] is 18 or 19) return false; // 198.18.0.0/15 benchmarking
        if (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) return false; // 198.51.100.0/24 TEST-NET-2
        if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) return false; // 203.0.113.0/24 TEST-NET-3
        if (bytes[0] >= 224) return false; // 224.0.0.0/4 multicast + 240.0.0.0/4 reserved + 255.255.255.255 broadcast

        return true;
    }

    private static bool IsPubliclyRoutableIPv6(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return false; // ::1
        if (address.Equals(IPAddress.IPv6Any)) return false; // ::
        if (address.IsIPv6LinkLocal) return false; // fe80::/10
        if (address.IsIPv6SiteLocal) return false; // legacy site-local fec0::/10
        if (address.IsIPv6Multicast) return false; // ff00::/8

        var bytes = address.GetAddressBytes();

        if ((bytes[0] & 0xFE) == 0xFC) return false; // fc00::/7 unique local

        // 64:ff9b::/96 NAT64 well-known prefix embeds an IPv4 address in the low 32 bits —
        // re-validate the embedded address so this can't be used to smuggle a private/metadata IP.
        if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xFF && bytes[3] == 0x9B &&
            bytes[4] == 0 && bytes[5] == 0 && bytes[6] == 0 && bytes[7] == 0 &&
            bytes[8] == 0 && bytes[9] == 0 && bytes[10] == 0 && bytes[11] == 0)
        {
            return IsPubliclyRoutableIPv4(new IPAddress(bytes[12..16]));
        }

        return true;
    }
}
