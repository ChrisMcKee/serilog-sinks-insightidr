using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.InsightIDR.Rapid7;

namespace Serilog.Sinks.InsightIDR
{
    public class InsightIdrSink : ILogEventSink, IDisposable
    {
        [ThreadStatic]
        private static StringWriter? _cachedWriter;

        private readonly AsyncLogger _asyncLogger;
        private readonly ITextFormatter _textFormatter;

        /// <summary>
        /// The insightOps sink → a service which sends log messages to insightOps.
        /// </summary>
        /// <param name="config">insightOps settings.</param>
        /// <param name="textFormatter">Formats log events.</param>
        public InsightIdrSink(InsightIdrSinkSettings config, ITextFormatter textFormatter)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _textFormatter = textFormatter;

            ValidateToken(config.Token);

            _asyncLogger = new AsyncLogger();
            _asyncLogger.SetToken(config.Token);
            _asyncLogger.SetRegion(config.Region);
            _asyncLogger.SetUseSsl(config.UseSsl);

            // These options are more or less not used.
            _asyncLogger.SetDebug(config.Debug);
            _asyncLogger.SetUseHostName(config.LogHostname);
            _asyncLogger.SetHostName(config.HostName);
            _asyncLogger.SetLogId(config.LogId);

            if (!config.IsUsingDataHub) return;

            _asyncLogger.SetIsUsingDataHub(config.IsUsingDataHub);
            _asyncLogger.SetDataHubAddr(config.DataHubAddress);
            _asyncLogger.SetDataHubPort(config.DataHubPort);
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null)
                throw new ArgumentNullException(nameof(logEvent));

            var writer = GetWriter();
            _textFormatter.Format(logEvent, writer);
            _asyncLogger.QueueLogEvent(writer.GetStringBuilder().ToString());
        }

        private static StringWriter GetWriter()
        {
            var writer = _cachedWriter;
            if (writer is null)
                return _cachedWriter = new StringWriter(new StringBuilder(256));
            writer.GetStringBuilder().Clear();
            return writer;
        }

        /// <summary>
        /// Dispose should automatically be called by Serilog when it Flushes.
        /// </summary>
        /// <remarks>REF: https://github.com/serilog/serilog/wiki/Developing-a-sink#releasing-resources </remarks>
        public void Dispose()
        {
            if (_asyncLogger is null)
            {
                return;
            }

            var flushed = _asyncLogger.FlushQueue(TimeSpan.FromSeconds(6));
            if (!flushed)
            {
                SelfLog.WriteLine("InsightIDR: failed to flush queue within timeout");
            }

            _asyncLogger.InterruptWorker();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The Token should be a GUID. The InsightOps AsyncLogger does a validation check but quietly
        /// displays an error message to TRACE (which is crap). This can lead to the client NEVER
        /// logging and makes it hard to track down (why this client failed to log).
        /// So - let's be proactive and error this hard, fast, and early.
        /// </summary>
        /// <param name="token"></param>
        private static void ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("The InsightOps Token (which is a Guid) is required. Otherwise, how else are logs going to be sent?");
            }

            var isGuid = Guid.TryParse(token, out var _);
            if (!isGuid)
            {
                throw new Exception($"Provided Token '{token}' is not a valid Guid");
            }
        }
    }
}
