using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.InsightIDR;

namespace UnitTests;

public class InsightIdrSinkTests
{
    private sealed class StaticFormatter(string text) : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output) => output.Write(text);
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
    public void Constructor_ValidGuidToken_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            new InsightIdrSink(ValidDataHubSettings(), new StaticFormatter("x")));
        Assert.Null(ex);
    }

    [Fact]
    public void Emit_NullLogEvent_ThrowsArgumentNullException()
    {
        using var sink = new InsightIdrSink(ValidDataHubSettings(), new StaticFormatter("x"));
        Assert.Throws<ArgumentNullException>(() => sink.Emit(null!));
    }

    [Fact]
    public void Emit_ValidLogEvent_DoesNotThrow()
    {
        using var sink = new InsightIdrSink(ValidDataHubSettings(), new StaticFormatter("formatted"));
        var ex = Record.Exception(() => sink.Emit(MakeLogEvent()));
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_WithoutException()
    {
        var sink = new InsightIdrSink(ValidDataHubSettings(), new StaticFormatter("x"));
        var ex = Record.Exception(() =>
        {
            sink.Dispose();
            sink.Dispose();
        });
        Assert.Null(ex);
    }
}
