using System.Text;

namespace MaxioMockServer.Middleware;

/// <summary>
/// Simple, dependency-free request/response logger. For every request it logs the
/// incoming method/path/query, then buffers the response so it can log the status
/// code and body before streaming it back to the client. Output goes to the console
/// (via <see cref="ILogger"/>) and is appended to a daily file under <c>logs/</c>.
/// </summary>
public sealed class RequestResponseLoggingMiddleware
{
    // Cap the amount of response body written to the log so a large payload
    // doesn't flood the console/file. The full body is still sent to the client.
    private const int MaxLoggedBodyChars = 2_000;

    private static readonly object FileLock = new();

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly string _logDirectory;

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _logDirectory = Path.Combine(env.ContentRootPath, "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var query = request.QueryString.HasValue ? request.QueryString.Value : "";
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var requestLine = $"--> {request.Method} {request.Path}{query} (from {remoteIp})";
        _logger.LogInformation("{RequestLine}", requestLine);

        // Swap the response body for a buffer we can read after the pipeline runs.
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Position = 0;
            var responseText = await new StreamReader(buffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();

            // An aborted connection (e.g. a simulated connection break) never wrote a real status line -
            // context.Response.StatusCode is just its unset default (200), which would misleadingly log as
            // a normal 200-with-no-body response. Log it plainly as ABORTED instead.
            var responseLine = context.RequestAborted.IsCancellationRequested
                ? $"<-- ABORTED {request.Method} {request.Path}{query} (connection reset, 0 bytes sent)"
                : $"<-- {context.Response.StatusCode} {request.Method} {request.Path}{query} " +
                  $"({buffer.Length} bytes) {Truncate(responseText, MaxLoggedBodyChars)}";
            _logger.LogInformation("{ResponseLine}", responseLine);

            WriteToFile(requestLine, responseLine);

            // Stream the buffered response back to the real network stream - unless the connection was
            // aborted (e.g. a simulated connection break via HttpContext.Abort()), in which case there is
            // nothing left to write to.
            buffer.Position = 0;
            if (!context.RequestAborted.IsCancellationRequested)
            {
                try
                {
                    await buffer.CopyToAsync(originalBody);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException)
                {
                    // Connection is gone; nothing more to do.
                }
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max
            ? value
            : value[..max] + $"... [truncated, {value.Length} chars total]";

    private void WriteToFile(string requestLine, string responseLine)
    {
        // Use UTC "o" round-trip timestamps; DateTime.UtcNow is fine here (not in a
        // deterministic-replay context).
        var timestamp = DateTime.UtcNow.ToString("O");
        var path = Path.Combine(_logDirectory, $"requests-{DateTime.UtcNow:yyyy-MM-dd}.log");
        var line = $"[{timestamp}] {requestLine}{Environment.NewLine}[{timestamp}] {responseLine}{Environment.NewLine}";

        lock (FileLock)
        {
            File.AppendAllText(path, line);
        }
    }
}
