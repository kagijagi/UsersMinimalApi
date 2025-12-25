using System.Collections.Concurrent;

// UserManagementAPI/Services/RequestCounterService.cs
public class RequestCounterService : IRequestCounterService
{
    // Thread-safe dictionary to hold route counters
    private readonly ConcurrentDictionary<string, long> _counters = new();

    // Increment the counter for a specific route
    public void Increment(string route)
    {
        // Add or update the counter atomically
        _counters.AddOrUpdate(
            route, // key 
            1, // initial value
            (_, old) => old + 1 // update function
        );
    }

    // Retrieve all route metrics
    public IReadOnlyDictionary<string, long> GetMetrics() => _counters;
}