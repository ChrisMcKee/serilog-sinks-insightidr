using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.InsightIDR;
using Serilog.Sinks.InsightIDR.Rapid7;

namespace UnitTests;

public class InsightIdrSinkTests
{
    private sealed class StaticFormatter(string text) : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output) => output.Write(text);
    }

    private sealed class FakeConnection : IInsightConnection
    {
        public List<byte[]> Writes { get; } = [];
        public int ConnectCount { get; private set; }
        public bool ThrowOnConnect { get; set; }
        public bool Disposed { get; private set; }

        public Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            ConnectCount++;
            return ThrowOnConnect ? Task.FromException(new IOException("boom")) : Task.CompletedTask;
        }

        public Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            Writes.Add(payload.ToArray());
            return Task.CompletedTask;
        }

        public void Dispose() => Disposed = true;
    }

    private static InsightIdrSinkSettings ValidDataHubSettings() => new()
    {
        Token = "00000000-0000-0000-0000-000000000000",
        Region = "eu",
        IsUsingDataHub = true,
        DataHubAddress = "localhost",
        DataHubPort = 1
    };

    private static LogEvent MakeLogEvent() =>
        new(DateTimeOffset.UtcNow, LogEventLevel.Information,
            null, new MessageTemplate("test", []), []);

    private static InsightIdrSink BuildSinkWithFake(InsightIdrSinkSettings settings, string formatted, out FakeConnection fake)
    {
        var connection = new FakeConnection();
        fake = connection;
        return new InsightIdrSink(settings, new StaticFormatter(formatted), () => connection);
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InsightIdrSink(null!, new StaticFormatter("x")));
    }

    [Fact]
    public void Constructor_NullToken_Throws()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            new InsightIdrSink(new InsightIdrSinkSettings { Token = null!, Region = "us" },
                new StaticFormatter("x")));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Constructor_WhitespaceToken_Throws()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            new InsightIdrSink(new InsightIdrSinkSettings { Token = "   ", Region = "us" },
                new StaticFormatter("x")));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Constructor_NonGuidToken_Throws()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            new InsightIdrSink(new InsightIdrSinkSettings { Token = "not-a-guid", Region = "us" },
                new StaticFormatter("x")));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Constructor_WhitespaceRegion_Throws()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            new InsightIdrSink(new InsightIdrSinkSettings { Token = "00000000-0000-0000-0000-000000000000", Region = "  " },
                new StaticFormatter("x")));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Constructor_ValidGuidToken_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            new InsightIdrSink(ValidDataHubSettings(), new StaticFormatter("x")));
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_DoesNotConnect()
    {
        var sink = BuildSinkWithFake(ValidDataHubSettings(), "x", out var fake);
        _ = sink;
        Assert.Equal(0, fake.ConnectCount);
    }

    [Fact]
    public async Task EmitBatchAsync_NullBatch_ThrowsArgumentNullException()
    {
        var sink = BuildSinkWithFake(ValidDataHubSettings(), "x", out _);
        await Assert.ThrowsAsync<ArgumentNullException>(() => sink.EmitBatchAsync(null!));
    }

    [Fact]
    public async Task EmitBatchAsync_SingleEvent_WritesOneLineToConnection()
    {
        var sink = BuildSinkWithFake(ValidDataHubSettings(), "formatted", out var fake);

        var ex = await Record.ExceptionAsync(() => sink.EmitBatchAsync([MakeLogEvent()]));

        Assert.Null(ex);
        Assert.Single(fake.Writes);
    }

    [Fact]
    public async Task EmitBatchAsync_MultipleEvents_WritesOneLinePerEvent()
    {
        var sink = BuildSinkWithFake(ValidDataHubSettings(), "formatted", out var fake);

        await sink.EmitBatchAsync([MakeLogEvent(), MakeLogEvent(), MakeLogEvent()]);

        Assert.Equal(3, fake.Writes.Count);
    }

    [Fact]
    public async Task EmitBatchAsync_NonDataHubMode_PrefixesLineWithToken()
    {
        var settings = new InsightIdrSinkSettings { Token = "11111111-1111-1111-1111-111111111111", Region = "eu" };
        var sink = BuildSinkWithFake(settings, "hello", out var fake);

        await sink.EmitBatchAsync([MakeLogEvent()]);

        var line = System.Text.Encoding.UTF8.GetString(fake.Writes[0]);
        Assert.StartsWith("11111111-1111-1111-1111-111111111111hello", line);
    }

    [Fact]
    public async Task EmitBatchAsync_TransportFailure_DisposesConnectionAndPropagates()
    {
        var sink = BuildSinkWithFake(ValidDataHubSettings(), "x", out var fake);
        fake.ThrowOnConnect = true;

        await Assert.ThrowsAsync<IOException>(() => sink.EmitBatchAsync([MakeLogEvent()]));

        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task OnEmptyBatchAsync_DoesNotThrow()
    {
        IBatchedLogEventSink sink = BuildSinkWithFake(ValidDataHubSettings(), "x", out _);
        var ex = await Record.ExceptionAsync(() => sink.OnEmptyBatchAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes_WithoutException()
    {
        var sink = BuildSinkWithFake(ValidDataHubSettings(), "x", out _);
        var ex = await Record.ExceptionAsync(async () =>
        {
            await sink.DisposeAsync();
            await sink.DisposeAsync();
        });
        Assert.Null(ex);
    }
}
