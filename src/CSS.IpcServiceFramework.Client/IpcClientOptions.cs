using CSS.IpcServiceFramework.Services;
using System;
using System.IO;

namespace CSS.IpcServiceFramework.Client
{
    public class IpcClientOptions
    {
        public Func<Stream, Stream> StreamTranslator { get; set; }

        /// <summary>
        /// The number of milliseconds to wait for the server to respond before
        /// the connection times out. Default value is 60000.
        /// </summary>
        public int ConnectionTimeout { get; set; } = 60000;

        /// <summary>
        /// The maximum number of retry attempts for connection failures. Default value is 3.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// The base delay in milliseconds between retry attempts. Default value is 100.
        /// The actual delay will use exponential backoff: baseDelay * 2^attempt + random jitter.
        /// </summary>
        public int RetryDelayMs { get; set; } = 100;

        /// <summary>
        /// Whether to enable connection stability validation after establishing connection.
        /// When enabled, the client will perform additional checks to ensure the connection is stable.
        /// Default value is true.
        /// </summary>
        public bool EnableConnectionValidation { get; set; } = true;

        /// <summary>
        /// The maximum delay in milliseconds between retry attempts to prevent excessively long waits.
        /// Default value is 5000 (5 seconds).
        /// </summary>
        public int MaxRetryDelayMs { get; set; } = 5000;

        /// <summary>
        /// Whether to enable circuit breaker pattern for connection failures.
        /// When enabled, the client will temporarily stop attempting connections after repeated failures.
        /// Default value is true.
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// The number of consecutive failures required to open the circuit breaker.
        /// Default value is 5.
        /// </summary>
        public int CircuitBreakerFailureThreshold { get; set; } = 5;

        /// <summary>
        /// The duration in milliseconds to keep the circuit breaker open before attempting to reconnect.
        /// Default value is 30000 (30 seconds).
        /// </summary>
        public int CircuitBreakerTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Indicates the method that will be used during deserialization on the server for locating and loading assemblies.
        /// If <c>false</c>, the assembly used during deserialization must match exactly the assembly used during serialization.
        /// 
        /// If <c>true</c>, the assembly used during deserialization need not match exactly the assembly used during serialization.
        /// Specifically, the version numbers need not match.
        /// 
        /// Default is <c>false</c>.
        /// </summary>
        public bool UseSimpleTypeNameAssemblyFormatHandling { get; set; } = false;

        public IIpcMessageSerializer Serializer { get; set; } = new DefaultIpcMessageSerializer();

        public IValueConverter ValueConverter { get; set; } = new DefaultValueConverter();
    }
}
