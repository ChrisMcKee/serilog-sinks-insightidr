using Serilog.Sinks.InsightIDR.Rapid7;

namespace UnitTests;

public class InsightTcpClientTests
{
    [Fact]
    public void Constructor_DataHubMode_UsesProvidedAddress()
    {
        var client = new InsightTcpClient(useSsl: true, useDataHub: true, serverAddr: "myhub.example.com", port: 9999, region: "us");
        Assert.Equal("myhub.example.com", client.ServerAddr);
    }

    [Fact]
    public void Constructor_DataHubMode_UsesProvidedPort()
    {
        var client = new InsightTcpClient(useSsl: false, useDataHub: true, serverAddr: "localhost", port: 12345, region: "eu");
        Assert.Equal(12345, client.TcpPort);
    }

    [Fact]
    public void Constructor_DataHubMode_DisablesTlsRegardlessOfFlag()
    {
        // DataHub does not support TLS — verify port is not the secure port (443)
        var client = new InsightTcpClient(useSsl: true, useDataHub: true, serverAddr: "localhost", port: 8080, region: "us");
        Assert.Equal(8080, client.TcpPort); // port from DataHub, not 443
    }

    [Fact]
    public void Constructor_Rapid7Mode_SslEnabled_UsesPort443()
    {
        var client = new InsightTcpClient(useSsl: true, useDataHub: false, serverAddr: "", port: 0, region: "eu");
        Assert.Equal(443, client.TcpPort);
    }

    [Fact]
    public void Constructor_Rapid7Mode_SslDisabled_UsesPort80()
    {
        var client = new InsightTcpClient(useSsl: false, useDataHub: false, serverAddr: "", port: 0, region: "eu");
        Assert.Equal(80, client.TcpPort);
    }

    [Theory]
    [InlineData("us", "us.data.logs.insight.rapid7.com")]
    [InlineData("eu", "eu.data.logs.insight.rapid7.com")]
    [InlineData("au", "au.data.logs.insight.rapid7.com")]
    [InlineData("ca", "ca.data.logs.insight.rapid7.com")]
    [InlineData("jp", "jp.data.logs.insight.rapid7.com")]
    public void Constructor_Rapid7Mode_FormatsServerAddressWithRegion(string region, string expectedAddress)
    {
        var client = new InsightTcpClient(useSsl: true, useDataHub: false, serverAddr: "", port: 0, region: region);
        Assert.Equal(expectedAddress, client.ServerAddr);
    }
}
