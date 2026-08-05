namespace Serilog.Sinks.InsightIDR
{
    public class InsightIdrSinkSettings
    {
        /// <summary>
        /// The unique token GUID of the log to send messages to.
        /// </summary>
        public required string Token { get; set; }

        /// <summary>
        /// Region code: us, eu, ca, au, jp.
        /// </summary>
        /// <see href="https://insightops.help.rapid7.com/docs/rest-api-overview#section-supported-regions"/>
        public required string Region { get; set; }

        /// <summary>
        /// Set to true to use SSL (Token-based or HTTP PUT Logging)
        /// </summary>
        public bool UseSsl { get; set; }

        /// <summary>
        /// Sets the debug flag. Will print error messages to the Serilog SelfLog.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Set to true to use a custom DataHub instance instead of the Rapid7 InsightIDR service.
        /// </summary>
        public bool IsUsingDataHub { get; set; }

        /// <summary>
        /// DataHub server address.
        /// </summary>
        public string DataHubAddress { get; set; } = "";

        /// <summary>
        /// DataHub server port.
        /// </summary>
        public int DataHubPort { get; set; }

        /// <summary>
        /// Set to true to send HostName alongside the log message.
        /// </summary>
        public bool LogHostname { get; set; }

        /// <summary>
        /// User-defined host name. If empty the library will try to obtain it automatically.
        /// </summary>
        public string HostName { get; set; } = "";

        /// <summary>
        /// Log ID.
        /// </summary>
        public string LogId { get; set; } = "";
    }
}
