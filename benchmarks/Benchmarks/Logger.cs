using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Serilog;
using Serilog.Sinks.InsightOps;
using WaffleGenerator;

namespace Benchmark;

[Config(typeof(BenchmarkConfig))]
public class LoggerBenchmark
{
    public IEnumerable<object[]> Data()
    {
        for (int i = 0; i < 3; i++)
        {
            var text = WaffleEngine.Text(paragraphs: 1, includeHeading: false);
            yield return [text];
        }
    }

    readonly ILogger _classic;
    readonly ILogger _newAsyncLogger;

    public LoggerBenchmark()
    {
        _classic = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.InsightOps(new InsightOpsSinkSettings()
            {
                Token = "00000000-0000-0000-0000-000000000000",
                DataHubAddress = "localhost",
                DataHubPort = Program.FakeLogPort,
                IsUsingDataHub = true
            })
            .CreateLogger();

        // Create our logger.
        _newAsyncLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.InsightOps(new InsightOpsSinkSettings
            {
                Token = "00000000-0000-0000-0000-000000000000",
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
