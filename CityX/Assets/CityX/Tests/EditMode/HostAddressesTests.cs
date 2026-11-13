using System.Collections.Generic;
using CityX.Net;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The Host/Join panel's line of this computer's addresses (docs/PLAYING_ONLINE.md).</summary>
    public sealed class HostAddressesTests
    {
        [Test]
        public void EveryNetworkIsListedWithItsName()
        {
            var found = new List<(string, string)> { ("Wi-Fi", "192.168.1.20"), ("Radmin VPN", "26.10.20.30") };
            string line = HostAddresses.Describe(found);
            StringAssert.Contains("192.168.1.20 (Wi-Fi)", line);
            StringAssert.Contains("26.10.20.30 (Radmin VPN)", line);
        }

        [Test]
        public void NoNetworkSaysNobodyCanJoin()
        {
            StringAssert.Contains("nobody can join", HostAddresses.Describe(new List<(string, string)>()));
        }

        [Test]
        public void TheLineStaysShort()
        {
            var found = new List<(string, string)>();
            for (int i = 0; i < 6; i++) found.Add(($"net{i}", $"10.0.0.{i}"));
            string line = HostAddresses.Describe(found);
            StringAssert.Contains($"10.0.0.{HostAddresses.MaxListed - 1}", line);
            StringAssert.DoesNotContain($"10.0.0.{HostAddresses.MaxListed}", line);
        }

        [Test]
        public void LoopbackAndSelfAssignedAddressesAreLeftOut()
        {
            Assert.IsFalse(HostAddresses.Usable("127.0.0.1"));
            Assert.IsFalse(HostAddresses.Usable("169.254.3.4"));
            Assert.IsTrue(HostAddresses.Usable("192.168.0.4"));
            Assert.IsTrue(HostAddresses.Usable("26.1.2.3"));
        }

        [Test]
        public void ThisComputersAddressesCanBeFound()
        {
            foreach ((string network, string address) in HostAddresses.Find())
            {
                Assert.IsTrue(HostAddresses.Usable(address), $"{network} gave {address}");
            }
        }
    }
}
