using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Load JWT settings from configuration
var jwtSection = builder.Configuration.GetRequiredSection("Jwt");
    var issuer = jwtSection.GetValue<string>("Issuer");
    var audience = jwtSection.GetValue<string>("Audience");
    var key = jwtSection.GetValue<string>("Key");

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    // Define a fixed window rate limiter named "fixed" - not used directly here but can be referenced
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 3; // Max 3 requests
        limiterOptions.Window = TimeSpan.FromSeconds(10); // per 10 seconds
        limiterOptions.QueueLimit = 0; // No queuing
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; // Process oldest first
    });

    // Global rate limiter - applies to all requests automatically
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        // Single global partition
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "global",
            factory: _ => new FixedWindowRateLimiterOptions // Global limiter options
            {
                PermitLimit = 100, // Max 100 requests
                Window = TimeSpan.FromMinutes(1), // per minute
                QueueLimit = 3, // Allow small queue
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst // Process oldest first
            }
        )
    );
});

// Configure JWT authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme) // Use JWT Bearer authentication
    .AddJwtBearer(options =>
    {
        // Configure token validation parameters
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, // Validate the issuer
            ValidateAudience = true, // Validate the audience
            ValidateLifetime = true, // Validate token expiration
            ValidateIssuerSigningKey = true, // Validate the signing key

            ValidIssuer = issuer, // Token issuer
            ValidAudience = audience, // Token audience
            IssuerSigningKey = new SymmetricSecurityKey( // Signing key
                Encoding.UTF8.GetBytes(key)) // Secret key
        };
    })
;

// Add authorization services
builder.Services.AddAuthorization();

// Register services
builder.Services.AddSingleton<IRequestCounterService, RequestCounterService>();

// Register JwtTokenService
builder.Services.AddSingleton(new JwtTokenService(
    issuer: issuer,
    audience: audience,
    key: key
));

var app = builder.Build();

// Generate and print a test JWT token
var jwt = app.Services.GetRequiredService<JwtTokenService>(); // Get JwtTokenService instance
Console.WriteLine("TEST TOKEN:");
// Generate a JWT token for user "Nikolai" with role "admin"
Console.WriteLine(jwt.GenerateJwtToken("Nikolai", "admin"));

// Use exception handler middleware
app.UseExceptionHandler("/error");

// Use request counting middleware
app.UseMiddleware<RequestCountingMiddleware>();

// Use authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Use rate limiting middleware
app.UseRateLimiter();

// Use logging middleware
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// In-memory concurrent store
var users = new ConcurrentDictionary<int, User>();

// Seed sample users
users.TryAdd(1, new User { Id = 1, FirstName = "John", LastName = "Doe", Age = 30, IsCustomer = true });
users.TryAdd(2, new User { Id = 2, FirstName = "Jane", LastName = "Smith", Age = 25, IsCustomer = false });
users.TryAdd(3, new User { Id = 3, FirstName = "Alex", LastName = "Johnson", Age = 40, IsCustomer = true });

// Error handling endpoint
app.MapGet("/error", (HttpContext context) =>
{
    var response = new
    {
        error = "Internal server error.",
        traceId = context.TraceIdentifier
    };

    return Results.Json(response, statusCode: StatusCodes.Status500InternalServerError);
});

// Test exception endpoint
app.MapGet("/boom", () =>
{
    throw new Exception("Test exception from /boom"); // This will be caught by the exception handler middleware
});


// ROOT
app.MapGet("/", () => Results.Ok(new { message = "Minimal Users API" }));

// GET all users with optional filtering
app.MapGet("/users", (string? search, bool? isCustomer) =>
{
    IEnumerable<User> result = users.Values;

    // Filter by IsCustomer - faster filter first
    if (isCustomer.HasValue)
    {
        result = result.Where(u => u.IsCustomer == isCustomer.Value);
    }

    // Filter by search term in FirstName or LastName
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        
        result = result
            .Where(u =>
                u.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(term, StringComparison.OrdinalIgnoreCase));
            //.OrderBy(u => u.LastName); - expensive operation, avoid if possible
    }

    return Results.Ok(result);
}).RequireRateLimiting("fixed"); // Apply fixed rate limiter to this endpoint

// GET user by Id with error handling
app.MapGet("/users/{id:int}", (int id) =>
{
    try
    {
        // Validate Id
        if (id <= 0)
            return Results.BadRequest("Id must be > 0.");

        // Retrieve user
        return users.TryGetValue(id, out var user)
            ? Results.Ok(user)
            : Results.NotFound($"User with Id {id} not found.");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unexpected error: {ex.Message}");
    }
});

// POST create new user
app.MapPost("/users", (User newUser) =>
{
    try
    {
        // Validate user
        var (IsValid, Error) = UserValidator.Validate(newUser);
        if (!IsValid)
            return Results.BadRequest(Error);

        // Attempt to add new user
        if (!users.TryAdd(newUser.Id, newUser))
            return Results.Conflict($"User with Id {newUser.Id} already exists.");

        return Results.Created($"/users/{newUser.Id}", newUser);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unexpected error: {ex.Message}");
    }
}).RequireAuthorization(); // Require authorization for creating users

// PUT update existing user with error handling
app.MapPut("/users/{id:int}", (int id, User updatedUser) =>
{
    try
    {
        // Validate Id
        if (id <= 0)
            return Results.BadRequest("Id must be > 0.");

        // Validate user
        var (IsValid, Error) = UserValidator.Validate(updatedUser);
        if (!IsValid)
            return Results.BadRequest(Error);

        // Check existence
        if (!users.TryGetValue(id, out var existingUser))
            return Results.NotFound($"User with Id {id} not found.");

        updatedUser.Id = id; // enforce ID consistency

        // Atomic compare-and-swap update
        if (!users.TryUpdate(id, updatedUser, existingUser))
            return Results.Conflict($"User with Id {id} could not be updated due to a concurrent modification.");

        return Results.Ok(updatedUser);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unexpected error: {ex.Message}");
    }
}).RequireAuthorization(); // Require authorization for updating users

// DELETE user by Id
app.MapDelete("/users/{id:int}", (int id) =>
{
    try
    {
        // Validate Id
        if (id <= 0)
            return Results.BadRequest("Id must be > 0.");

        // Attempt to remove user
        return users.TryRemove(id, out _)
            ? Results.Ok($"User with Id {id} deleted.")
            : Results.NotFound($"User with Id {id} not found.");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unexpected error: {ex.Message}");
    }
}).RequireAuthorization(); // Require authorization for deleting users

// GET metrics
app.MapGet("/metrics", (IRequestCounterService counterService) =>
{
    return Results.Ok(counterService.GetMetrics());
}).RequireAuthorization(); // Require authorization for metrics

app.Run();
