using System.ComponentModel;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Sinks.InsightIDR;

namespace UnitTests;

public class LoggerSinkConfigurationExtensionsTests
{
    private static InsightIdrSinkSettings DataHubSettings() => new()
    {
        Token = "00000000-0000-0000-0000-000000000000",
        Region = "eu",
        IsUsingDataHub = true,
        DataHubAddress = "localhost",
        DataHubPort = 1
    };

    [Fact]
    public void InsightIDR_InvalidLogLevel_ThrowsInvalidEnumArgumentException()
    {
        Assert.Throws<InvalidEnumArgumentException>(() =>
            new LoggerConfiguration()
                .WriteTo.InsightIDR(DataHubSettings(), formatter: (ITextFormatter?)null, restrictedToMinimumLevel: (LogEventLevel)999));
    }

    [Fact]
    public void InsightIDR_NullFormatter_UsesDefaultFormatterWithoutException()
    {
        var ex = Record.Exception(() =>
            new LoggerConfiguration()
                .WriteTo.InsightIDR(DataHubSettings(), formatter: null)
                .CreateLogger());
        Assert.Null(ex);
    }

    [Fact]
    public void InsightIDR_CustomFormatter_IsAccepted()
    {
        ITextFormatter custom = new MessageTemplateTextFormatter("{Message}");
        var ex = Record.Exception(() =>
            new LoggerConfiguration()
                .WriteTo.InsightIDR(DataHubSettings(), formatter: custom)
                .CreateLogger());
        Assert.Null(ex);
    }

    [Fact]
    public void InsightIDR_EmptyOutputTemplate_FallsBackToDefaultWithoutException()
    {
        var ex = Record.Exception(() =>
            new LoggerConfiguration()
                .WriteTo.InsightIDR(DataHubSettings(), outputTemplate: "")
                .CreateLogger());
        Assert.Null(ex);
    }

    [Fact]
    public void InsightIDR_WhitespaceOutputTemplate_FallsBackToDefaultWithoutException()
    {
        var ex = Record.Exception(() =>
            new LoggerConfiguration()
                .WriteTo.InsightIDR(DataHubSettings(), outputTemplate: "   ")
                .CreateLogger());
        Assert.Null(ex);
    }

    [Fact]
    public void InsightIDR_NullOptionalStrings_MapsToEmptyStrings()
    {
        var ex = Record.Exception(() =>
            new LoggerConfiguration()
                .WriteTo.InsightIDR(
                    token: "00000000-0000-0000-0000-000000000000",
                    region: "eu",
                    isUsingDataHub: true,
                    dataHubAddress: null,
                    dataHubPort: 1,
                    hostName: null,
                    logId: null)
                .CreateLogger());
        Assert.Null(ex);
    }

    [Fact]
    public void InsightIDR_ValidSettings_ReturnsLoggerConfiguration()
    {
        var config = new LoggerConfiguration()
            .WriteTo.InsightIDR(DataHubSettings());
        Assert.NotNull(config);
    }
}
