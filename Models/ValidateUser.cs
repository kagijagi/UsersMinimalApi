// UserManagementAPI/Models/ValidateUser.cs
public static class UserValidator
{
    // Validates a User object and returns a tuple indicating validity and an error message if invalid
    public static (bool IsValid, string? Error) Validate(User user)
    {
        if (user is null) return (false, "User payload is missing!"); // Null check
        if (user.Id <= 0) return (false, "Id must be > 0!"); // ID check
        if (string.IsNullOrWhiteSpace(user.FirstName)) return (false, "FirstName is required!"); // FirstName check
        if (string.IsNullOrWhiteSpace(user.LastName)) return (false, "LastName is required!"); // LastName check
        if (user.Age <= 0) return (false, "Age must be > 0!"); // Age check

        return (true, null); // Valid user
    }
}