public static class UserValidator
{
    public static (bool IsValid, string? Error) Validate(User user)
    {
        if (user is null) return (false, "User payload is missing!");
        if (user.Id <= 0) return (false, "Id must be > 0!");
        if (string.IsNullOrWhiteSpace(user.FirstName)) return (false, "FirstName is required!");
        if (string.IsNullOrWhiteSpace(user.LastName)) return (false, "LastName is required!");
        if (user.Age <= 0) return (false, "Age must be > 0!");

        return (true, null);
    }
}