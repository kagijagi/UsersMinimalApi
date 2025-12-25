using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;

// UserManagementAPI/Middleware/RequestResponseLoggingMiddleware.cs
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next; // Next middleware in the pipeline
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger; // Logger instance

    // Constructor
    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // Invoke method called for each HTTP request
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request; // Request details

        // Call the next middleware in the pipeline
        await _next(context);

        var response = context.Response;

        // Prepare log entry
        var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " +
                       $"Request: {request.Method} {request.Path} | " +
                       $"Response Status: {response.StatusCode}";

        // ILogger log the error to console
        _logger.LogInformation(logEntry);

        // Log to file
        await WriteToFileAsync(logEntry, LogType.Info);

        // If there was an exception, log its details
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();

        if (exceptionFeature != null)
        {
            var ex = exceptionFeature.Error; // Exception details

            // Prepare error log entry
            var errorLog =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " +
                $"Request: {request.Method} {request.Path} | " +
                $"Response Status: {response.StatusCode} | " +
                $"TraceId: {context.TraceIdentifier}\n" +
                $"Exception: {ex}\n" +
                $"----------------------------------------\n";

            // Log error to file
            await WriteToFileAsync(errorLog, LogType.Error);

            // ILogger log the error to console
            _logger.LogError(ex, "Unhandled exception occurred.");
        }
    }

    // Helper method to write logs to a file
    private async Task WriteToFileAsync(string message, LogType type)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var root = Directory.GetCurrentDirectory();
        var folder = Path.Combine(root, "Logs");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        var fileName = type == LogType.Error
            ? $"logs_ERROR_{today}.txt"
            : $"logs_HTTP_{today}.txt";

        var filePath = Path.Combine(folder, fileName);

        await File.AppendAllTextAsync(filePath, message + Environment.NewLine, Encoding.UTF8);
    }
}