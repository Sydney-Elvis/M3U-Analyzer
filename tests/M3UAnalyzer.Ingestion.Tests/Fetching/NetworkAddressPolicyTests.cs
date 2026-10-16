using System.Net;
using M3UAnalyzer.Ingestion.Fetching;

namespace M3UAnalyzer.Ingestion.Tests.Fetching;

[TestClass]
public sealed class NetworkAddressPolicyTests
{
    [TestMethod]
    [DataRow("8.8.8.8", true)]
    [DataRow("1.1.1.1", true)]
    [DataRow("172.32.0.1", true)]
    [DataRow("100.128.0.1", true)]
    [DataRow("198.20.0.1", true)]
    [DataRow("2606:4700:4700::1111", true)]
    [DataRow("::ffff:8.8.8.8", true)]
    [DataRow("127.0.0.1", false)]
    [DataRow("10.1.2.3", false)]
    [DataRow("172.16.0.1", false)]
    [DataRow("172.31.255.255", false)]
    [DataRow("192.168.1.10", false)]
    [DataRow("100.64.0.1", false)]
    [DataRow("100.127.255.255", false)]
    [DataRow("169.254.169.254", false)]
    [DataRow("0.0.0.0", false)]
    [DataRow("224.0.0.1", false)]
    [DataRow("240.0.0.1", false)]
    [DataRow("255.255.255.255", false)]
    [DataRow("192.0.0.8", false)]
    [DataRow("192.0.2.5", false)]
    [DataRow("198.18.0.1", false)]
    [DataRow("198.19.255.255", false)]
    [DataRow("198.51.100.7", false)]
    [DataRow("203.0.113.9", false)]
    [DataRow("::1", false)]
    [DataRow("::", false)]
    [DataRow("fe80::1", false)]
    [DataRow("fd00::1", false)]
    [DataRow("fc00::1", false)]
    [DataRow("2001:db8::1", false)]
    [DataRow("::ffff:127.0.0.1", false)]
    [DataRow("64:ff9b::808:808", false)]
    [DataRow("64:ff9b:1::a00:1", false)]
    [DataRow("ff02::1", false)]
    [DataRow("fec0::1", false)]
    public void IsPublicAddress_ClassifiesAddress(string address, bool expected)
    {
        Assert.AreEqual(expected, NetworkAddressPolicy.IsPublicAddress(IPAddress.Parse(address)), address);
    }

    [TestMethod]
    public void IsPublicAddress_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => NetworkAddressPolicy.IsPublicAddress(null!));
    }
}
