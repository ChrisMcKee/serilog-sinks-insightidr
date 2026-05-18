using Serilog.Sinks.InsightIDR.Rapid7;

namespace Serilog.Sinks.InsightIDR
{
    public class InsightIdrSinkSettings : IAsyncLoggerConfig
    {
        /// <summary>
        /// The unique token GUID of the log to send messages to. This applies when using the newer token-based logging.
        /// </summary>
        public string Token { get; set ; }

        /// <summary>
        /// Supported Region's.
        /// </summary>
        /// <remarks>List: us, eu, ca, au, jp</remarks>
        /// <see href="https://insightops.help.rapid7.com/docs/rest-api-overview#section-supported-regions"/>
        public string Region { get; set; }

        /// <inheritdoc />
        public bool UseSsl { get; set; }

        /// <inheritdoc />
        public bool Debug { get; set; }

        /// <inheritdoc />
        public bool IsUsingDataHub { get; set; }

        /// <inheritdoc />
        public string DataHubAddress { get; set; }

        /// <inheritdoc />
        public int DataHubPort { get; set; }

        /// <inheritdoc />
        public bool LogHostname { get; set; }

        /// <inheritdoc />
        public string HostName { get; set; }

        /// <inheritdoc />
        public string LogId { get; set; }
    }
}
