using System.Text;
using Serilog.Sinks.InsightIDR.Rapid7;

namespace UnitTests;

public class Rapid7LineFormatterTests
{
    private static string Sep => Rapid7LineFormatter.LineSeparator;

    [Fact]
    public void Append_NoNewlines_CopiesSourceVerbatim()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "hello world");
        Assert.Equal("hello world", sb.ToString());
    }

    [Fact]
    public void Append_EmptyString_ProducesNothing()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "");
        Assert.Equal("", sb.ToString());
    }

    [Fact]
    public void Append_LineFeed_ReplacedWithLineSeparator()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "line1\nline2");
        Assert.Equal($"line1{Sep}line2", sb.ToString());
    }

    [Fact]
    public void Append_CarriageReturn_ReplacedWithLineSeparator()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "line1\rline2");
        Assert.Equal($"line1{Sep}line2", sb.ToString());
    }

    [Fact]
    public void Append_CRLF_ProducesSingleLineSeparator()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "line1\r\nline2");
        var result = sb.ToString();
        Assert.Equal($"line1{Sep}line2", result);
        Assert.DoesNotContain(Sep + Sep, result);
    }

    [Fact]
    public void Append_MultipleNewlines_AllReplaced()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "a\nb\r\nc\rd");
        Assert.Equal($"a{Sep}b{Sep}c{Sep}d", sb.ToString());
    }

    [Fact]
    public void Append_OnlyNewlines_ProducesOnlySeparators()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "\n\r\n\r");
        Assert.Equal($"{Sep}{Sep}{Sep}", sb.ToString());
    }

    [Fact]
    public void Append_NewlineAtStart_ReplacedCorrectly()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "\ntrailing");
        Assert.Equal($"{Sep}trailing", sb.ToString());
    }

    [Fact]
    public void Append_NewlineAtEnd_ReplacedCorrectly()
    {
        var sb = new StringBuilder();
        Rapid7LineFormatter.AppendWithNewlineReplacement(sb, "leading\n");
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
        Assert.True(Rapid7LineFormatter.CheckIfHostNameValid(hostName));
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
        Assert.False(Rapid7LineFormatter.CheckIfHostNameValid(hostName));
    }

    // -------------------------------------------------------------------------
    // ChunkMessage
    // -------------------------------------------------------------------------

    [Fact]
    public void ChunkMessage_ShortMessage_ReturnsSingleChunk()
    {
        var chunks = Rapid7LineFormatter.ChunkMessage("short message");
        Assert.Single(chunks);
        Assert.Equal("short message", chunks[0]);
    }

    [Fact]
    public void ChunkMessage_MessageAtExactLimit_ReturnsSingleChunk()
    {
        var exactLimit = new string('x', Rapid7LineFormatter.LogLengthLimit);
        var chunks = Rapid7LineFormatter.ChunkMessage(exactLimit);
        Assert.Single(chunks);
        Assert.Equal(exactLimit, chunks[0]);
    }

    [Fact]
    public void ChunkMessage_MessageOverLimit_IsSplitIntoMultipleChunks()
    {
        var oversized = new string('y', Rapid7LineFormatter.LogLengthLimit * 2 + 1);
        var chunks = Rapid7LineFormatter.ChunkMessage(oversized);
        Assert.Equal(3, chunks.Count);
        Assert.Equal(Rapid7LineFormatter.LogLengthLimit, chunks[0].Length);
        Assert.Equal(Rapid7LineFormatter.LogLengthLimit, chunks[1].Length);
        Assert.Equal(1, chunks[2].Length);
        Assert.Equal(oversized, string.Concat(chunks));
    }

    [Fact]
    public void ChunkMessage_ExceedsRecursionLimit_DropsRemainder()
    {
        var huge = new string('z', Rapid7LineFormatter.LogLengthLimit * (Rapid7LineFormatter.RecursionLimit + 1));
        var chunks = Rapid7LineFormatter.ChunkMessage(huge);
        Assert.Equal(Rapid7LineFormatter.RecursionLimit, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal(Rapid7LineFormatter.LogLengthLimit, chunk.Length));
    }

    [Fact]
    public void ChunkMessage_TrimsTrailingNewlines()
    {
        var chunks = Rapid7LineFormatter.ChunkMessage("hello\r\n");
        Assert.Single(chunks);
        Assert.Equal("hello", chunks[0]);
    }
}
