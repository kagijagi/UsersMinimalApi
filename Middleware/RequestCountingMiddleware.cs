//
public class RequestCountingMiddleware
{
    // Dependencies
    private readonly RequestDelegate _next; // Next middleware in the pipeline
    private readonly IRequestCounterService _counterService; // Service to count requests

    // Constructor
    public RequestCountingMiddleware(RequestDelegate next, IRequestCounterService counterService)
    {
        _next = next; // Next middleware
        _counterService = counterService; // Injected request counter service
    }

    // Invoke method called for each HTTP request
    public async Task InvokeAsync(HttpContext context)
    {
        // Call the next middleware first
        await _next(context);

        // Get the route name from the endpoint metadata
        var endpoint = context.GetEndpoint();
        var routeName = endpoint?.DisplayName ?? "Unknown";

        // Increment the request count for this route
        _counterService.Increment(routeName);
    }
}