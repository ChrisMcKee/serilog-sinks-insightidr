using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;

namespace Benchmark;

public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        this.ArtifactsPath = Path.Combine(Path.GetTempPath(), "bdn");

        var baseConfig = Job.ShortRun.WithIterationCount(1000).WithWarmupCount(1);

        this.AddJob(baseConfig.WithRuntime(CoreRuntime.Core10_0).WithPlatform(Platform.X64));

        this.AddExporter(MarkdownExporter.GitHub);
        this.AddExporter(CsvExporter.Default);
        this.AddExporter(RPlotExporter.Default);

        this.AddDiagnoser(MemoryDiagnoser.Default);
    }

}
