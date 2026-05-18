using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Serilog.Sinks.InsightIDR.Rapid7;
using WaffleGenerator;

namespace Benchmark;

[Config(typeof(BenchmarkConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Method)]
public class AsyncClientBenchmark
{
    // Generated once so TestLog and TestLogNew receive identical strings.
    private static readonly string[] SharedData;

    static AsyncClientBenchmark()
    {
        SharedData = Enumerable.Range(0, 3)
            .Select(_ => WaffleEngine.Text(paragraphs: 1, includeHeading: false))
            .ToArray();
    }

    public IEnumerable<object[]> Data() => SharedData.Select(s => new object[] { s });

    readonly InsightCore.Net.AsyncLogger _classic;
    readonly Serilog.Sinks.InsightIDR.Rapid7.AsyncLogger _newAsyncLogger;

    public AsyncClientBenchmark()
    {
        _classic = new InsightCore.Net.AsyncLogger();
        _classic.setDataHubAddr("localhost");
        _classic.setDataHubPort(Program.FakeLogPort);
        _classic.setIsUsingDataHub(true);

        _newAsyncLogger = new AsyncLogger();
        _newAsyncLogger.SetDataHubAddr("localhost");
        _newAsyncLogger.SetDataHubPort(Program.FakeLogPort);
        _newAsyncLogger.SetIsUsingDataHub(true);
    }

    [Benchmark(Baseline = true)]
    [ArgumentsSource(nameof(Data))]
    public void TestLog(string log)
    {
        _classic.AddLine(log);
    }

    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public void TestLogNew(string log)
    {
        _newAsyncLogger.QueueLogEvent(log);
    }
}
