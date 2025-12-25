// UserManagementAPI/Services/Interfaces/IRequestCounterService.cs
public interface IRequestCounterService
{
    void Increment(string route); // Increment counter for a route
    IReadOnlyDictionary<string, long> GetMetrics(); // Get all route metrics
}