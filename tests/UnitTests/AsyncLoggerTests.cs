using System.Text;
using Serilog.Sinks.InsightIDR.Rapid7;

namespace UnitTests;

public class AsyncLoggerTests
{
    private static string Sep => AsyncLogger.LineSeparator;

    [Fact]
    public void Append_NoNewlines_CopiesSourceVerbatim()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "hello world");
        Assert.Equal("hello world", sb.ToString());
    }

    [Fact]
    public void Append_EmptyString_ProducesNothing()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "");
        Assert.Equal("", sb.ToString());
    }

    [Fact]
    public void Append_LineFeed_ReplacedWithLineSeparator()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "line1\nline2");
        Assert.Equal($"line1{Sep}line2", sb.ToString());
    }

    [Fact]
    public void Append_CarriageReturn_ReplacedWithLineSeparator()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "line1\rline2");
        Assert.Equal($"line1{Sep}line2", sb.ToString());
    }

    [Fact]
    public void Append_CRLF_ProducesSingleLineSeparator()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "line1\r\nline2");
        var result = sb.ToString();
        Assert.Equal($"line1{Sep}line2", result);
        Assert.DoesNotContain(Sep + Sep, result);
    }

    [Fact]
    public void Append_MultipleNewlines_AllReplaced()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "a\nb\r\nc\rd");
        Assert.Equal($"a{Sep}b{Sep}c{Sep}d", sb.ToString());
    }

    [Fact]
    public void Append_OnlyNewlines_ProducesOnlySeparators()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "\n\r\n\r");
        Assert.Equal($"{Sep}{Sep}{Sep}", sb.ToString());
    }

    [Fact]
    public void Append_NewlineAtStart_ReplacedCorrectly()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "\ntrailing");
        Assert.Equal($"{Sep}trailing", sb.ToString());
    }

    [Fact]
    public void Append_NewlineAtEnd_ReplacedCorrectly()
    {
        var sb = new StringBuilder();
        AsyncLogger.AppendWithNewlineReplacement(sb, "leading\n");
        Assert.Equal($"leading{Sep}", sb.ToString());
    }

    // -------------------------------------------------------------------------
    // CheckIfHostNameValid
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("validhost")]
    [InlineData("my-server")]
    [InlineData("server01")]
    [InlineData("")]
    public void HostName_Valid_ReturnsTrue(string hostName)
    {
        Assert.True(AsyncLogger.CheckIfHostNameValid(hostName));
    }

    [Theory]
    [InlineData("host name")]
    [InlineData("host/name")]
    [InlineData("host\\name")]
    [InlineData("host[0]")]
    [InlineData("host:8080")]
    [InlineData("host;more")]
    [InlineData("host|pipe")]
    [InlineData("host<tag>")]
    [InlineData("host+name")]
    [InlineData("host=name")]
    [InlineData("host,name")]
    [InlineData("host?q")]
    [InlineData("host*wild")]
    [InlineData("host_under")]
    public void HostName_WithForbiddenChar_ReturnsFalse(string hostName)
    {
        Assert.False(AsyncLogger.CheckIfHostNameValid(hostName));
    }

    // QueueLogEvent chunking (producer thread only, no network required)

    private static AsyncLogger BuildDataHubLogger()
    {
        var logger = new AsyncLogger();
        logger.SetIsUsingDataHub(true);
        logger.SetDataHubAddr("localhost");
        logger.SetDataHubPort(1);
        return logger;
    }

    [Fact]
    public void QueueLogEvent_ShortMessage_DoesNotThrow()
    {
        var logger = BuildDataHubLogger();
        var ex = Record.Exception(() => logger.QueueLogEvent("short message"));
        Assert.Null(ex);
    }

    [Fact]
    public void QueueLogEvent_MessageAtExactLimit_DoesNotThrow()
    {
        var logger = BuildDataHubLogger();
        var exactLimit = new string('x', 65536);
        var ex = Record.Exception(() => logger.QueueLogEvent(exactLimit));
        Assert.Null(ex);
    }

    [Fact]
    public void QueueLogEvent_MessageOverLimit_IsChunkedWithoutException()
    {
        var logger = BuildDataHubLogger();
        var oversized = new string('y', 65536 * 2 + 1);
        var ex = Record.Exception(() => logger.QueueLogEvent(oversized));
        Assert.Null(ex);
    }

    [Fact]
    public void QueueLogEvent_ExceedsRecursionLimit_DropsRemainderWithoutException()
    {
        var logger = BuildDataHubLogger();
        var huge = new string('z', 65536 * 33);
        var ex = Record.Exception(() => logger.QueueLogEvent(huge));
        Assert.Null(ex);
    }
}
