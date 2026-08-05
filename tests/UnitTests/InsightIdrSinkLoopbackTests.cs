using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.InsightIDR;

namespace UnitTests;

/// <summary>
/// Exercises the real <see cref="Serilog.Sinks.InsightIDR.Rapid7.InsightTcpClient"/> against a loopback
/// listener, since <see cref="InsightIdrSinkTests"/> only exercises the sink against a fake connection.
/// </summary>
public class InsightIdrSinkLoopbackTests
{
    private sealed class StaticFormatter(string text) : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output) => output.Write(text);
    }

    private static LogEvent MakeLogEvent() =>
        new(DateTimeOffset.UtcNow, LogEventLevel.Information,
            null, new MessageTemplate("test", []), []);

    [Fact]
    public async Task EmitBatchAsync_DataHubMode_DeliversLineOverRealSocket()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var acceptTask = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask();

        var settings = new InsightIdrSinkSettings
        {
            Token = "00000000-0000-0000-0000-000000000000",
            Region = "eu",
            IsUsingDataHub = true,
            DataHubAddress = "127.0.0.1",
            DataHubPort = port
        };

        await using var sink = new InsightIdrSink(settings, new StaticFormatter("hello from loopback"));

        await sink.EmitBatchAsync([MakeLogEvent()]);

        using var serverClient = await acceptTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var stream = serverClient.GetStream();
        var buffer = new byte[1024];
        var read = await stream.ReadAsync(buffer, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var received = Encoding.UTF8.GetString(buffer, 0, read);

        // DataHub mode: no token prefix, message terminated with a newline.
        Assert.Equal("hello from loopback\n", received);

        listener.Stop();
    }
}
