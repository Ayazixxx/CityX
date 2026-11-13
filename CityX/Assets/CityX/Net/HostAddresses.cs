using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CityX.Net
{
    /// <summary>
    /// The addresses a friend can join this computer at, shown on the title screen's Host/Join panel (owner, 2026-11-12, before a first online
    /// duel: "i want to 1v1 my friend in a duel"; docs/PLAYING_ONLINE.md). Every network that is up: the home one (192.168...) and a gaming VPN's
    /// (Radmin's 26..., Tailscale's 100...), each with its adapter's name so the right one can be read out. A public internet address is not
    /// known here (it needs a site outside to tell it), so the doc says how to find it.
    /// </summary>
    public static class HostAddresses
    {
        /// <summary>The most addresses listed: the panel's line is short.</summary>
        public const int MaxListed = 3;

        /// <summary>This computer's IPv4 addresses on networks that are up, loopback and self-assigned (169.254...) ones left out.</summary>
        public static List<(string Network, string Address)> Find()
        {
            var found = new List<(string, string)>();
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }
                    foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && Usable(unicast.Address.ToString()))
                        {
                            found.Add((adapter.Name, unicast.Address.ToString()));
                        }
                    }
                }
            }
            catch (NetworkInformationException)
            {
                // No list of networks: nothing to show, the panel says so.
            }
            return found;
        }

        /// <summary>Not loopback, not a self-assigned address a network never gave out.</summary>
        public static bool Usable(string address) => !address.StartsWith("127.") && !address.StartsWith("169.254.");

        /// <summary>The line the panel shows.</summary>
        public static string Describe(IReadOnlyList<(string Network, string Address)> found)
        {
            if (found.Count == 0)
            {
                return "No network found: nobody can join this computer.";
            }
            var parts = new List<string>();
            for (int i = 0; i < found.Count && i < MaxListed; i++)
            {
                parts.Add($"{found[i].Address} ({found[i].Network})");
            }
            return "To host, friends join you at: " + string.Join(", ", parts);
        }
    }
}
