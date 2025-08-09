using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace JKang.IpcServiceFramework.Client.NamedPipe
{
    internal class NamedPipeIpcClient<TInterface> : IpcClient<TInterface>
        where TInterface : class
    {
        private readonly NamedPipeIpcClientOptions _options;
        private static readonly Random _random = new Random();
        
        // Circuit breaker state
        private volatile int _consecutiveFailures = 0;
        private DateTime _circuitOpenedAt = DateTime.MinValue;
        private readonly object _circuitLock = new object();

        public NamedPipeIpcClient(
            string name,
            NamedPipeIpcClientOptions options)
            : base(name, options)
        {
            _options = options;
        }

        protected override async Task<IpcStreamWrapper> ConnectToServerAsync(CancellationToken cancellationToken)
        {
            // Check circuit breaker state
            if (_options.EnableCircuitBreaker && IsCircuitOpen())
            {
                throw new IpcCommunicationException($"Circuit breaker is open for named pipe '{_options.PipeName}'. Too many consecutive failures detected.");
            }
            
            try
            {
                var result = await ConnectWithRetryAsync(cancellationToken).ConfigureAwait(false);
                
                // Reset circuit breaker on successful connection
                if (_options.EnableCircuitBreaker)
                {
                    ResetCircuitBreaker();
                }
                
                return result;
            }
            catch (Exception)
            {
                // Record failure for circuit breaker
                if (_options.EnableCircuitBreaker)
                {
                    RecordFailure();
                }
                throw;
            }
        }

        private bool IsCircuitOpen()
        {
            lock (_circuitLock)
            {
                if (_consecutiveFailures < _options.CircuitBreakerFailureThreshold)
                {
                    return false;
                }
                
                // Check if enough time has passed to attempt reconnection
                if (DateTime.UtcNow.Subtract(_circuitOpenedAt).TotalMilliseconds >= _options.CircuitBreakerTimeoutMs)
                {
                    // Reset for half-open state
                    _consecutiveFailures = _options.CircuitBreakerFailureThreshold - 1;
                    return false;
                }
                
                return true;
            }
        }

        private void RecordFailure()
        {
            lock (_circuitLock)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures >= _options.CircuitBreakerFailureThreshold)
                {
                    _circuitOpenedAt = DateTime.UtcNow;
                }
            }
        }

        private void ResetCircuitBreaker()
        {
            lock (_circuitLock)
            {
                _consecutiveFailures = 0;
                _circuitOpenedAt = DateTime.MinValue;
            }
        }

        private async Task<IpcStreamWrapper> ConnectWithRetryAsync(CancellationToken cancellationToken)
        {
            var maxRetries = Math.Max(1, _options.MaxRetryAttempts);
            var baseDelayMs = Math.Max(0, _options.RetryDelayMs);
            
            Exception lastException = null;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                NamedPipeClientStream stream = null;
                CancellationTokenSource timeoutCts = null;
                CancellationTokenSource combinedCts = null;
                
                try
                {
                    // Check cancellation before each attempt
                    cancellationToken.ThrowIfCancellationRequested();

                    stream = new NamedPipeClientStream(".", _options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    
                    // Create timeout and combined cancellation tokens
                    timeoutCts = new CancellationTokenSource(_options.ConnectionTimeout);
                    combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    
                    // Attempt connection with timeout and cancellation support
                    await stream.ConnectAsync(combinedCts.Token).ConfigureAwait(false);

                    // Validate the connection is actually working and stable
                    if (!stream.IsConnected)
                    {
                        throw new IOException("Named pipe connection established but stream is not connected.");
                    }

                    // Perform additional connection validation if enabled
                    if (_options.EnableConnectionValidation)
                    {
                        // Test connection stability by checking if we can still read/write
                        if (!stream.CanRead || !stream.CanWrite)
                        {
                            throw new IOException("Named pipe connection established but stream is not readable or writable.");
                        }
                    }

                    // Connection successful - clean up temporary resources and return
                    timeoutCts?.Dispose();
                    combinedCts?.Dispose();
                    return new IpcStreamWrapper(stream);
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    // If the original cancellation token was cancelled, don't retry
                    stream?.Dispose();
                    timeoutCts?.Dispose();
                    combinedCts?.Dispose();
                    throw;
                }
                catch (OperationCanceledException ex) when (timeoutCts?.Token.IsCancellationRequested == true)
                {
                    // Timeout occurred - convert to TimeoutException for proper handling
                    stream?.Dispose();
                    timeoutCts?.Dispose();
                    combinedCts?.Dispose();
                    lastException = new TimeoutException($"Connection to named pipe '{_options.PipeName}' timed out after {_options.ConnectionTimeout}ms on attempt {attempt + 1}.", ex);
                    
                    // Only retry if this is a retriable timeout and we have attempts left
                    if (attempt < maxRetries - 1)
                    {
                        continue;
                    }
                    throw lastException;
                }
                catch (Exception ex) when (attempt < maxRetries - 1 && IsRetriableException(ex))
                {
                    // Store exception for potential final throw
                    lastException = ex;
                    
                    // Dispose the failed stream and cleanup
                    stream?.Dispose();
                    timeoutCts?.Dispose();
                    combinedCts?.Dispose();
                    
                    // Exponential backoff with jitter for reliability
                    var delayMs = (int)(baseDelayMs * Math.Pow(2, attempt)) + _random.Next(0, Math.Max(1, baseDelayMs / 2));
                    var delay = TimeSpan.FromMilliseconds(Math.Min(delayMs, _options.MaxRetryDelayMs)); // Cap at max retry delay
                    
                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // If cancelled during delay, don't continue retrying
                        throw;
                    }
                    
                    continue;
                }
                catch (Exception ex)
                {
                    // For non-retriable exceptions or final attempt, cleanup and rethrow
                    lastException = ex;
                    stream?.Dispose();
                    timeoutCts?.Dispose();
                    combinedCts?.Dispose();
                    throw;
                }
            }

            // If we've exhausted all retries, throw the most informative exception
            var finalException = lastException ?? new IOException($"Failed to connect to named pipe '{_options.PipeName}' after {maxRetries} attempts.");
            throw new IOException($"Failed to connect to named pipe '{_options.PipeName}' after {maxRetries} attempts. Last error: {finalException.Message}", finalException);
        }

        private static bool IsRetriableException(Exception ex)
        {
            // Only retry specific exceptions that indicate transient failures
            switch (ex)
            {
                case TimeoutException _:
                    return true;
                    
                case IOException ioEx when IsRetriableIOException(ioEx):
                    return true;
                    
                case UnauthorizedAccessException _:
                    // This might indicate pipe access issues that could be transient
                    return true;
                    
                case InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("pipe"):
                    // Some pipe-specific invalid operation exceptions might be transient
                    return true;
                    
                default:
                    return false;
            }
        }

        private static bool IsRetriableIOException(IOException ioEx)
        {
            // Check for specific error codes that indicate the pipe might be available later
            const int ERROR_PIPE_BUSY = 231;           // All pipe instances are busy
            const int ERROR_FILE_NOT_FOUND = 2;        // Pipe doesn't exist yet
            const int ERROR_PIPE_NOT_CONNECTED = 233;  // Pipe exists but no server connected
            const int ERROR_SEM_TIMEOUT = 121;         // Semaphore timeout
            const int ERROR_BAD_PIPE = 230;            // Pipe is being closed
            
            var hResult = ioEx.HResult & 0xFFFF;
            return hResult == ERROR_PIPE_BUSY || 
                   hResult == ERROR_FILE_NOT_FOUND || 
                   hResult == ERROR_PIPE_NOT_CONNECTED ||
                   hResult == ERROR_SEM_TIMEOUT ||
                   hResult == ERROR_BAD_PIPE;
        }
    }
}
