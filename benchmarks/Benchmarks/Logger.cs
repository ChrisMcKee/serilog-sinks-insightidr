using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Serilog;
using Serilog.Sinks.InsightIDR;
using Serilog.Sinks.InsightOps;
using WaffleGenerator;

namespace Benchmark;

[Config(typeof(BenchmarkConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Method)]
public class LoggerBenchmark
{
    private static readonly string[] SharedData;

    static LoggerBenchmark()
    {
        SharedData = Enumerable.Range(0, 3)
                               .Select(_ => WaffleEngine.Text(paragraphs: 1, includeHeading: false))
                               .ToArray();
    }

    public IEnumerable<object[]> Data() => SharedData.Select(s => new object[] { s });

    readonly ILogger _classic;
    readonly ILogger _newAsyncLogger;

    public LoggerBenchmark()
    {
        // 3.1.0 NuGet package — InsightOpsSinkSettings + old Emit() path (new StringWriter per call)
        _classic = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.InsightOps(new InsightOpsSinkSettings
            {
                Token = "00000000-0000-0000-0000-000000000000",
                DataHubAddress = "localhost",
                DataHubPort = Program.FakeLogPort,
                IsUsingDataHub = true
            })
            .CreateLogger();

        // Project build — InsightIdrSinkSettings + new Emit() path (ThreadStatic StringWriter)
        _newAsyncLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.InsightIDR(new InsightIdrSinkSettings
            {
                Token = "00000000-0000-0000-0000-000000000000",
                Region = "us",
                DataHubAddress = "localhost",
                DataHubPort = Program.FakeLogPort,
                IsUsingDataHub = true
            })
            .CreateLogger();
    }

    [Benchmark(Baseline = true)]
    [ArgumentsSource(nameof(Data))]
    public void TestLog(string log)
    {
        _classic.Information("msg={Log}", log);
    }

    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public void TestLogNew(string log)
    {
        _newAsyncLogger.Information("msg={Log}", log);
    }
}
